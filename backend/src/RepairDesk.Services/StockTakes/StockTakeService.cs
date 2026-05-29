using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.StockTakes;

public sealed record StockTakeItemDto(
    Guid Id,
    Guid PartId,
    string PartNome,
    string? PartSku,
    string? PartMarca,
    string? PartModelo,
    string? LocalArmazenamento,
    int QtdSistema,
    int? QtdContada,
    int Diferenca,
    DateTime? ContadoEm);

public sealed record StockTakeDto(
    Guid Id,
    DateTime OpenedAt,
    Guid OpenedByUserId,
    DateTime? ClosedAt,
    Guid? ClosedByUserId,
    StockTakeStatus Status,
    string? Notas,
    int TotalItems,
    int ContadosCount,
    int DiferencasCount,
    IReadOnlyList<StockTakeItemDto>? Items);

public sealed record CountItemRequest(int QtdContada);
public sealed record CloseStockTakeRequest(string? Notas);

public interface IStockTakeService
{
    Task<StockTakeDto?> GetCurrentAsync(CancellationToken ct = default);
    Task<StockTakeDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockTakeDto>> ListRecentAsync(int take = 20, CancellationToken ct = default);
    Task<StockTakeDto> OpenAsync(CancellationToken ct = default);
    Task<StockTakeItemDto> CountItemAsync(Guid stockTakeId, Guid partId, int qtdContada, CancellationToken ct = default);
    Task<StockTakeDto> CloseAsync(Guid stockTakeId, string? notas, CancellationToken ct = default);
    Task<StockTakeDto> CancelAsync(Guid stockTakeId, CancellationToken ct = default);
}

public sealed class StockTakeService : IStockTakeService
{
    private readonly IStockTakeRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;

    public StockTakeService(
        IStockTakeRepository repo,
        ITenantContext tenant,
        ICurrentUser user,
        IAuditLogger audit)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task<StockTakeDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        var current = await _repo.GetCurrentOpenAsync(ct);
        if (current is null) return null;
        // GetCurrentOpenAsync devolve sem Items — vou buscar com Items.
        var full = await _repo.FindByIdAsync(current.Id, includeItems: true, ct);
        return full is null ? null : ToDto(full, includeItems: true);
    }

    public async Task<StockTakeDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _repo.FindByIdAsync(id, includeItems: true, ct)
                ?? throw new NotFoundException("StockTake", id);
        return ToDto(s, includeItems: true);
    }

    public async Task<IReadOnlyList<StockTakeDto>> ListRecentAsync(int take = 20, CancellationToken ct = default)
    {
        var list = await _repo.ListAsync(Math.Clamp(take, 1, 100), ct);
        return list.Select(s => ToDto(s, includeItems: false)).ToList();
    }

    public async Task<StockTakeDto> OpenAsync(CancellationToken ct = default)
    {
        var existing = await _repo.GetCurrentOpenAsync(ct);
        if (existing is not null)
            throw new ConflictException("stocktake_already_open", "Já existe um inventário aberto. Fecha-o antes de iniciar outro.");

        if (_user.UserId is not { } uid)
            throw new ValidationException("user_required", "Sessão sem utilizador associado.");

        var parts = await _repo.ListActivePartsAsync(ct);
        var stockTake = new StockTake
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId ?? Guid.Empty,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = uid,
            Status = StockTakeStatus.Aberto,
        };
        await _repo.AddAsync(stockTake, ct);

        var items = parts.Select(p => new StockTakeItem
        {
            Id = Guid.NewGuid(),
            TenantId = stockTake.TenantId,
            StockTakeId = stockTake.Id,
            PartId = p.Id,
            QtdSistema = p.QtdStock,
        }).ToList();
        await _repo.AddItemsAsync(items, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Create, "StockTake", stockTake.Id,
            new { items = items.Count }, stockTake.TenantId, uid, ct);

        return await GetByIdAsync(stockTake.Id, ct);
    }

    public async Task<StockTakeItemDto> CountItemAsync(Guid stockTakeId, Guid partId, int qtdContada, CancellationToken ct = default)
    {
        if (qtdContada < 0)
            throw new ValidationException("qtd_invalida", "Quantidade contada não pode ser negativa.");

        var st = await _repo.FindByIdAsync(stockTakeId, includeItems: true, ct)
                 ?? throw new NotFoundException("StockTake", stockTakeId);
        if (st.Status != StockTakeStatus.Aberto)
            throw new ConflictException("stocktake_not_open", "Inventário não está aberto.");

        var item = st.Items.FirstOrDefault(i => i.PartId == partId)
                   ?? throw new NotFoundException("StockTakeItem", partId);

        item.QtdContada = qtdContada;
        item.ContadoEm = DateTime.UtcNow;
        item.ContadoByUserId = _user.UserId;
        await _repo.SaveAsync(ct);

        return ToItemDto(item);
    }

    public async Task<StockTakeDto> CloseAsync(Guid stockTakeId, string? notas, CancellationToken ct = default)
    {
        var st = await _repo.FindByIdAsync(stockTakeId, includeItems: true, ct)
                 ?? throw new NotFoundException("StockTake", stockTakeId);
        if (st.Status != StockTakeStatus.Aberto)
            throw new ConflictException("stocktake_not_open", "Inventário não está aberto.");
        if (_user.UserId is not { } uid)
            throw new ValidationException("user_required", "Sessão sem utilizador associado.");

        // Para cada item contado com diferença != 0, criar PartMovimento de ajuste.
        var ajustes = 0;
        foreach (var item in st.Items)
        {
            if (item.QtdContada is not { } qtd) continue;
            var diff = qtd - item.QtdSistema;
            if (diff == 0) continue;

            var part = await _repo.FindPartByIdAsync(item.PartId, ct);
            if (part is null) continue;

            var stockAntes = part.QtdStock;
            var stockDepois = stockAntes + diff;
            if (stockDepois < 0)
                throw new ConflictException("stock_negativo",
                    $"Ajuste para '{part.Nome}' deixava stock negativo (antes {stockAntes}, ajuste {diff}).");

            part.QtdStock = stockDepois;
            _repo.AddMovimento(new PartMovimento
            {
                PartId = part.Id,
                Quantidade = diff,
                StockAntes = stockAntes,
                StockDepois = stockDepois,
                Motivo = PartMovimentoMotivo.AjusteManual,
                Notas = $"Inventário {st.OpenedAt:yyyy-MM-dd}: contado {qtd}, sistema {item.QtdSistema}.",
            });
            ajustes++;
        }

        st.Status = StockTakeStatus.Concluido;
        st.ClosedAt = DateTime.UtcNow;
        st.ClosedByUserId = uid;
        if (!string.IsNullOrWhiteSpace(notas)) st.Notas = notas.Trim();
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, "StockTake", st.Id,
            new { closed = true, ajustes }, st.TenantId, uid, ct);

        return ToDto(st, includeItems: true);
    }

    public async Task<StockTakeDto> CancelAsync(Guid stockTakeId, CancellationToken ct = default)
    {
        var st = await _repo.FindByIdAsync(stockTakeId, includeItems: true, ct)
                 ?? throw new NotFoundException("StockTake", stockTakeId);
        if (st.Status != StockTakeStatus.Aberto)
            throw new ConflictException("stocktake_not_open", "Inventário não está aberto.");

        st.Status = StockTakeStatus.Cancelado;
        st.ClosedAt = DateTime.UtcNow;
        st.ClosedByUserId = _user.UserId;
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, "StockTake", st.Id,
            new { cancelled = true }, st.TenantId, _user.UserId, ct);

        return ToDto(st, includeItems: true);
    }

    private static StockTakeDto ToDto(StockTake s, bool includeItems)
    {
        var items = s.Items ?? new List<StockTakeItem>();
        var contados = items.Count(i => i.QtdContada is not null);
        var diferencas = items.Count(i => i.QtdContada is { } q && q != i.QtdSistema);
        return new StockTakeDto(
            s.Id, s.OpenedAt, s.OpenedByUserId, s.ClosedAt, s.ClosedByUserId, s.Status, s.Notas,
            items.Count, contados, diferencas,
            includeItems
                ? items.OrderBy(i => i.Part?.Nome).Select(ToItemDto).ToList()
                : null);
    }

    private static StockTakeItemDto ToItemDto(StockTakeItem i) => new(
        i.Id,
        i.PartId,
        i.Part?.Nome ?? "(?)",
        i.Part?.Sku,
        i.Part?.Marca,
        i.Part?.Modelo,
        i.Part?.LocalArmazenamento,
        i.QtdSistema,
        i.QtdContada,
        (i.QtdContada ?? i.QtdSistema) - i.QtdSistema,
        i.ContadoEm);
}
