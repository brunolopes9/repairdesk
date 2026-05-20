using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class VendaItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VendaId { get; set; }
    public Venda? Venda { get; set; }

    public Guid? PartId { get; set; }
    public Part? Part { get; set; }

    public required string Descricao { get; set; }
    public int Quantidade { get; set; }
    public int PrecoUnitarioCents { get; set; }
    public int DescontoCents { get; set; }
    public decimal IvaRate { get; set; }

    /// <summary>IMEI principal (15 dígitos) quando o artigo é um telemóvel. Validado com Luhn.</summary>
    public string? Imei { get; set; }
    /// <summary>IMEI secundário para equipamentos dual-SIM.</summary>
    public string? Imei2 { get; set; }

    /// <summary>
    /// Fornecedor B2B do artigo (texto livre — entity Fornecedor será promovida quando volume justificar).
    /// Snapshot no momento da venda. UI sugere autocomplete com fornecedores já usados pelo tenant.
    /// </summary>
    public string? FornecedorNome { get; set; }

    /// <summary>Condição do artigo. Snapshot — Part.CondicaoDefault pode mudar depois.</summary>
    public CondicaoArtigo Condicao { get; set; } = CondicaoArtigo.NaoAplicavel;

    /// <summary>
    /// Data até quando o fornecedor cobre garantia B2B (ex: Molano open-box 60d → DataVenda + 60).
    /// Útil em reparações em garantia: se está dentro, RMA ao fornecedor (€0 a teu cargo);
    /// se está fora, absorves o custo. Ver reference_garantia_imei_nc_pt em memory.
    /// </summary>
    public DateTime? GarantiaFornecedorAteAo { get; set; }

    public int TotalCents => Math.Max(0, Quantidade * PrecoUnitarioCents - DescontoCents);
}
