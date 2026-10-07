using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Compras;

/// <summary>Linha de compra (= lote de stock) com todos os valores calculados por unidade e totais.</summary>
public sealed record CompraLinhaDto(
    Guid Id,
    string Descricao,
    int Quantidade,
    int QuantidadeVendida,
    int QuantidadeAbatida,
    int QuantidadeEmStock,
    decimal PrecoUnitarioPago,
    decimal TaxaIvaCompra,
    decimal LucroUnitario,
    string? Localizacao,
    // Por unidade (motor de IVA, SPEC §3).
    decimal CustoSemIva,
    decimal IvaPagoNaCompra,
    decimal AutoliquidacaoUe,
    decimal LucroComIva,
    decimal PrecoVendaSemIva,
    decimal IvaDaVenda,
    decimal PrecoFinalComIva,
    decimal IvaAPagarEstado,
    // Totais da linha.
    decimal TotalPago,
    decimal TotalSemIva);

public sealed record CompraDocumentoDto(
    Guid Id,
    Guid FornecedorId,
    string FornecedorNome,
    RegimeIvaFornecedor RegimeIva,
    DateTime Data,
    string? NumeroFatura,
    IReadOnlyList<string> NumerosEncomenda,
    string? MetodoPagamento,
    decimal PortesPagos,
    decimal? PortesIva,
    decimal? TotalDocumento,
    string? Notas,
    bool FaturaEmFalta,
    Guid? SupplierInvoiceImportId,
    int Unidades,
    int UnidadesEmStock,
    decimal TotalLinhasPago,
    decimal TotalCalculado,
    decimal? Diferenca,
    bool TemDiferenca,
    IReadOnlyList<CompraLinhaDto> Linhas);

public sealed record CompraLinhaWriteRequest(
    Guid? Id,
    string Descricao,
    int Quantidade,
    decimal PrecoUnitarioPago,
    // Null → taxa do regime do fornecedor (23% nacional, 0% UE).
    decimal? TaxaIvaCompra,
    // Null → 1 € películas/vidros, 5 € resto (FiscalDefaults).
    decimal? LucroUnitario,
    string? Localizacao);

public sealed record CompraDocumentoWriteRequest(
    Guid FornecedorId,
    DateTime Data,
    string? NumeroFatura,
    IReadOnlyList<string>? NumerosEncomenda,
    string? MetodoPagamento,
    decimal PortesPagos,
    decimal? PortesIva,
    decimal? TotalDocumento,
    string? Notas,
    IReadOnlyList<CompraLinhaWriteRequest> Linhas,
    // true = gravar mesmo havendo um documento com o mesmo nº de fatura/encomenda.
    bool IgnorarDuplicado = false);

public sealed record CompraDuplicadoDto(Guid Id, DateTime Data, string? NumeroFatura, string? NumerosEncomenda);

/// <summary>Linha de inventário (lote com stock &gt; 0) — SPEC §4.5.</summary>
public sealed record InventarioLinhaDto(
    Guid LinhaId,
    Guid DocumentoId,
    string Fornecedor,
    DateTime Data,
    string Referencia,
    bool FaturaEmFalta,
    string Descricao,
    string? Localizacao,
    int QuantidadeEmStock,
    decimal PrecoUnitarioPago,
    decimal TotalPago,
    decimal TotalSemIva,
    decimal PrecoFinalComIva,
    // Totais do que está em stock (quantidade × por unidade).
    decimal Lucro,
    decimal IvaAPagarEstado,
    decimal TaxaIvaCompra);

public sealed record ResumoColunaDto(
    int Unidades,
    decimal ValorCobradoComIva,
    decimal ValorPecasPago,
    decimal IvaDaVenda,
    decimal IvaJaPagoFornecedores,
    decimal IvaAPagarEstado,
    decimal IvaAPagarNacionais,
    decimal IvaAPagarUe,
    decimal Lucro);

public sealed record ResumoFornecedorDto(
    Guid FornecedorId,
    string Nome,
    RegimeIvaFornecedor RegimeIva,
    int Documentos,
    decimal TotalPecas,
    decimal IvaPecas,
    decimal Portes,
    decimal TotalGasto,
    int FaturasEmFalta);

public sealed record ResumoComprasDto(
    decimal TaxaIvaVenda,
    ResumoColunaDto JaVendido,
    ResumoColunaDto EmStock,
    decimal AutoliquidacaoUeDeclarada,
    decimal IvaComprasNacionais,
    decimal IvaPortesNacionais,
    decimal PortesPagos,
    int DocumentosComFaturaEmFalta,
    int DocumentosComDiferenca,
    IReadOnlyList<ResumoFornecedorDto> PorFornecedor);

public sealed record SimuladorRequest(decimal PrecoPago, RegimeIvaFornecedor Regime, decimal Lucro, decimal? PrecoVendaComIva);

public sealed record SimuladorResponse(
    decimal CustoSemIva,
    decimal IvaPagoNaCompra,
    decimal AutoliquidacaoUe,
    decimal PrecoVendaSemIva,
    decimal IvaDaVenda,
    decimal PrecoFinalComIva,
    decimal IvaAPagarEstado,
    decimal LucroQueSobra,
    // Preenchidos quando se simula um preço de venda escolhido.
    decimal? VendaIvaAPagar,
    decimal? VendaLucro);
