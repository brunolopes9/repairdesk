using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class Part : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string? Sku { get; set; }
    public required string Nome { get; set; }
    public PartCategoria Categoria { get; set; } = PartCategoria.Outro;
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public Guid? PriceTableEntryId { get; set; }
    public PriceTableEntry? PriceTableEntry { get; set; }
    public int QtdStock { get; set; }
    public int QtdMinima { get; set; }
    public int CustoUnitarioCents { get; set; }
    public string? Fornecedor { get; set; }
    public string? LocalArmazenamento { get; set; }
    public string? Notas { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>
    /// Sprint 121: quando true, expõe esta peça no endpoint /api/external/parts?lojaOnline=true
    /// para a loja online consumir e mostrar no catálogo público. Default false — peças
    /// internas (ex: charge boards, peças técnicas) não devem aparecer na loja.
    /// </summary>
    public bool MostrarLojaOnline { get; set; }
    public List<PartMovimento> Movimentos { get; set; } = new();
}
