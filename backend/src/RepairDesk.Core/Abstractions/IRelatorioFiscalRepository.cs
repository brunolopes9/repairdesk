using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

public interface IRelatorioFiscalRepository
{
    Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Sprint 513: versão rica de ListDocumentosAsync para o ecrã "Documentos / Faturas" (Vendas).
    /// Inclui PDF, NIF do cliente e provider — tudo o que a lista única de faturas precisa de mostrar.
    /// Agrega Reparações + Trabalhos + Vendas com fatura emitida no período.
    /// </summary>
    Task<IReadOnlyList<DocumentoVendaRow>> ListVendaDocumentosDetalheAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    /// <summary>Limpa campos Invoice* da entity (Reparacao, Trabalho ou Venda) — usado pelo sync Moloni
    /// quando detecta que o documento foi anulado externamente no painel Moloni.</summary>
    Task ClearInvoiceFieldsAsync(string tipo, Guid entityId, CancellationToken ct = default);

    /// <summary>Sprint 528: marca o recibo de liquidação na entity (Reparacao/Trabalho/Venda) cujo
    /// InvoiceExternalId corresponde. Permite esconder o botão "Emitir recibo" e mostrar o recibo na ficha.</summary>
    Task MarcarReciboEmitidoAsync(string invoiceExternalId, string reciboNumero, DateTime emitidoEm, CancellationToken ct = default);

    /// <summary>
    /// Sprint 159: soma custos brutos (com IVA) das peças do stock consumidas em reparações
    /// PAGAS no período. Bruno deduz IVA destes ao IVA liquidado para apurar IVA a entregar.
    /// </summary>
    Task<int> SumPecasCustoComIvaAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Sprint 159: soma custos brutos (com IVA) de Despesas imputadas a reparações/trabalhos
    /// PAGOS no período + despesas overhead (sem trabalho associado, ex: renda, internet).
    /// </summary>
    Task<int> SumDespesasComIvaAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Sprint 180: detalhe das compras de stock (PartMovimento Entrada + Despesas Peças/Material)
    /// para drill-down UI.
    /// </summary>
    Task<IReadOnlyList<IvaDeducaoLinha>> ListComprasStockAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Sprint 180: detalhe das despesas operacionais (Despesas excepto Peças/Material e excepto IsCogs)
    /// para drill-down UI.
    /// </summary>
    Task<IReadOnlyList<IvaDeducaoLinha>> ListDespesasOpExAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public sealed record IvaDeducaoLinha(
    DateTime Data,
    string Descricao,
    string? Fornecedor,
    string Origem,
    int ValorComIvaCents,
    int IvaCents);

public sealed record RelatorioFiscalDocumentoRow(
    Guid Id,
    string Tipo,
    int NumeroInterno,
    string? InvoiceNumber,
    string? InvoiceExternalId,
    DateTime InvoiceEmittedAt,
    string? ClienteNome,
    int ValorCents);

/// <summary>Sprint 513: linha rica de documento de venda emitido (fatura/simplificada) para a lista única.</summary>
public sealed record DocumentoVendaRow(
    Guid Id,
    string Origem,                 // "Venda" | "Reparacao" | "Trabalho"
    int NumeroInterno,
    string? InvoiceNumber,
    string? InvoiceExternalId,
    string? InvoicePdfUrl,
    BillingProvider Provider,
    DateTime InvoiceEmittedAt,
    Guid? ClienteId,
    string? ClienteNome,
    string? ClienteNif,
    int TotalCents,
    string? ReciboNumero = null,
    DateTime? ReciboEmitidoEm = null,
    // Sprint 529d: força o tipo do documento (ex.: "ORC" para orçamentos) em vez de o inferir do número.
    string? TipoOverride = null);
