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
    IReadOnlyList<RelatorioIvaDocumentoDto> Documentos);

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
