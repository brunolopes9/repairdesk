using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Encomendas;

public sealed record ItemParaEncomendarDto(Guid Id, string Descricao, int Quantidade, string? Fornecedor, string? Nota, int Estado,
    DateTime CriadoEm, DateTime? EncomendadoEm, DateTime? RecebidoEm);

public sealed record ItemParaEncomendarRequest(string Descricao, int Quantidade = 1, string? Fornecedor = null, string? Nota = null);

public sealed record EstadoItemRequest(int Estado);

public interface IListaEncomendasService
{
    /// <summary>Por encomendar e a caminho; os recebidos dos últimos 7 dias também (para desfazer um engano).</summary>
    Task<IReadOnlyList<ItemParaEncomendarDto>> ListarAsync(CancellationToken ct = default);
    Task<ItemParaEncomendarDto> CriarAsync(ItemParaEncomendarRequest req, CancellationToken ct = default);
    Task<ItemParaEncomendarDto> AtualizarAsync(Guid id, ItemParaEncomendarRequest req, CancellationToken ct = default);
    Task<ItemParaEncomendarDto> MudarEstadoAsync(Guid id, EstadoItemRequest req, CancellationToken ct = default);
    Task ApagarAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Sprint 687: lista de compras da oficina (o que encomendar, fora das reparações).</summary>
public sealed class ListaEncomendasService : IListaEncomendasService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ListaEncomendasService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ItemParaEncomendarDto>> ListarAsync(CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow.AddDays(-7);
        return await _db.ItensParaEncomendar.AsNoTracking()
            .Where(i => i.Estado < 2 || i.RecebidoEm >= limite)
            .OrderBy(i => i.Estado).ThenBy(i => i.Fornecedor).ThenByDescending(i => i.CreatedAt)
            .Select(i => new ItemParaEncomendarDto(i.Id, i.Descricao, i.Quantidade, i.Fornecedor, i.Nota, i.Estado, i.CreatedAt, i.EncomendadoEm, i.RecebidoEm))
            .ToListAsync(ct);
    }

    public async Task<ItemParaEncomendarDto> CriarAsync(ItemParaEncomendarRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new ForbiddenException("tenant_required", "Sem empresa.");
        var item = new ItemParaEncomendar { TenantId = tenantId, Descricao = "" };
        Aplicar(item, req);
        _db.ItensParaEncomendar.Add(item);
        await _db.SaveChangesAsync(ct);
        return Dto(item);
    }

    public async Task<ItemParaEncomendarDto> AtualizarAsync(Guid id, ItemParaEncomendarRequest req, CancellationToken ct = default)
    {
        var item = await Load(id, ct);
        Aplicar(item, req);
        await _db.SaveChangesAsync(ct);
        return Dto(item);
    }

    public async Task<ItemParaEncomendarDto> MudarEstadoAsync(Guid id, EstadoItemRequest req, CancellationToken ct = default)
    {
        if (req.Estado is < 0 or > 2) throw new ValidationException("estado_invalido", "Estado: 0 por encomendar, 1 encomendado, 2 recebido.");
        var item = await Load(id, ct);
        item.Estado = req.Estado;
        item.EncomendadoEm = req.Estado >= 1 ? item.EncomendadoEm ?? DateTime.UtcNow : null;
        item.RecebidoEm = req.Estado == 2 ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(ct);
        return Dto(item);
    }

    public async Task ApagarAsync(Guid id, CancellationToken ct = default)
    {
        var item = await Load(id, ct);
        _db.ItensParaEncomendar.Remove(item);
        await _db.SaveChangesAsync(ct);
    }

    private static void Aplicar(ItemParaEncomendar item, ItemParaEncomendarRequest req)
    {
        var desc = (req.Descricao ?? "").Trim();
        if (desc.Length is 0 or > 200) throw new ValidationException("descricao_invalida", "Escreve o que é preciso encomendar (até 200 caracteres).");
        if (req.Quantidade is < 1 or > 999) throw new ValidationException("quantidade_invalida", "Quantidade entre 1 e 999.");
        item.Descricao = desc;
        item.Quantidade = req.Quantidade;
        item.Fornecedor = string.IsNullOrWhiteSpace(req.Fornecedor) ? null : req.Fornecedor.Trim()[..Math.Min(120, req.Fornecedor.Trim().Length)];
        item.Nota = string.IsNullOrWhiteSpace(req.Nota) ? null : req.Nota.Trim()[..Math.Min(500, req.Nota.Trim().Length)];
    }

    private async Task<ItemParaEncomendar> Load(Guid id, CancellationToken ct)
        => await _db.ItensParaEncomendar.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("ItemParaEncomendar", id);

    private static ItemParaEncomendarDto Dto(ItemParaEncomendar i)
        => new(i.Id, i.Descricao, i.Quantidade, i.Fornecedor, i.Nota, i.Estado, i.CreatedAt, i.EncomendadoEm, i.RecebidoEm);
}
