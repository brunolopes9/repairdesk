namespace RepairDesk.Services.Relatorios;

public sealed record RelatorioIvaResponse(
    int Ano,
    int Trimestre,
    DateTime PeriodoDe,
    DateTime PeriodoAte,
    // === Vendas (IVA liquidado) ===
    int TotalSemIvaCents,                          // soma da base das faturas emitidas
    int IvaLiquidadoCents,                         // IVA cobrado ao cliente
    // === Compras dedutíveis ===
    /// <summary>Sprint 159: input manual Bruno (compras não registadas no RepairDesk).</summary>
    int IvaComprasCents,
    /// <summary>Sprint 159: IVA dedutível auto-calculado das peças do stock consumidas em reparações pagas.</summary>
    int IvaDedutivelPecasCents,
    /// <summary>Sprint 159: IVA dedutível auto-calculado das Despesas imputadas no período.</summary>
    int IvaDedutivelDespesasCents,
    /// <summary>Sprint 159: soma das 3 fontes de IVA dedutível.</summary>
    int IvaDedutivelTotalCents,
    // === A entregar ===
    int IvaAEntregarCents,                         // max(0, liquidado − dedutivelTotal)
    // === Comparação trimestre anterior ===
    int TrimestreAnteriorTotalSemIvaCents,
    int TrimestreAnteriorIvaLiquidadoCents,
    IReadOnlyList<RelatorioIvaDocumentoDto> Documentos,
    // Sprint 180: drill-down — Bruno consegue ver de onde vem cada KPI.
    IReadOnlyList<IvaDeducaoLinhaDto> ComprasStockDetalhe,
    IReadOnlyList<IvaDeducaoLinhaDto> DespesasOpExDetalhe,
    // === Regime da margem (bens em segunda mão, M13) ===
    // Sprint 535: IVA da margem (venda−compra), JÁ somado em IvaLiquidadoCents. As vendas em margem
    // saem a 0% nos docs Moloni, por isso este valor é calculado à parte e acrescentado ao liquidado.
    int IvaRegimeMargemCents = 0,
    // Nº de linhas de 2ª mão sem custo registado (sem Part) — não entraram na base; Bruno deve registar o custo.
    int RegimeMargemSemCustoCount = 0);

public sealed record RelatorioIvaDocumentoDto(
    Guid Id,
    string Tipo,
    int NumeroInterno,
    string NumeroDocumento,
    DateTime Data,
    string Cliente,
    int BaseCents,
    int IvaCents,
    int TotalCents);

/// <summary>Sprint 180: linha individual de IVA dedutível para drill-down do UI.</summary>
public sealed record IvaDeducaoLinhaDto(
    DateTime Data,
    string Descricao,
    string? Fornecedor,
    string Origem,           // 'stock-entrada' | 'despesa-pecas' | 'despesa-opex'
    int ValorComIvaCents,
    int IvaCents);
