using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

/// <summary>
/// RGPD (portabilidade art. 20.º e apagamento art. 17.º): tudo o que pertence a um cliente.
/// Doc 94 Fase 4c: as reparações são Vendas do tipo Reparação — fotos, comunicações, garantias e
/// avaliações estão ligadas à Venda.
/// </summary>
public class ClienteRgpdRepository : IClienteRgpdRepository
{
    private readonly AppDbContext _db;

    public ClienteRgpdRepository(AppDbContext db) => _db = db;

    public async Task<ClienteRgpdData?> LoadClienteDataAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clienteId, ct);
        if (cliente is null) return null;

        var vendas = await _db.Vendas.AsNoTracking()
            .Include(v => v.Items)
            .Where(v => v.ClienteId == clienteId)
            .OrderBy(v => v.Numero)
            .ToListAsync(ct);
        var vendaIds = vendas.Select(v => v.Id).ToArray();

        var timeline = await _db.VendaEstadoLogs.AsNoTracking()
            .Where(t => vendaIds.Contains(t.VendaId)).OrderBy(t => t.MudouEm).ToListAsync(ct);
        var fotos = await _db.VendaFotos.AsNoTracking()
            .Where(f => vendaIds.Contains(f.VendaId)).OrderBy(f => f.CreatedAt).ToListAsync(ct);
        var comunicacoes = await _db.VendaComunicacoes.AsNoTracking()
            .Where(c => c.ClienteId == clienteId || vendaIds.Contains(c.VendaId)).OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var garantias = await _db.Garantias.AsNoTracking()
            .Where(g => g.VendaId != null && vendaIds.Contains(g.VendaId.Value)).OrderBy(g => g.DataInicio).ToListAsync(ct);
        var avaliacoes = await _db.Avaliacoes.AsNoTracking()
            .Where(a => vendaIds.Contains(a.VendaId)).OrderBy(a => a.CreatedAt).ToListAsync(ct);

        var relatedIds = RelatedIds(cliente.Id, vendas, timeline, fotos, comunicacoes, garantias, avaliacoes);
        var audit = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.EntityId != null && relatedIds.Contains(a.EntityId.Value))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return new ClienteRgpdData(cliente, vendas, timeline, fotos, comunicacoes, garantias, avaliacoes, audit);
    }

    public async Task HardDeleteAsync(ClienteRgpdData data, CancellationToken ct = default)
    {
        using var _ = _db.HardDeleteScope();

        var clienteId = data.Cliente.Id;
        var vendas = await _db.Vendas.Include(v => v.Items).Where(v => v.ClienteId == clienteId).ToListAsync(ct);
        var vendaIds = vendas.Select(v => v.Id).ToArray();

        var timeline = await _db.VendaEstadoLogs.Where(t => vendaIds.Contains(t.VendaId)).ToListAsync(ct);
        var fotos = await _db.VendaFotos.Where(f => vendaIds.Contains(f.VendaId)).ToListAsync(ct);
        var assinaturas = await _db.VendaAssinaturas.Where(a => vendaIds.Contains(a.VendaId)).ToListAsync(ct);
        var comunicacoes = await _db.VendaComunicacoes
            .Where(c => c.ClienteId == clienteId || vendaIds.Contains(c.VendaId)).ToListAsync(ct);
        var garantias = await _db.Garantias.Where(g => g.VendaId != null && vendaIds.Contains(g.VendaId.Value)).ToListAsync(ct);
        var avaliacoes = await _db.Avaliacoes.Where(a => vendaIds.Contains(a.VendaId)).ToListAsync(ct);
        var push = await _db.PushSubscriptions.Where(p => vendaIds.Contains(p.VendaId)).ToListAsync(ct);
        var cliente = await _db.Clientes.FirstAsync(c => c.Id == clienteId, ct);

        var relatedIds = RelatedIds(clienteId, vendas, timeline, fotos, comunicacoes, garantias, avaliacoes);
        var auditEntries = await _db.AuditEntries
            .Where(a => a.EntityId != null && relatedIds.Contains(a.EntityId.Value))
            .ToListAsync(ct);

        // Stock: unidades destas vendas voltam aos lotes (a venda deixa de existir).
        foreach (var item in vendas.Where(v => VendaConsomeStock(v)).SelectMany(v => v.Items).Where(i => i.CompraLinhaId != null))
        {
            var lote = await _db.ComprasLinhas.FirstOrDefaultAsync(l => l.Id == item.CompraLinhaId, ct);
            if (lote is not null) lote.QuantidadeVendida = Math.Max(0, lote.QuantidadeVendida - item.Quantidade);
        }

        _db.AuditEntries.RemoveRange(auditEntries);
        _db.PushSubscriptions.RemoveRange(push);
        _db.Avaliacoes.RemoveRange(avaliacoes);
        _db.Garantias.RemoveRange(garantias);
        _db.VendaComunicacoes.RemoveRange(comunicacoes);
        _db.VendaAssinaturas.RemoveRange(assinaturas);
        _db.VendaFotos.RemoveRange(fotos);
        _db.VendaEstadoLogs.RemoveRange(timeline);
        _db.VendaItems.RemoveRange(vendas.SelectMany(v => v.Items));
        _db.Vendas.RemoveRange(vendas);
        _db.Clientes.Remove(cliente);
        await _db.SaveChangesAsync(ct);
    }

    private static bool VendaConsomeStock(Venda v)
        => v.Estado is Core.Enums.VendaEstado.EmCurso or Core.Enums.VendaEstado.AEsperaPeca
            or Core.Enums.VendaEstado.Pronta or Core.Enums.VendaEstado.Entregue;

    private static HashSet<Guid> RelatedIds(
        Guid clienteId,
        IEnumerable<Venda> vendas,
        IEnumerable<VendaEstadoLog> timeline,
        IEnumerable<VendaFoto> fotos,
        IEnumerable<VendaComunicacao> comunicacoes,
        IEnumerable<Garantia> garantias,
        IEnumerable<Avaliacao> avaliacoes)
        => new HashSet<Guid>(
            new[] { clienteId }
                .Concat(vendas.Select(v => v.Id))
                .Concat(vendas.SelectMany(v => v.Items).Select(i => i.Id))
                .Concat(timeline.Select(t => t.Id))
                .Concat(fotos.Select(f => f.Id))
                .Concat(comunicacoes.Select(c => c.Id))
                .Concat(garantias.Select(g => g.Id))
                .Concat(avaliacoes.Select(a => a.Id)));
}
