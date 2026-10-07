using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Documento de compra a um fornecedor — uma fatura (ou, enquanto a fatura não chega, a encomenda).
/// Cada <see cref="CompraLinha"/> é um lote de stock com o seu custo e IVA de compra.
/// SPEC compras §2.2. Valores monetários em euros com 4 casas decimais (arredonda-se só na apresentação).
/// </summary>
public class CompraDocumento : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Nº sequencial por tenant, visível na UI ("Compra 13"). Atribuído ao gravar; nunca reutilizado.</summary>
    public int Numero { get; set; }

    public Guid FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    /// <summary>Data da fatura (ou da encomenda, se ainda não há fatura).</summary>
    public DateTime Data { get; set; }

    /// <summary>Nº da fatura do fornecedor. Null = fatura em falta (só há encomenda/resumo).</summary>
    public string? NumeroFatura { get; set; }

    /// <summary>
    /// Nº(s) de encomenda no site do fornecedor, normalizados e separados por vírgula
    /// (uma fatura pode juntar várias encomendas — ex.: "165048,165227").
    /// </summary>
    public string? NumerosEncomenda { get; set; }

    public string? MetodoPagamento { get; set; }

    /// <summary>Portes pagos (valor bruto, como saiu da conta).</summary>
    public decimal PortesPagos { get; set; }

    /// <summary>IVA incluído nos portes que é dedutível (nacional). UE = 0. Null = desconhecido.</summary>
    public decimal? PortesIva { get; set; }

    /// <summary>Total impresso no documento — serve para reconciliar com a soma das linhas + portes.</summary>
    public decimal? TotalDocumento { get; set; }

    public string? Notas { get; set; }

    /// <summary>Importação (PDF lido pela IA / email) que originou este documento, se existir.</summary>
    public Guid? SupplierInvoiceImportId { get; set; }

    public List<CompraLinha> Linhas { get; set; } = new();

    public bool FaturaEmFalta => string.IsNullOrWhiteSpace(NumeroFatura);
}
