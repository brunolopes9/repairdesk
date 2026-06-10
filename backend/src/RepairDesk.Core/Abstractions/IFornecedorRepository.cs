using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IFornecedorRepository
{
    Task<IReadOnlyList<Fornecedor>> ListByTenantAsync(bool includeInactive, CancellationToken ct = default);
    Task<Fornecedor?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Fornecedor?> FindByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Fornecedor f, CancellationToken ct = default);
    void Remove(Fornecedor f);
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Sprint 548 (Doc 93 #3): histórico consolidado de UM fornecedor — o "Histórico de
    /// Fornecedores" do Moloni, mas com tudo o que o Mender sabe: compras de stock (entradas
    /// × custo, match por Part.Fornecedor — string snapshot, como o Top Fornecedores do
    /// Negócio), despesas, importações de faturas (com as últimas N), última compra e taxa
    /// de defeito a 12 meses (IMEIs vendidos deste fornecedor que voltaram em reparação).
    /// Null quando o fornecedor não existe no tenant.
    /// </summary>
    Task<FornecedorHistorico?> GetHistoricoAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Sprint 548: snapshot consolidado de um fornecedor.</summary>
public sealed record FornecedorHistorico(
    Guid Id,
    string Nome,
    bool IntraUe,
    string DefaultImportAction,
    int? DefaultDespesaCategoria,
    int? GarantiaB2BDiasDefault,
    long ComprasStockCents,
    long DespesasCents,
    int ImportsTotal,
    int ImportsPendentes,
    DateTime? UltimaCompraEm,
    int ItensVendidos12m,
    int ItensComReparacao12m,
    decimal TaxaDefeitoPct12m,
    IReadOnlyList<FornecedorFaturaResumo> UltimasFaturas);

/// <summary>Sprint 548: linha resumida de uma fatura importada deste fornecedor.</summary>
public sealed record FornecedorFaturaResumo(
    Guid ImportId,
    string? Numero,
    DateTime? Data,
    int? TotalCents,
    string Status);
