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
            // "13" encontra a compra nº 13 ou a que tem o lote nº 13.
            var numero = int.TryParse(term.TrimStart('#'), out var n) ? n : -1;
            q = q.Where(d =>
                d.Numero == numero || d.Linhas.Any(l => l.Numero == numero) ||
                (d.NumeroFatura != null && d.NumeroFatura.Contains(term)) ||
                (d.NumerosEncomenda != null && d.NumerosEncomenda.Contains(term)) ||
                d.Fornecedor!.Name.Contains(term) ||
                d.Linhas.Any(l => l.Descricao.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(d => d.Data).ThenByDescending(d => d.Numero)
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
        return await q.OrderBy(l => l.Documento!.Data).ThenBy(l => l.Numero).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompraDocumento>> ListDocumentosAsync(CancellationToken ct = default)
        => await _db.ComprasDocumentos.AsNoTracking().Include(d => d.Fornecedor).ToListAsync(ct);

    public async Task<IReadOnlyList<CompraDocumento>> FindCandidatosFaturaAsync(
        Guid fornecedorId, string? numero, DateTime de, DateTime ate, CancellationToken ct = default)
    {
        var num = numero?.Trim().ToUpper();
        var candidatos = await _db.ComprasDocumentos
            .Include(d => d.Fornecedor)
            .Include(d => d.Linhas)
            .Where(d => d.FornecedorId == fornecedorId)
            .Where(d =>
                (num != null && d.NumeroFatura != null && d.NumeroFatura.ToUpper() == num) ||
                (num != null && d.NumerosEncomenda != null && d.NumerosEncomenda.Contains(num)) ||
                ((d.NumeroFatura == null || d.NumeroFatura == "") && d.Data >= de && d.Data <= ate))
            .AsSplitQuery()
            .ToListAsync(ct);

        // A coluna de encomendas é uma lista separada por vírgulas: confirma o nº exato em memória.
        return candidatos.Where(d =>
                (num != null && string.Equals(d.NumeroFatura, num, StringComparison.OrdinalIgnoreCase)) ||
                (num != null && (d.NumerosEncomenda ?? string.Empty).Split(',').Any(e => string.Equals(e, num, StringComparison.OrdinalIgnoreCase))) ||
                (string.IsNullOrWhiteSpace(d.NumeroFatura) && d.Data >= de && d.Data <= ate))
            .ToList();
    }

    public Task AddAsync(CompraDocumento doc, CancellationToken ct = default) => _db.ComprasDocumentos.AddAsync(doc, ct).AsTask();
    public void Remove(CompraDocumento doc) => _db.ComprasDocumentos.Remove(doc);
    public async Task<IReadOnlyList<CompraLinha>> FindLinhasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await _db.ComprasLinhas.Include(l => l.Documento).ThenInclude(d => d!.Fornecedor)
            .Where(l => ids.Contains(l.Id)).ToListAsync(ct);

    public void AddLinha(CompraLinha linha) => _db.ComprasLinhas.Add(linha);
    public void RemoveLinha(CompraLinha linha) => _db.ComprasLinhas.Remove(linha);
    /// <summary>
    /// Grava e atribui o nº visível (por tenant) a documentos e lotes novos. Dois pedidos em
    /// simultâneo podem calcular o mesmo nº: o índice único rejeita um deles e tenta-se outra vez.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var atribuidos = await AtribuirNumerosAsync(ct);
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts && atribuidos.Count > 0)
            {
                foreach (var reset in atribuidos) reset();
            }
        }
    }

    private async Task<List<Action>> AtribuirNumerosAsync(CancellationToken ct)
    {
        var resets = new List<Action>();

        var docs = _db.ChangeTracker.Entries<CompraDocumento>()
            .Where(e => e.State == EntityState.Added && e.Entity.Numero == 0).Select(e => e.Entity).ToList();
        foreach (var grupo in docs.GroupBy(d => d.TenantId))
        {
            var max = await _db.ComprasDocumentos.IgnoreQueryFilters()
                .Where(d => d.TenantId == grupo.Key).Select(d => (int?)d.Numero).MaxAsync(ct) ?? 0;
            foreach (var d in grupo.OrderBy(d => d.Data))
            {
                d.Numero = ++max;
                resets.Add(() => d.Numero = 0);
            }
        }

        var linhas = _db.ChangeTracker.Entries<CompraLinha>()
            .Where(e => e.State == EntityState.Added && e.Entity.Numero == 0).Select(e => e.Entity).ToList();
        foreach (var grupo in linhas.GroupBy(l => l.TenantId))
        {
            var max = await _db.ComprasLinhas.IgnoreQueryFilters()
                .Where(l => l.TenantId == grupo.Key).Select(l => (int?)l.Numero).MaxAsync(ct) ?? 0;
            foreach (var l in grupo.OrderBy(l => l.Documento?.Data ?? DateTime.MaxValue))
            {
                l.Numero = ++max;
                resets.Add(() => l.Numero = 0);
            }
        }
        return resets;
    }
}
