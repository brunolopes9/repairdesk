using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Cash;

/// <summary>
/// Sprint 300 (Doc 80 Pillar A.1).
///
/// **Concurrency note:** Open/Close usam o índice unique (TenantId, LocationId, Date)
/// como guarda. Race "duas abas a abrir dia em simultâneo" devolve DbUpdateException
/// no segundo SaveChanges → traduzimos para ConflictException.
/// </summary>
public sealed class CashService : ICashService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _http;

    public CashService(
        AppDbContext db,
        ITenantContext tenant,
        TimeProvider clock,
        IAuditLogger audit,
        Microsoft.AspNetCore.Http.IHttpContextAccessor http)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
        _audit = audit;
        _http = http;
    }

    public async Task<DailyClosingDto> OpenDayAsync(OpenDayRequest req, CancellationToken ct = default)
    {
        EnsureTenant();
        if (req.OpeningCents < 0)
            throw new ValidationException("invalid_opening", "Saldo inicial não pode ser negativo.");

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var existing = await _db.DailyClosings
            .FirstOrDefaultAsync(c => c.Date == today && c.LocationId == req.LocationId, ct);
        if (existing is not null)
            throw new ConflictException("day_already_open", "Caixa já foi aberta hoje.");

        var closing = new DailyClosing
        {
            TenantId = _tenant.TenantId!.Value,
            LocationId = req.LocationId,
            Date = today,
            Status = DailyClosingStatus.Open,
            OpeningCents = req.OpeningCents,
            ExpectedClosingCents = req.OpeningCents,
            OpenedAt = _clock.GetUtcNow().UtcDateTime,
            OpenedByUserId = CurrentUserId(),
            Notas = req.Notas,
        };
        _db.DailyClosings.Add(closing);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("day_already_open", "Caixa já foi aberta hoje (race).");
        }

        await _audit.LogAsync(AuditAction.Create, nameof(DailyClosing), closing.Id,
            new { closing.Date, closing.OpeningCents, closing.LocationId }, ct: ct);

        return await BuildDtoAsync(closing.Id, ct);
    }

    public async Task<DailyClosingDto?> GetTodayAsync(Guid? locationId, CancellationToken ct = default)
    {
        EnsureTenant();
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        return await GetByDateAsync(today, locationId, ct);
    }

    public async Task<DailyClosingDto?> GetByDateAsync(DateOnly date, Guid? locationId, CancellationToken ct = default)
    {
        EnsureTenant();
        var closing = await _db.DailyClosings
            .FirstOrDefaultAsync(c => c.Date == date && c.LocationId == locationId, ct);
        return closing is null ? null : await BuildDtoAsync(closing.Id, ct);
    }

    public async Task<DailyClosingDto?> GetByIdAsync(Guid dailyClosingId, CancellationToken ct = default)
    {
        EnsureTenant();
        var closing = await _db.DailyClosings.FirstOrDefaultAsync(c => c.Id == dailyClosingId, ct);
        return closing is null ? null : await BuildDtoAsync(closing.Id, ct);
    }

    public async Task<CashMovementDto> RecordMovementAsync(RecordMovementRequest req, CancellationToken ct = default)
    {
        EnsureTenant();
        if (req.AmountCents <= 0)
            throw new ValidationException("invalid_amount", "Valor tem de ser positivo (o sinal vem do Type).");
        if (string.IsNullOrWhiteSpace(req.Descricao))
            throw new ValidationException("missing_descricao", "Descrição obrigatória.");

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var closing = await _db.DailyClosings
            .FirstOrDefaultAsync(c => c.Date == today && c.LocationId == req.LocationId && c.Status == DailyClosingStatus.Open, ct);

        // Auto-open: se ninguém abriu caixa hoje, abrir com opening=0 (operador pode corrigir
        // editando depois — mas é melhor registar movimentos do que perder).
        if (closing is null)
        {
            closing = new DailyClosing
            {
                TenantId = _tenant.TenantId!.Value,
                LocationId = req.LocationId,
                Date = today,
                Status = DailyClosingStatus.Open,
                OpeningCents = 0,
                ExpectedClosingCents = 0,
                OpenedAt = _clock.GetUtcNow().UtcDateTime,
                OpenedByUserId = CurrentUserId(),
                Notas = "Auto-aberta no primeiro movimento.",
            };
            _db.DailyClosings.Add(closing);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Outra thread abriu primeiro — recarregar
                closing = await _db.DailyClosings.FirstAsync(c => c.Date == today && c.LocationId == req.LocationId, ct);
            }
        }

        var movement = new CashMovement
        {
            TenantId = _tenant.TenantId!.Value,
            LocationId = req.LocationId,
            DailyClosingId = closing.Id,
            Type = req.Type,
            PaymentMethod = req.PaymentMethod,
            AmountCents = req.AmountCents,
            Descricao = req.Descricao,
            VendaId = req.VendaId,
            ReparacaoId = req.ReparacaoId,
            OccurredAt = _clock.GetUtcNow().UtcDateTime,
            RecordedByUserId = CurrentUserId(),
        };
        _db.CashMovements.Add(movement);

        // Update totals do closing — separar por método e por entrada/saída de DINHEIRO.
        var signedAmount = IsExit(req.Type) ? -req.AmountCents : req.AmountCents;
        switch (req.PaymentMethod)
        {
            case PaymentMethod.Dinheiro:
                if (signedAmount > 0) closing.CashEntriesCents += signedAmount;
                else closing.CashExitsCents += -signedAmount;
                closing.ExpectedClosingCents += signedAmount;
                break;
            case PaymentMethod.MBWay:
                closing.MbwayCents += signedAmount;
                break;
            case PaymentMethod.Multibanco:
                closing.MultibancoCents += signedAmount;
                break;
            default:
                closing.OtherCents += signedAmount;
                break;
        }
        closing.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        return ToMovementDto(movement);
    }

    public async Task<IReadOnlyList<DailyClosingDto>> ListRecentAsync(int take, Guid? locationId, CancellationToken ct = default)
    {
        EnsureTenant();
        take = Math.Clamp(take, 1, 100);
        var closings = await _db.DailyClosings
            .Where(c => locationId == null || c.LocationId == locationId)
            .OrderByDescending(c => c.Date)
            .Take(take)
            .ToListAsync(ct);
        return closings.Select(c => ToDto(c, Array.Empty<CashMovementDto>())).ToList();
    }

    public async Task<DailyClosingDto> CloseDayAsync(Guid dailyClosingId, CloseDayRequest req, CancellationToken ct = default)
    {
        EnsureTenant();
        if (req.ActualClosingCents < 0)
            throw new ValidationException("invalid_actual", "Saldo final não pode ser negativo.");

        var closing = await _db.DailyClosings.FirstOrDefaultAsync(c => c.Id == dailyClosingId, ct)
            ?? throw new NotFoundException(nameof(DailyClosing), dailyClosingId);
        if (closing.Status != DailyClosingStatus.Open)
            throw new ConflictException("not_open", "Caixa não está aberta — não se fecha duas vezes.");

        closing.ActualClosingCents = req.ActualClosingCents;
        closing.DiffCents = req.ActualClosingCents - closing.ExpectedClosingCents;
        closing.Status = DailyClosingStatus.Closed;
        closing.ClosedAt = _clock.GetUtcNow().UtcDateTime;
        closing.ClosedByUserId = CurrentUserId();
        if (!string.IsNullOrWhiteSpace(req.Notas))
            closing.Notas = string.IsNullOrWhiteSpace(closing.Notas) ? req.Notas : $"{closing.Notas}\n---\n{req.Notas}";
        closing.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.Update, nameof(DailyClosing), closing.Id,
            new { operation = "close", closing.ExpectedClosingCents, closing.ActualClosingCents, closing.DiffCents }, ct: ct);

        return await BuildDtoAsync(closing.Id, ct);
    }

    // ===== Helpers =====

    private static bool IsExit(CashMovementType type) =>
        type is CashMovementType.Sangria or CashMovementType.DespesaCaixa or CashMovementType.Troco;

    private async Task<DailyClosingDto> BuildDtoAsync(Guid id, CancellationToken ct)
    {
        var closing = await _db.DailyClosings.FirstAsync(c => c.Id == id, ct);
        var movimentos = await _db.CashMovements
            .Where(m => m.DailyClosingId == id)
            .OrderByDescending(m => m.OccurredAt)
            .Select(m => ToMovementDto(m))
            .ToListAsync(ct);
        return ToDto(closing, movimentos);
    }

    private static DailyClosingDto ToDto(DailyClosing c, IReadOnlyList<CashMovementDto> movimentos) =>
        new(c.Id, c.Date, c.Status, c.LocationId,
            c.OpeningCents, c.ExpectedClosingCents, c.ActualClosingCents, c.DiffCents,
            c.CashEntriesCents, c.CashExitsCents, c.MbwayCents, c.MultibancoCents, c.CardCents, c.OtherCents,
            c.ZReportPdfUrl, c.OpenedAt, c.ClosedAt, c.Notas, movimentos);

    private static CashMovementDto ToMovementDto(CashMovement m) =>
        new(m.Id, m.Type, m.PaymentMethod, m.AmountCents, m.Descricao,
            m.VendaId, m.ReparacaoId, m.OccurredAt);

    private void EnsureTenant()
    {
        if (_tenant.TenantId is null)
            throw new ForbiddenException("no_tenant_context", "Sem contexto de tenant.");
    }

    private Guid? CurrentUserId()
    {
        var sub = _http.HttpContext?.User?.FindFirst("sub")?.Value
            ?? _http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
