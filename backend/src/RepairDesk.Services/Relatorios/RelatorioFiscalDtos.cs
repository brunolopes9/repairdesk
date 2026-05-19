namespace RepairDesk.Services.Relatorios;

public sealed record RelatorioIvaResponse(
    int Ano,
    int Trimestre,
    DateTime PeriodoDe,
    DateTime PeriodoAte,
    int TotalSemIvaCents,
    int IvaLiquidadoCents,
    int IvaComprasCents,
    int IvaAEntregarCents,
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
