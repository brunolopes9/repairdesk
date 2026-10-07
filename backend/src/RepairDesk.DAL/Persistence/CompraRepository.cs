using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class CompraRepository : ICompraRepository
{
    private readonly AppDbContext _db;
    public CompraRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<CompraDocumento> Items, int Total)> SearchAsync(CompraFiltro filtro, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ComprasDocumentos.AsNoTracking().Include(d => d.Fornecedor).Include(d => d.Linhas).AsQueryable();

        if (filtro.FornecedorId is { } fid) q = q.Where(d => d.FornecedorId == fid);
        if (filtro.SoFaturaEmFalta) q = q.Where(d => d.NumeroFatura == null || d.NumeroFatura == "");
        if (filtro.DeUtc is { } de) q = q.Where(d => d.Data >= de);
        if (filtro.AteUtc is { } ate) q = q.Where(d => d.Data < ate);
        if (!string.IsNullOrWhiteSpace(filtro.Query))
        {
            var term = filtro.Query.Trim();
            q = q.Where(d =>
                (d.NumeroFatura != null && d.NumeroFatura.Contains(term)) ||
                (d.NumerosEncomenda != null && d.NumerosEncomenda.Contains(term)) ||
                d.Fornecedor!.Name.Contains(term) ||
                d.Linhas.Any(l => l.Descricao.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.Data).ThenByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<CompraDocumento?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ComprasDocumentos
            .Include(d => d.Fornecedor)
            .Include(d => d.Linhas)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<CompraDocumento>> FindPossiveisDuplicadosAsync(
        Guid fornecedorId, string? numeroFatura, IReadOnlyCollection<string> encomendas, Guid? excluirId, CancellationToken ct = default)
    {
        var candidatos = await _db.ComprasDocumentos.AsNoTracking()
            .Where(d => d.FornecedorId == fornecedorId && (excluirId == null || d.Id != excluirId))
            .Where(d => (numeroFatura != null && d.NumeroFatura != null && d.NumeroFatura.ToUpper() == numeroFatura.ToUpper()) || d.NumerosEncomenda != null)
            .ToListAsync(ct);

        // A comparação de encomendas é feita em memória: a coluna guarda uma lista separada por vírgulas.
        return candidatos.Where(d =>
                (numeroFatura != null && string.Equals(d.NumeroFatura, numeroFatura, StringComparison.OrdinalIgnoreCase)) ||
                (d.NumerosEncomenda ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Any(e => encomendas.Contains(e, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<IReadOnlyList<CompraLinha>> ListLinhasAsync(bool soComStock, CancellationToken ct = default)
    {
        var q = _db.ComprasLinhas.AsNoTracking().Include(l => l.Documento).ThenInclude(d => d!.Fornecedor).AsQueryable();
        if (soComStock) q = q.Where(l => l.Quantidade - l.QuantidadeVendida - l.QuantidadeAbatida > 0);
        return await q.OrderBy(l => l.Documento!.Data).ThenBy(l => l.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompraDocumento>> ListDocumentosAsync(CancellationToken ct = default)
        => await _db.ComprasDocumentos.AsNoTracking().Include(d => d.Fornecedor).ToListAsync(ct);

    public Task AddAsync(CompraDocumento doc, CancellationToken ct = default) => _db.ComprasDocumentos.AddAsync(doc, ct).AsTask();
    public void Remove(CompraDocumento doc) => _db.ComprasDocumentos.Remove(doc);
    public void AddLinha(CompraLinha linha) => _db.ComprasLinhas.Add(linha);
    public void RemoveLinha(CompraLinha linha) => _db.ComprasLinhas.Remove(linha);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
