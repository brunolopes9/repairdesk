using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ClienteRgpdRepository : IClienteRgpdRepository
{
    private readonly AppDbContext _db;

    public ClienteRgpdRepository(AppDbContext db) => _db = db;

    public async Task<ClienteRgpdData?> LoadClienteDataAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clienteId, ct);
        if (cliente is null) return null;

        var reparacoes = await _db.Reparacoes.AsNoTracking()
            .Where(r => r.ClienteId == clienteId)
            .OrderBy(r => r.Numero)
            .ToListAsync(ct);
        var reparacaoIds = reparacoes.Select(r => r.Id).ToArray();

        var trabalhos = await _db.Trabalhos.AsNoTracking()
            .Where(t => t.ClienteId == clienteId)
            .OrderBy(t => t.Numero)
            .ToListAsync(ct);
        var trabalhoIds = trabalhos.Select(t => t.Id).ToArray();

        var despesas = await _db.Despesas.AsNoTracking()
            .Where(d =>
                (d.ReparacaoId != null && reparacaoIds.Contains(d.ReparacaoId.Value)) ||
                (d.TrabalhoId != null && trabalhoIds.Contains(d.TrabalhoId.Value)))
            .OrderBy(d => d.Data)
            .ToListAsync(ct);

        var timeline = await _db.ReparacaoEstadoLogs.AsNoTracking()
            .Where(t => reparacaoIds.Contains(t.ReparacaoId))
            .OrderBy(t => t.MudouEm)
            .ToListAsync(ct);

        var fotos = await _db.ReparacaoFotos.AsNoTracking()
            .Where(f => reparacaoIds.Contains(f.ReparacaoId))
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);

        var garantias = await _db.Garantias.AsNoTracking()
            .Where(g => (g.ReparacaoId != null && reparacaoIds.Contains(g.ReparacaoId.Value)))
            .OrderBy(g => g.DataInicio)
            .ToListAsync(ct);

        var avaliacoes = await _db.Avaliacoes.AsNoTracking()
            .Where(a => reparacaoIds.Contains(a.ReparacaoId))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        var vendas = await _db.Vendas.AsNoTracking()
            .Include(v => v.Items)
                .ThenInclude(i => i.Part)
            .Where(v => v.ClienteId == clienteId)
            .OrderBy(v => v.Numero)
            .ToListAsync(ct);
        var vendaIds = vendas.Select(v => v.Id).ToArray();

        var partMovimentos = await _db.PartMovimentos.AsNoTracking()
            .Include(m => m.Part)
            .Where(m =>
                (m.ReparacaoId != null && reparacaoIds.Contains(m.ReparacaoId.Value)) ||
                (m.VendaId != null && vendaIds.Contains(m.VendaId.Value)))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var relatedIds = new HashSet<Guid>(
            new[] { cliente.Id }
                .Concat(reparacaoIds)
                .Concat(trabalhoIds)
                .Concat(despesas.Select(d => d.Id))
                .Concat(timeline.Select(t => t.Id))
                .Concat(fotos.Select(f => f.Id))
                .Concat(garantias.Select(g => g.Id))
                .Concat(avaliacoes.Select(a => a.Id))
                .Concat(partMovimentos.Select(m => m.Id))
                .Concat(vendaIds)
                .Concat(vendas.SelectMany(v => v.Items).Select(i => i.Id)));

        var audit = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.EntityId != null && relatedIds.Contains(a.EntityId.Value))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return new ClienteRgpdData(cliente, reparacoes, timeline, trabalhos, despesas, fotos, garantias, avaliacoes, partMovimentos, vendas, audit);
    }

    public async Task HardDeleteAsync(ClienteRgpdData data, CancellationToken ct = default)
    {
        using var _ = _db.HardDeleteScope();

        var reparacaoIds = data.Reparacoes.Select(r => r.Id).ToArray();
        var trabalhoIds = data.Trabalhos.Select(t => t.Id).ToArray();

        var vendas = await _db.Vendas.Include(v => v.Items)
            .Where(v => v.ClienteId == data.Cliente.Id).ToListAsync(ct);
        var vendaIds = vendas.Select(v => v.Id).ToArray();
        var vendaItems = vendas.SelectMany(v => v.Items).ToList();

        var fotos = await _db.ReparacaoFotos.Where(f => reparacaoIds.Contains(f.ReparacaoId)).ToListAsync(ct);
        var partMovimentos = await _db.PartMovimentos.Where(m =>
            (m.ReparacaoId != null && reparacaoIds.Contains(m.ReparacaoId.Value)) ||
            (m.VendaId != null && vendaIds.Contains(m.VendaId.Value))).ToListAsync(ct);
        var despesas = await _db.Despesas.Where(d =>
            (d.ReparacaoId != null && reparacaoIds.Contains(d.ReparacaoId.Value)) ||
            (d.TrabalhoId != null && trabalhoIds.Contains(d.TrabalhoId.Value))).ToListAsync(ct);
        var garantias = await _db.Garantias.Where(g =>
            (g.ReparacaoId != null && reparacaoIds.Contains(g.ReparacaoId.Value)) ||
            (g.VendaId != null && vendaIds.Contains(g.VendaId.Value))).ToListAsync(ct);
        var avaliacoes = await _db.Avaliacoes.Where(a => reparacaoIds.Contains(a.ReparacaoId)).ToListAsync(ct);
        var timeline = await _db.ReparacaoEstadoLogs.Where(t => reparacaoIds.Contains(t.ReparacaoId)).ToListAsync(ct);
        var reparacoes = await _db.Reparacoes.Where(r => r.ClienteId == data.Cliente.Id).ToListAsync(ct);
        var trabalhos = await _db.Trabalhos.Where(t => t.ClienteId == data.Cliente.Id).ToListAsync(ct);
        var cliente = await _db.Clientes.FirstAsync(c => c.Id == data.Cliente.Id, ct);

        var relatedIds = new HashSet<Guid>(
            new[] { cliente.Id }
                .Concat(reparacoes.Select(r => r.Id))
                .Concat(trabalhos.Select(t => t.Id))
                .Concat(despesas.Select(d => d.Id))
                .Concat(timeline.Select(t => t.Id))
                .Concat(fotos.Select(f => f.Id))
                .Concat(garantias.Select(g => g.Id))
                .Concat(avaliacoes.Select(a => a.Id))
                .Concat(partMovimentos.Select(m => m.Id))
                .Concat(vendaIds)
                .Concat(vendaItems.Select(i => i.Id)));
        var auditEntries = await _db.AuditEntries
            .Where(a => a.EntityId != null && relatedIds.Contains(a.EntityId.Value))
            .ToListAsync(ct);

        _db.AuditEntries.RemoveRange(auditEntries);
        _db.PartMovimentos.RemoveRange(partMovimentos);
        _db.Despesas.RemoveRange(despesas);
        _db.ReparacaoFotos.RemoveRange(fotos);
        _db.Garantias.RemoveRange(garantias);
        _db.Avaliacoes.RemoveRange(avaliacoes);
        _db.ReparacaoEstadoLogs.RemoveRange(timeline);
        _db.VendaItems.RemoveRange(vendaItems);
        _db.Vendas.RemoveRange(vendas);
        _db.Reparacoes.RemoveRange(reparacoes);
        _db.Trabalhos.RemoveRange(trabalhos);
        _db.Clientes.Remove(cliente);
        await _db.SaveChangesAsync(ct);
    }
}
