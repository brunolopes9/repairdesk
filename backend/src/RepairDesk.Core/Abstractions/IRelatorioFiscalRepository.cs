namespace RepairDesk.Core.Abstractions;

public interface IRelatorioFiscalRepository
{
    Task<IReadOnlyList<RelatorioFiscalDocumentoRow>> ListDocumentosAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public sealed record RelatorioFiscalDocumentoRow(
    Guid Id,
    string Tipo,
    int NumeroInterno,
    string? InvoiceNumber,
    DateTime InvoiceEmittedAt,
    string? ClienteNome,
    int ValorCents);
