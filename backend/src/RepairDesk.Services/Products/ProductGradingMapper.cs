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

    // ===== Sprint 197: 2D Origin + Grade =====

    /// <summary>Sprint 197: schema.org canonical (NewCondition / UsedCondition / RefurbishedCondition).</summary>
    public static string OriginCanonical(ProductOrigin o) => o switch
    {
        ProductOrigin.New => "new",
        ProductOrigin.Used => "used",
        ProductOrigin.Refurbished => "refurbished",
        _ => "n/a",
    };

    public static string OriginLabelPt(ProductOrigin o) => o switch
    {
        ProductOrigin.New => "Novo",
        ProductOrigin.Used => "Usado original",
        ProductOrigin.Refurbished => "Recondicionado",
        _ => o.ToString(),
    };

    public static string GradeCanonical(ProductGrade g) => g switch
    {
        ProductGrade.Sealed => "sealed",
        ProductGrade.APlusPlus => "A++",
        ProductGrade.APlus => "A+",
        ProductGrade.A => "A",
        ProductGrade.BPlus => "B+",
        ProductGrade.B => "B",
        ProductGrade.CPlus => "C+",
        ProductGrade.C => "C",
        _ => g.ToString(),
    };

    /// <summary>Sprint 197: labels alinhadas com Molano grading guide (buymolano.com) — todos garantem
    /// bateria 80%+. A++ é específico Bruno (open-box premium 100% bateria).</summary>
    public static string GradeLabelPt(ProductGrade g) => g switch
    {
        ProductGrade.Sealed => "Selado",
        ProductGrade.APlusPlus => "A++ · Como novo (open-box 100% bateria)",
        ProductGrade.APlus => "A+ · Como novo (vestígio quase impercetível)",
        ProductGrade.A => "A · Excelente (ligeira descoloração possível)",
        ProductGrade.BPlus => "B+ · Muito bom (vestígios menores, max 3)",
        ProductGrade.B => "B · Bom (vestígios menores, max 5)",
        ProductGrade.CPlus => "C+ · Razoável (riscos profundos / amolgadelas)",
        ProductGrade.C => "C · Aceitável (desgaste significativo)",
        _ => g.ToString(),
    };

    /// <summary>Sprint 197: label combinado para SEO prompt + UI. "Novo (selado)", "Usado original A++", "Recondicionado B".</summary>
    public static string ComposedLabelPt(ProductOrigin origin, ProductGrade grade)
    {
        if (origin == ProductOrigin.New && grade == ProductGrade.Sealed) return "Novo (selado)";
        return $"{OriginLabelPt(origin)} {GradeCanonical(grade)}";
    }

    /// <summary>
    /// Sprint 197: deriva o ProductGrading legacy para back-compat (webhook envia ambos durante
    /// transição). Heurística aproximada — não é bijectivo.
    /// </summary>
    public static ProductGrading ComposeLegacy(ProductOrigin origin, ProductGrade grade) => (origin, grade) switch
    {
        (ProductOrigin.New, ProductGrade.Sealed) => ProductGrading.Novo,
        (ProductOrigin.New, _) => ProductGrading.OpenBox,
        (ProductOrigin.Used, ProductGrade.APlusPlus) => ProductGrading.OpenBox,
        (ProductOrigin.Used, ProductGrade.APlus) => ProductGrading.Premium,
        (ProductOrigin.Used, ProductGrade.A) => ProductGrading.GradeA,
        (ProductOrigin.Used, ProductGrade.BPlus or ProductGrade.B) => ProductGrading.GradeB,
        (ProductOrigin.Used, ProductGrade.CPlus or ProductGrade.C) => ProductGrading.GradeC,
        (ProductOrigin.Refurbished, ProductGrade.APlusPlus or ProductGrade.APlus or ProductGrade.A) => ProductGrading.GradeA,
        (ProductOrigin.Refurbished, ProductGrade.BPlus or ProductGrade.B) => ProductGrading.GradeB,
        (ProductOrigin.Refurbished, ProductGrade.CPlus or ProductGrade.C) => ProductGrading.GradeC,
        _ => ProductGrading.Novo,
    };
}
