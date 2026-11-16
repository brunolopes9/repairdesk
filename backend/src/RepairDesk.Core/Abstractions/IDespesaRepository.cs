using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 574: totais de TODAS as despesas do filtro (não só da página), por categoria.</summary>
public sealed record DespesaResumoCategoria(DespesaCategoria Categoria, int Registos, int TotalCents, int IvaCents, int SemIva, int Recorrentes);

public interface IDespesaRepository
{
    Task<Despesa?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Sprint 646: despesas desta empresa com estes números visíveis (com tracking, para editar).</summary>
    Task<IReadOnlyList<Despesa>> FindByNumerosAsync(IReadOnlyCollection<int> numeros, CancellationToken ct = default);
    Task<(IReadOnlyList<Despesa> Items, int Total)> SearchAsync(
        string? query,
        DespesaCategoria? categoria,
        IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports,
        bool excludeSupplierInvoiceImports,
        DateTime? from,
        DateTime? to,
        bool? isRecorrente,
        int page,
        int pageSize,
        CancellationToken ct = default,
        DespesaAmbito? ambito = null);
    Task<IReadOnlyList<DespesaResumoCategoria>> ResumoAsync(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to,
        bool? isRecorrente, CancellationToken ct = default, DespesaAmbito? ambito = null);
    /// <summary>Sprint 573: despesas operacionais (sem COGS) com data em [from, to).</summary>
    Task<IReadOnlyList<Despesa>> ListPeriodoAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task AddAsync(Despesa despesa, CancellationToken ct = default);
    /// <summary>Sprint 688: despesas que saíram da fatura de uma compra (fatura mista).</summary>
    Task<IReadOnlyList<Despesa>> ListByCompraAsync(Guid compraId, CancellationToken ct = default);
    /// <summary>Sprint 688: para cada despesa ligada a uma compra — o nº da compra e o PDF dela.</summary>
    Task<IReadOnlyDictionary<Guid, CompraLigada>> ComprasLigadasAsync(IReadOnlyCollection<Guid> despesaIds, CancellationToken ct = default);
    void Remove(Despesa despesa);
    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>Sprint 688: a compra de onde uma despesa saiu (fatura mista).</summary>
public sealed record CompraLigada(Guid CompraId, int Numero, Guid? SupplierInvoiceImportId);
