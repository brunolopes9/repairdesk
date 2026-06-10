using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 543: bootstrap da categoria de Despesa por fornecedor conhecido — a primeira fatura da
/// Anthropic já chega pré-classificada como Software sem o Bruno ter ensinado nada. Lista CURTA e
/// só com casos inequívocos (Google fica fora: tanto é Workspace=Software como Ads=Marketing).
/// A regra aprendida na aprovação (Fornecedor.DefaultDespesaCategoria, last-wins) tem sempre
/// prioridade sobre isto — isto só preenche quando ainda não há regra.
/// </summary>
public static class KnownDespesaSuppliers
{
    private static readonly (string Token, DespesaCategoria Categoria)[] Known =
    {
        // SaaS / software / cloud
        ("anthropic", DespesaCategoria.Software),
        ("openai", DespesaCategoria.Software),
        ("github", DespesaCategoria.Software),
        ("vercel", DespesaCategoria.Software),
        ("cloudflare", DespesaCategoria.Software),
        ("hetzner", DespesaCategoria.Software),
        ("microsoft", DespesaCategoria.Software),
        ("adobe", DespesaCategoria.Software),
        ("moloni", DespesaCategoria.Software),
        // Telecom PT
        ("vodafone", DespesaCategoria.Comunicacoes),
        ("meo", DespesaCategoria.Comunicacoes),
        ("nos comunica", DespesaCategoria.Comunicacoes),
        ("digi", DespesaCategoria.Comunicacoes),
        // Transportadoras
        ("ctt", DespesaCategoria.Transporte),
        ("dhl", DespesaCategoria.Transporte),
        ("ups", DespesaCategoria.Transporte),
        ("gls", DespesaCategoria.Transporte),
        ("inpost", DespesaCategoria.Transporte),
        // Combustível
        ("galp", DespesaCategoria.Combustivel),
        ("bp ", DespesaCategoria.Combustivel),
        ("repsol", DespesaCategoria.Combustivel),
    };

    /// <summary>Categoria sugerida a partir do nome do fornecedor, ou null se desconhecido.</summary>
    public static DespesaCategoria? SuggestCategoria(string? fornecedorNome)
    {
        if (string.IsNullOrWhiteSpace(fornecedorNome)) return null;
        var nome = fornecedorNome.Trim().ToLowerInvariant();
        foreach (var (token, categoria) in Known)
        {
            // Tokens curtos/ambíguos (ex: "meo", "ctt") só por palavra inteira — senão "Romeo Lda"
            // ou "Doutor Pneu CTTuning" classificavam errado.
            var match = token.Length <= 4
                ? nome.Split(' ', ',', '-', '.').Contains(token.TrimEnd())
                : nome.Contains(token);
            if (match) return categoria;
        }
        return null;
    }
}
