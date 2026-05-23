using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class PartRepository : IPartRepository
{
    private readonly AppDbContext _db;

    public PartRepository(AppDbContext db) => _db = db;

    public Task<Part?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Parts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Part?> FindByIdWithMovimentosAsync(Guid id, CancellationToken ct = default)
        => _db.Parts
            .Include(p => p.Movimentos.OrderByDescending(m => m.CreatedAt))
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Part?> FindBySkuAsync(string sku, CancellationToken ct = default)
        => _db.Parts.FirstOrDefaultAsync(p => p.Sku == sku, ct);

    public Task<bool> SkuExistsAsync(string sku, Guid? exceptId = null, CancellationToken ct = default)
        => _db.Parts.AnyAsync(p => p.Sku == sku && (exceptId == null || p.Id != exceptId), ct);

    public async Task<(IReadOnlyList<Part> Items, int Total)> SearchAsync(
        string? query,
        PartCategoria? categoria,
        string? marca,
        bool lowStockOnly,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.Parts.AsNoTracking().AsQueryable();

        if (categoria is not null) q = q.Where(p => p.Categoria == categoria.Value);
        if (!string.IsNullOrWhiteSpace(marca)) q = q.Where(p => p.Marca == marca);
        // Sprint 139: qtdMinima=0 = "não me incomodes com esta peça" — fica fora do filtro.
        if (lowStockOnly) q = q.Where(p => p.QtdMinima > 0 && p.QtdStock <= p.QtdMinima);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim()}%";
            q = q.Where(p =>
                EF.Functions.Like(p.Nome, like) ||
                (p.Sku != null && EF.Functions.Like(p.Sku, like)) ||
                (p.Marca != null && EF.Functions.Like(p.Marca, like)) ||
                (p.Modelo != null && EF.Functions.Like(p.Modelo, like)) ||
                (p.Fornecedor != null && EF.Functions.Like(p.Fornecedor, like)) ||
                (p.LocalArmazenamento != null && EF.Functions.Like(p.LocalArmazenamento, like)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(p => p.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<Part>> LowStockAsync(CancellationToken ct = default)
        // Sprint 139: qtdMinima=0 desliga alerta — usado em peças one-shot.
        => await _db.Parts
            .AsNoTracking()
            .Where(p => p.Activo && p.QtdMinima > 0 && p.QtdStock <= p.QtdMinima)
            .OrderBy(p => p.QtdStock - p.QtdMinima)
            .ThenBy(p => p.Nome)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> MarcasAsync(CancellationToken ct = default)
        => await _db.Parts
            .AsNoTracking()
            .Where(p => p.Marca != null && p.Marca != "")
            .Select(p => p.Marca!)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(ct);

    public Task AddAsync(Part part, CancellationToken ct = default)
        => _db.Parts.AddAsync(part, ct).AsTask();

    public void Remove(Part part) => _db.Parts.Remove(part);

    public void AddMovimento(PartMovimento movimento) => _db.PartMovimentos.Add(movimento);

    public async Task<IReadOnlyList<PartMovimento>> MovimentosAsync(Guid? partId, Guid? reparacaoId, CancellationToken ct = default)
    {
        var q = _db.PartMovimentos.AsNoTracking().Include(m => m.Part).AsQueryable();
        if (partId is not null) q = q.Where(m => m.PartId == partId.Value);
        if (reparacaoId is not null) q = q.Where(m => m.ReparacaoId == reparacaoId.Value);
        return await q.OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
    }

    public async Task<int> SumCustoByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var movimentos = await _db.PartMovimentos
            .AsNoTracking()
            .Include(m => m.Part)
            .Where(m => m.ReparacaoId == reparacaoId)
            .ToListAsync(ct);

        return movimentos.Sum(m => -(m.Quantidade) * (m.Part?.CustoUnitarioCents ?? 0));
    }

    public async Task<IReadOnlyList<ReabastecerSugestao>> ReabastecerSugestoesAsync(int days, CancellationToken ct = default)
    {
        // Sprint 208: consumo NET dos últimos N dias excluindo reparações soft-deleted.
        // Bruno reportou: peça aparecia 2/30d mas só usou 1× — outra rep tinha sido apagada.
        // Sprint 198 fixou Uso vs Devolução; este adiciona filtro IsDeleted=false.
        var since = DateTime.UtcNow.AddDays(-days);
        var consumo = await _db.PartMovimentos
            .AsNoTracking()
            .Where(m => m.CreatedAt >= since
                && (m.Motivo == Core.Enums.PartMovimentoMotivo.UsoEmReparacao
                 || m.Motivo == Core.Enums.PartMovimentoMotivo.Devolucao)
                && m.Part != null
                && m.Part.Activo
                // Sprint 208: ignora movimentos ligados a reparações apagadas.
                // Global filter de IsDeleted no AppDbContext aplica em Include/Navigation, mas
                // aqui consultamos PartMovimentos directo sem Include — filtro explícito.
                && (m.ReparacaoId == null
                    || _db.Reparacoes.Any(r => r.Id == m.ReparacaoId && !r.IsDeleted)))
            .GroupBy(m => new { m.PartId, m.Part!.Sku, m.Part.Nome, m.Part.QtdStock, m.Part.CustoUnitarioCents })
            .Select(g => new
            {
                g.Key.PartId,
                g.Key.Sku,
                g.Key.Nome,
                g.Key.QtdStock,
                g.Key.CustoUnitarioCents,
                // Uso é negativo, Devolução é positivo → -Sum dá consumo líquido positivo.
                Consumo = -g.Sum(m => m.Quantidade),
            })
            .ToListAsync(ct);

        // Filtra: stock <= consumo (risco de ruptura no mesmo período).
        return consumo
            .Where(c => c.Consumo > 0 && c.QtdStock <= c.Consumo)
            .Select(c => new ReabastecerSugestao(
                c.PartId,
                c.Sku ?? string.Empty,
                c.Nome,
                c.QtdStock,
                c.Consumo,
                c.Consumo > 0 ? (int)Math.Round(c.QtdStock / (double)c.Consumo * days) : 0,
                c.CustoUnitarioCents))
            .OrderBy(c => c.DiasRestantesEstimados)
            .ToList();
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
