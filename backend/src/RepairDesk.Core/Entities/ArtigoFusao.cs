using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 581: registo de "juntar artigos" — o que passou da origem para o destino, para se poder desfazer
/// (a origem fica apagada com soft delete e volta a existir com os seus lotes e códigos).
/// </summary>
public class ArtigoFusao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DestinoId { get; set; }
    public Guid OrigemId { get; set; }
    public required string OrigemSku { get; set; }
    public required string OrigemNome { get; set; }
    /// <summary>JSON: ids dos lotes que passaram para o destino.</summary>
    public required string LoteIdsJson { get; set; }
    /// <summary>JSON: ids dos códigos (SKUs de fornecedor/alternativos) da origem que passaram para o destino.</summary>
    public required string CodigosMovidosJson { get; set; }
    /// <summary>JSON: ids dos códigos alternativos criados no destino com as etiquetas da origem.</summary>
    public required string CodigosCriadosJson { get; set; }
    public DateTime? DesfeitaEm { get; set; }
}
