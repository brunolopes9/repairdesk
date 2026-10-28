namespace RepairDesk.Services.Fiscal;

/// <summary>
/// Sprint 575: estimativas de IRS (regime simplificado) e Segurança Social de trabalhador independente.
/// São ESTIMATIVAS para ter noção dos valores — não substituem o contabilista nem a liquidação das Finanças
/// (Sprint 580: entram o IRS Jovem e as deduções à coleta indicadas; não entram o mínimo de existência nem outros
/// rendimentos do agregado).
/// As taxas mudam todos os anos: atualizar <see cref="EscaloesIrs"/> quando sair o Orçamento do Estado.
/// </summary>
public static class EstimativasFiscais
{
    /// <summary>Coeficientes do art. 31.º CIRS (regime simplificado).</summary>
    public const decimal CoefBens = 0.15m;
    /// <summary>Atividades da tabela do art. 151.º (ex.: programação/informática) — também usado no trading, por prudência.</summary>
    public const decimal CoefServicosTabela = 0.75m;
    /// <summary>Outras prestações de serviços (ex.: reparações).</summary>
    public const decimal CoefOutrosServicos = 0.35m;
    /// <summary>Regra dos 15% (art. 31.º n.º 13): nos rendimentos a 0,75 têm de se justificar despesas de 15%.</summary>
    public const decimal Regra15 = 0.15m;
    /// <summary>Dedução específica que conta para a regra dos 15% (sem rendimentos da categoria A).</summary>
    public const decimal DeducaoEspecifica = 4104m;

    /// <summary>Segurança Social: rendimento relevante = 70% dos serviços + 20% das vendas de bens; taxa 21,4%.</summary>
    public const decimal SsServicos = 0.70m;
    public const decimal SsBens = 0.20m;
    public const decimal SsTaxa = 0.214m;

    /// <summary>
    /// Redução dos coeficientes de serviços no ano de início de atividade (50%) e no seguinte (25%) — art. 31.º n.º 10.
    /// Devolve o fator a aplicar ao coeficiente (1 = sem redução).
    /// </summary>
    public static decimal FatorInicioAtividade(int ano, DateTime inicio)
        => inicio.Year == ano ? 0.50m : inicio.Year == ano - 1 ? 0.75m : 1m;

    /// <summary>Escalões de IRS (limite superior, taxa marginal). Tabela de 2026 — confirmar com o OE de cada ano.</summary>
    private static readonly (decimal Ate, decimal Taxa)[] Escaloes2026 =
    [
        (8_342m, 0.125m),
        (12_587m, 0.157m),
        (17_838m, 0.212m),
        (23_089m, 0.241m),
        (29_397m, 0.311m),
        (43_090m, 0.349m),
        (46_566m, 0.431m),
        (86_634m, 0.446m),
        (decimal.MaxValue, 0.48m),
    ];

    /// <summary>Indexante dos Apoios Sociais de 2026 — o IRS Jovem isenta no máximo 55 × IAS por ano.</summary>
    public const decimal Ias2026 = 537.13m;
    public const decimal IrsJovemLimiteIas = 55m;

    /// <summary>
    /// Sprint 580: % isenta pelo IRS Jovem (art. 12.º-B CIRS) conforme o ano de rendimentos em que o titular está:
    /// 1.º = 100%, 2.º–4.º = 75%, 5.º–7.º = 50%, 8.º–10.º = 25%, depois 0.
    /// </summary>
    public static decimal IrsJovemPercentagem(int? anoIrsJovem) => anoIrsJovem switch
    {
        1 => 1.00m,
        >= 2 and <= 4 => 0.75m,
        >= 5 and <= 7 => 0.50m,
        >= 8 and <= 10 => 0.25m,
        _ => 0m,
    };

    /// <summary>Parte do rendimento isenta pelo IRS Jovem (com o limite de 55 × IAS).</summary>
    public static decimal IrsJovemIsencao(decimal rendimento, int? anoIrsJovem)
        => Math.Min(Math.Max(0m, rendimento) * IrsJovemPercentagem(anoIrsJovem), IrsJovemLimiteIas * Ias2026);

    public static int AnoTabela(int ano) => 2026;

    public static IReadOnlyList<(decimal Ate, decimal Taxa)> EscaloesIrs(int ano) => Escaloes2026;

    /// <summary>IRS por escalões progressivos (cada parte do rendimento à taxa do seu escalão).</summary>
    public static decimal Irs(decimal rendimentoColetavel, int ano)
    {
        if (rendimentoColetavel <= 0) return 0m;
        decimal imposto = 0, anterior = 0;
        foreach (var (ate, taxa) in EscaloesIrs(ano))
        {
            var parte = Math.Min(rendimentoColetavel, ate) - anterior;
            if (parte <= 0) break;
            imposto += parte * taxa;
            anterior = ate;
        }
        return imposto;
    }
}
