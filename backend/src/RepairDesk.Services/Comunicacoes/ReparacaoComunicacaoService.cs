using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Comunicacoes;

public sealed record ReparacaoComunicacaoDto(
    Guid Id,
    Guid ReparacaoId,
    Guid ClienteId,
    ComunicacaoTipo Tipo,
    ComunicacaoDirecao Direcao,
    string Texto,
    Guid CreatedByUserId,
    DateTime CreatedAt);

public sealed record CreateComunicacaoRequest(
    ComunicacaoTipo Tipo,
    ComunicacaoDirecao Direcao,
    string Texto);

public interface IReparacaoComunicacaoService
{
    Task<IReadOnlyList<ReparacaoComunicacaoDto>> ListAsync(Guid reparacaoId, CancellationToken ct = default);
    /// <summary>Sprint 453: histórico cliente — todas as comunicações em todas as reparações deste cliente.</summary>
    Task<IReadOnlyList<ReparacaoComunicacaoDto>> ListByClienteAsync(Guid clienteId, int take, CancellationToken ct = default);
    Task<ReparacaoComunicacaoDto> CreateAsync(Guid reparacaoId, CreateComunicacaoRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class ReparacaoComunicacaoService : IReparacaoComunicacaoService
{
    private readonly IReparacaoComunicacaoRepository _repo;
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;

    public ReparacaoComunicacaoService(
        IReparacaoComunicacaoRepository repo,
        IReparacaoRepository reparacoes,
        ITenantContext tenant,
        ICurrentUser user,
        IAuditLogger audit)
    {
        _repo = repo;
        _reparacoes = reparacoes;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ReparacaoComunicacaoDto>> ListAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        // Confirma que a reparação pertence ao tenant (filter global já valida; FindByIdAsync devolve null se não pertence).
        _ = await _reparacoes.FindByIdAsync(reparacaoId, ct) ?? throw new NotFoundException("Reparacao", reparacaoId);
        var list = await _repo.ListByReparacaoAsync(reparacaoId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ReparacaoComunicacaoDto>> ListByClienteAsync(Guid clienteId, int take, CancellationToken ct = default)
    {
        // Tenant isolation: o IClienteRepository tem global query filter por tenant; FindByIdAsync devolve null cross-tenant.
        // Sem necessidade de ler o cliente — basta filter via clienteId no repo (que também tem filtro global).
        var list = await _repo.ListByClienteAsync(clienteId, take, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ReparacaoComunicacaoDto> CreateAsync(Guid reparacaoId, CreateComunicacaoRequest req, CancellationToken ct = default)
    {
        var texto = (req.Texto ?? "").Trim();
        if (texto.Length is < 1 or > 2000)
            throw new ValidationException("texto_invalido", "Texto obrigatório (1 a 2000 caracteres).");
        if (_user.UserId is not { } uid)
            throw new ValidationException("user_required", "Sessão sem utilizador associado.");

        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct) ?? throw new NotFoundException("Reparacao", reparacaoId);

        var entry = new ReparacaoComunicacao
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId ?? Guid.Empty,
            ReparacaoId = rep.Id,
            ClienteId = rep.ClienteId,
            Tipo = req.Tipo,
            Direcao = req.Direcao,
            Texto = texto,
            CreatedByUserId = uid,
        };
        await _repo.AddAsync(entry, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Create, "ReparacaoComunicacao", entry.Id,
            new { entry.ReparacaoId, tipo = entry.Tipo.ToString(), direcao = entry.Direcao.ToString() },
            entry.TenantId, uid, ct);

        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("ReparacaoComunicacao", id);
        _repo.Remove(entry);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, "ReparacaoComunicacao", id,
            new { entry.ReparacaoId }, entry.TenantId, _user.UserId, ct);
    }

    private static ReparacaoComunicacaoDto ToDto(ReparacaoComunicacao c) => new(
        c.Id, c.ReparacaoId, c.ClienteId, c.Tipo, c.Direcao, c.Texto, c.CreatedByUserId, c.CreatedAt);
}
