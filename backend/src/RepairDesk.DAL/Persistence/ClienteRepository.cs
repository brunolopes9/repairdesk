using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ClienteRepository : IClienteRepository
{
    private readonly AppDbContext _db;

    public ClienteRepository(AppDbContext db) => _db = db;

    public Task<Cliente?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ClienteEquipamentoRow>> ListEquipamentosAsync(
        Guid clienteId,
        int take,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);

        var reparacoes = await _db.Reparacoes
            .AsNoTracking()
            .Where(r => r.ClienteId == clienteId)
            .Select(r => new ClienteEquipamentoSource(
                r.Equipamento,
                r.Imei,
                r.CreatedAt,
                r.Id,
                r.Numero,
                null,
                null))
            .ToListAsync(ct);

        var vendaItems = await _db.VendaItems
            .AsNoTracking()
            .Where(i => i.Venda != null && i.Venda.ClienteId == clienteId && (i.Imei != null || i.Imei2 != null))
            .Select(i => new
            {
                i.Descricao,
                i.Imei,
                i.Imei2,
                Data = i.Venda!.Data,
                VendaId = (Guid?)i.VendaId,
                VendaNumero = (int?)i.Venda!.Numero,
            })
            .ToListAsync(ct);

        var sources = new List<ClienteEquipamentoSource>(reparacoes);
        foreach (var item in vendaItems)
        {
            if (!string.IsNullOrWhiteSpace(item.Imei))
            {
                sources.Add(new ClienteEquipamentoSource(
                    item.Descricao,
                    item.Imei,
                    item.Data,
                    null,
                    null,
                    item.VendaId,
                    item.VendaNumero));
            }

            if (!string.IsNullOrWhiteSpace(item.Imei2))
            {
                sources.Add(new ClienteEquipamentoSource(
                    item.Descricao,
                    item.Imei2,
                    item.Data,
                    null,
                    null,
                    item.VendaId,
                    item.VendaNumero));
            }
        }

        return sources
            .Where(x => !string.IsNullOrWhiteSpace(x.Nome) || !string.IsNullOrWhiteSpace(x.Imei))
            .GroupBy(EquipamentoKey, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var ordered = g.OrderByDescending(x => x.Data).ToList();
                var latest = ordered[0];
                var latestRepair = ordered.FirstOrDefault(x => x.ReparacaoId.HasValue);
                var latestSale = ordered.FirstOrDefault(x => x.VendaId.HasValue);

                return new ClienteEquipamentoRow(
                    Nome: FirstNonBlank(ordered.Select(x => x.Nome)) ?? "Equipamento sem nome",
                    Imei: FirstNonBlank(ordered.Select(x => x.Imei)),
                    PrimeiroRegistoEm: g.Min(x => x.Data),
                    UltimoRegistoEm: latest.Data,
                    ReparacoesCount: g.Where(x => x.ReparacaoId.HasValue).Select(x => x.ReparacaoId!.Value).Distinct().Count(),
                    VendasCount: g.Where(x => x.VendaId.HasValue).Select(x => x.VendaId!.Value).Distinct().Count(),
                    UltimaReparacaoId: latestRepair?.ReparacaoId,
                    UltimaReparacaoNumero: latestRepair?.ReparacaoNumero,
                    UltimaVendaId: latestSale?.VendaId,
                    UltimaVendaNumero: latestSale?.VendaNumero);
            })
            .OrderByDescending(x => x.UltimoRegistoEm)
            .ThenBy(x => x.Nome)
            .Take(take)
            .ToList();
    }

    public Task<bool> NifExistsAsync(string nif, Guid? exceptId = null, CancellationToken ct = default)
        => _db.Clientes.AnyAsync(c => c.Nif == nif && (exceptId == null || c.Id != exceptId), ct);

    public Task<Cliente?> FindByNifAsync(string nif, CancellationToken ct = default)
        => _db.Clientes.FirstOrDefaultAsync(c => c.Nif == nif, ct);

    public Task<Cliente?> FindByTelefoneAsync(string telefoneNormalizado, CancellationToken ct = default)
        => _db.Clientes.FirstOrDefaultAsync(c => c.Telefone == telefoneNormalizado, ct);

    public async Task<(IReadOnlyList<Cliente> Items, int Total)> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Clientes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(c =>
                EF.Functions.Like(c.Nome, like) ||
                EF.Functions.Like(c.Telefone, like) ||
                (c.Email != null && EF.Functions.Like(c.Email, like)) ||
                (c.Nif != null && EF.Functions.Like(c.Nif, like)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(c => c.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _db.Clientes.AsNoTracking().AnyAsync(ct);

    public async Task<IReadOnlyList<Cliente>> ExportAllAsync(CancellationToken ct = default)
        => await _db.Clientes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync(ct);

    public Task AddAsync(Cliente cliente, CancellationToken ct = default) => _db.Clientes.AddAsync(cliente, ct).AsTask();
    public void Remove(Cliente cliente) => _db.Clientes.Remove(cliente);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private static string EquipamentoKey(ClienteEquipamentoSource row)
    {
        var imei = row.Imei?.Trim();
        if (!string.IsNullOrWhiteSpace(imei)) return $"imei:{imei}";
        return $"nome:{row.Nome.Trim()}";
    }

    private static string? FirstNonBlank(IEnumerable<string?> values)
        => values.Select(x => x?.Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private sealed record ClienteEquipamentoSource(
        string Nome,
        string? Imei,
        DateTime Data,
        Guid? ReparacaoId,
        int? ReparacaoNumero,
        Guid? VendaId,
        int? VendaNumero);
}
