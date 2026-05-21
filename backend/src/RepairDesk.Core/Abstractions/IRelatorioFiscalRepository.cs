namespace RepairDesk.Core.Abstractions;

public interface IRelatorioFiscalRepository
{
    Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    /// <summary>Limpa campos Invoice* da entity (Reparacao, Trabalho ou Venda) — usado pelo sync Moloni
    /// quando detecta que o documento foi anulado externamente no painel Moloni.</summary>
    Task ClearInvoiceFieldsAsync(string tipo, Guid entityId, CancellationToken ct = default);

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
}

public sealed record RelatorioFiscalDocumentoRow(
    Guid Id,
    string Tipo,
    int NumeroInterno,
    string? InvoiceNumber,
    string? InvoiceExternalId,
    DateTime InvoiceEmittedAt,
    string? ClienteNome,
    int ValorCents);
