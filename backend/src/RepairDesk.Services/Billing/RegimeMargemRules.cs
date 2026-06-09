using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Billing;

/// <summary>
/// Sprint 534: Regime da margem de lucro — bens em segunda mão (CIVA art. 308.º; motivo de isenção
/// SAF-T <c>M13</c>). Aplica-se à venda de artigos USADOS/RECONDICIONADOS comprados a fonte sem IVA
/// dedutível (ex.: recondicionados Molano). A fatura ao cliente mostra o preço total SEM IVA
/// discriminado + a menção M13; o IVA incide só sobre a margem (venda − compra), declarado à parte
/// pelo vendedor na declaração periódica (não aparece na fatura).
/// </summary>
public static class RegimeMargemRules
{
    /// <summary>Código de motivo de isenção SAF-T: "Regime da margem de lucro — Bens em segunda mão".</summary>
    public const string ExemptionCodeM13 = "M13";

    /// <summary>
    /// True se a condição do artigo o qualifica como bem em segunda mão (regime da margem).
    /// Recondicionado/Usado = segunda mão; Novo/OpenBox/NãoAplicável = IVA normal.
    /// </summary>
    public static bool IsSegundaMao(CondicaoArtigo condicao)
        => condicao is CondicaoArtigo.Recondicionado or CondicaoArtigo.Usado;
}
