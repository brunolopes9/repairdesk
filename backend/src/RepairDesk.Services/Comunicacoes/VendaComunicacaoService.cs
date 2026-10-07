using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Comunicacoes;

public sealed record VendaComunicacaoDto(
    Guid Id,
    Guid VendaId,
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

public interface IVendaComunicacaoService
{
    Task<IReadOnlyList<VendaComunicacaoDto>> ListAsync(Guid vendaId, CancellationToken ct = default);
    /// <summary>Sprint 453: histórico cliente — todas as comunicações em todas as reparações deste cliente.</summary>
    Task<IReadOnlyList<VendaComunicacaoDto>> ListByClienteAsync(Guid clienteId, int take, CancellationToken ct = default);
    Task<VendaComunicacaoDto> CreateAsync(Guid vendaId, CreateComunicacaoRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class VendaComunicacaoService : IVendaComunicacaoService
{
    private readonly IVendaComunicacaoRepository _repo;
    private readonly IVendaRepository _vendas;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;

    public VendaComunicacaoService(
        IVendaComunicacaoRepository repo,
        IVendaRepository vendas,
        ITenantContext tenant,
        ICurrentUser user,
        IAuditLogger audit)
    {
        _repo = repo;
        _vendas = vendas;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task<IReadOnlyList<VendaComunicacaoDto>> ListAsync(Guid vendaId, CancellationToken ct = default)
    {
        // Confirma que a reparação pertence ao tenant (filter global já valida; FindByIdAsync devolve null se não pertence).
        _ = await _vendas.FindByIdAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        var list = await _repo.ListByVendaAsync(vendaId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<VendaComunicacaoDto>> ListByClienteAsync(Guid clienteId, int take, CancellationToken ct = default)
    {
        // Tenant isolation: o IClienteRepository tem global query filter por tenant; FindByIdAsync devolve null cross-tenant.
        // Sem necessidade de ler o cliente — basta filter via clienteId no repo (que também tem filtro global).
        var list = await _repo.ListByClienteAsync(clienteId, take, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<VendaComunicacaoDto> CreateAsync(Guid vendaId, CreateComunicacaoRequest req, CancellationToken ct = default)
    {
        var texto = (req.Texto ?? "").Trim();
        if (texto.Length is < 1 or > 2000)
            throw new ValidationException("texto_invalido", "Texto obrigatório (1 a 2000 caracteres).");
        if (_user.UserId is not { } uid)
            throw new ValidationException("user_required", "Sessão sem utilizador associado.");

        var rep = await _vendas.FindByIdAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (rep.ClienteId is not { } clienteId)
            throw new ValidationException("sem_cliente", "Esta venda não tem cliente — associa um cliente para registar comunicações.");

        var entry = new VendaComunicacao
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId ?? Guid.Empty,
            VendaId = rep.Id,
            ClienteId = clienteId,
            Tipo = req.Tipo,
            Direcao = req.Direcao,
            Texto = texto,
            CreatedByUserId = uid,
        };
        await _repo.AddAsync(entry, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Create, "VendaComunicacao", entry.Id,
            new { entry.VendaId, tipo = entry.Tipo.ToString(), direcao = entry.Direcao.ToString() },
            entry.TenantId, uid, ct);

        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("VendaComunicacao", id);
        _repo.Remove(entry);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, "VendaComunicacao", id,
            new { entry.VendaId }, entry.TenantId, _user.UserId, ct);
    }

    private static VendaComunicacaoDto ToDto(VendaComunicacao c) => new(
        c.Id, c.VendaId, c.ClienteId, c.Tipo, c.Direcao, c.Texto, c.CreatedByUserId, c.CreatedAt);
}
