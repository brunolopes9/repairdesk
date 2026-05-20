using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Products;

/// <summary>
/// Sprint 146: mapping <see cref="ProductGrading"/> interno → labels canónicos da loja.
///
/// A loja headless (lopestech-shop) usa A+ / A / B+ / B / C+ / C como canonical values + labels PT
/// user-friendly. O RepairDesk tem o enum internal com Novo/GradeA-C/OpenBox/Premium (Sprint 122).
///
/// Solução: NÃO mudar o enum (preserva dados existentes); adicionar mapping no External API + SDK.
/// A loja consome `gradeCanonical` (machine-readable, estável) + `gradeLabel` (display).
///
/// Mapping decidido com o outro Claude (loja side):
/// <code>
/// Novo       → "Novo"    "Novo"
/// Premium    → "A+"      "Como novo"
/// GradeA     → "A"       "Excelente"
/// GradeB     → "B"       "Bom"
/// GradeC     → "C"       "Aceitável"
/// OpenBox    → "OpenBox" "Open Box"
/// </code>
///
/// A loja pode mapear localmente para os tiers que mostra (ex: A+/A → "Como novo"). RepairDesk
/// não tem B+ nem C+ como grades distintos hoje — Bruno avalia se precisa de expandir no futuro
/// (seria migration grande, fica fora do scope desta sprint).
/// </summary>
public static class ProductGradingMapper
{
    public static string ToCanonical(ProductGrading grading) => grading switch
    {
        ProductGrading.Novo => "Novo",
        ProductGrading.Premium => "A+",
        ProductGrading.GradeA => "A",
        ProductGrading.GradeB => "B",
        ProductGrading.GradeC => "C",
        ProductGrading.OpenBox => "OpenBox",
        _ => grading.ToString(),
    };

    public static string ToLabelPt(ProductGrading grading) => grading switch
    {
        ProductGrading.Novo => "Novo",
        ProductGrading.Premium => "Como novo",
        ProductGrading.GradeA => "Excelente",
        ProductGrading.GradeB => "Bom",
        ProductGrading.GradeC => "Aceitável",
        ProductGrading.OpenBox => "Open Box",
        _ => grading.ToString(),
    };

    /// <summary>
    /// Espelhar para <see cref="CondicaoArtigo"/> também — a loja usa 3 tiers "new/used/refurbished"
    /// que mapeiam aos 5 valores internos do RepairDesk.
    /// </summary>
    public static string CondicaoCanonical(CondicaoArtigo c) => c switch
    {
        CondicaoArtigo.Novo => "new",
        CondicaoArtigo.OpenBox => "refurbished",
        CondicaoArtigo.Recondicionado => "refurbished",
        CondicaoArtigo.Usado => "used",
        _ => "n/a",
    };
}
