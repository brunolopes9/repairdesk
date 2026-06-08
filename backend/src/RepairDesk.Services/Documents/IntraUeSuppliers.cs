using System.Linq;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 526: registo de fornecedores B2B intra-UE conhecidos (factos objectivos sobre as empresas,
/// não específico de um tenant). Usado ao auto-criar um Fornecedor a partir de um import de fatura
/// para marcar <see cref="Core.Entities.Fornecedor.IntraUe"/> por defeito — compras a estes são em
/// autoliquidação (IVA não dedutível em PT). O lojista pode sempre sobrepor no toggle do fornecedor.
///
/// Fase 2 (futuro): substituir/complementar isto por deteção do prefixo do VAT lido da própria fatura
/// (ex: "FR…", "NL…", "ES…" → intra-UE; "PT…" → nacional), tornando-o independente desta lista.
/// </summary>
public static class IntraUeSuppliers
{
    // Utopya (FR), Molano (NL/Haarlem) — os fornecedores estrangeiros recorrentes da LopesTech.
    private static readonly string[] Known = ["utopya", "molano"];

    /// <summary>True se o nome do fornecedor corresponder a um fornecedor intra-UE conhecido.</summary>
    public static bool IsKnownIntraUe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.ToLowerInvariant();
        return Known.Any(s => n.Contains(s));
    }
}
