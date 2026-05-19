namespace RepairDesk.Core.Abstractions;

public interface IRelatorioFiscalRepository
{
    Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    /// <summary>Limpa campos Invoice* da entity (Reparacao, Trabalho ou Venda) — usado pelo sync Moloni
    /// quando detecta que o documento foi anulado externamente no painel Moloni.</summary>
    Task ClearInvoiceFieldsAsync(string tipo, Guid entityId, CancellationToken ct = default);
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
