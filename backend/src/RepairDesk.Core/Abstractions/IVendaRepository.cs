using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IVendaRepository
{
    Task<Venda?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Venda?> FindByIdWithItemsAsync(Guid id, CancellationToken ct = default);
    Task CreateWithNextNumeroAsync(Venda venda, Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<Venda> Items, int Total)> SearchAsync(VendaFiltro filtro, int page, int pageSize, CancellationToken ct = default);
    Task<VendaImeiLookupRow?> FindVendaByImeiAsync(string imei, CancellationToken ct = default);
    Task<int> SumPaidBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<TopVendaItemRow>> TopItemsByRevenueAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
    /// <summary>Distinct FornecedorNome usados pelo tenant — para autocomplete UI Vendas.</summary>
    Task<IReadOnlyList<string>> ListDistinctFornecedoresAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>Filtros da lista de Vendas. Datas sobre <c>Venda.Data</c> (data da entrega/venda).</summary>
public sealed record VendaFiltro(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? ClienteId = null,
    VendaTipo? Tipo = null,
    VendaEstado? Estado = null,
    /// <summary>true = só as em curso (Orçamento, À espera de peça, Pronta).</summary>
    bool EmCurso = false,
    /// <summary>true = Entregues sem nº de fatura registado.</summary>
    bool FaturaPorRegistar = false,
    string? Q = null);

public sealed record TopVendaItemRow(
    Guid? PartId,
    string Descricao,
    int Quantidade,
    int TotalCents);

public sealed record VendaImeiLookupRow(
    Guid VendaId,
    int Numero,
    DateTime Data,
    string Descricao,
    string? ClienteNome,
    string? FornecedorNome = null,
    int Condicao = 0,
    DateTime? GarantiaFornecedorAteAo = null);
