namespace RepairDesk.Services.Billing;

/// <summary>
/// Sprint 136: constrói as linhas de Orçamento/Fatura Moloni discriminando peças
/// (cada Part usada na reparação) + 1 linha de "Mão-de-obra" com o resto até ao total.
///
/// Bruno escolheu Opção A (Sprint 135 feedback): peça aparece ao **custo de aquisição** ao
/// fornecedor (ex: 46€ por ecrã que pagou no Tudo4Mobile com IVA+portes); o restante até ao
/// preço final é mão-de-obra (ex: 70€ - 46€ = 24€). Máxima transparência ao cliente.
/// </summary>
public static class ReparacaoBillingItemsBuilder
{
    /// <summary>Cada peça consumida na reparação (Uso menos Devoluções).</summary>
    public sealed record UsedPart(string Name, int Quantity, int UnitCostCents);

    /// <summary>
    /// Devolve a lista de linhas para o Moloni `products` array, OU <c>null</c> se não há
    /// peças válidas ou se a mão-de-obra calculada seria negativa (peças custaram mais que o
    /// orçamento). O caller usa null como sinal para fazer fallback à linha sintética antiga.
    /// </summary>
    public static IReadOnlyList<MoloniInvoiceDraftItem>? Build(
        string equipamento,
        IEnumerable<UsedPart> usedParts,
        int totalCents,
        decimal vatPercent)
    {
        if (totalCents <= 0) return null;

        var items = new List<MoloniInvoiceDraftItem>();
        var subtotal = 0;
        foreach (var p in usedParts)
        {
            if (p.Quantity <= 0 || p.UnitCostCents <= 0) continue;
            subtotal += p.Quantity * p.UnitCostCents;
            items.Add(new MoloniInvoiceDraftItem(
                Name: Truncate(p.Name, maxLen: 80),
                Summary: null,
                Quantity: p.Quantity,
                UnitPriceCents: p.UnitCostCents,
                DiscountCents: 0,
                VatPercent: vatPercent));
        }

        if (items.Count == 0) return null;          // sem peças válidas → fallback
        var maoDeObra = totalCents - subtotal;
        if (maoDeObra < 0) return null;             // peças > orçamento → ambíguo, fallback
        if (maoDeObra > 0)
        {
            items.Add(new MoloniInvoiceDraftItem(
                Name: Truncate($"Mão-de-obra · {equipamento}", maxLen: 80),
                Summary: null,
                Quantity: 1,
                UnitPriceCents: maoDeObra,
                DiscountCents: 0,
                VatPercent: vatPercent));
        }
        // maoDeObra == 0: só peças, sem trabalho explícito. Não adiciona linha vazia.
        return items;
    }

    private static string Truncate(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        return s.Length <= maxLen ? s : s[..(maxLen - 1)].TrimEnd() + "…";
    }
}
