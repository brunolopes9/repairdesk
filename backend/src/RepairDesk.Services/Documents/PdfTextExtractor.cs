using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 119: extrai texto plain de um PDF (encomendas de fornecedor, faturas recebidas,
/// orçamentos). Sem parsing inteligente — devolve apenas o texto bruto para o utilizador
/// confirmar campos na UI. Parser específico por fornecedor (Tudo4Mobile, Molano, etc)
/// fica para sprints futuros.
/// </summary>
public static class PdfTextExtractor
{
    /// <summary>Hard limit — protege contra PDFs maliciosos com milhares de páginas.</summary>
    public const int MaxPagesToRead = 30;
    public const int MaxBytes = 10 * 1024 * 1024; // 10 MB

    public static PdfExtractionResult Extract(Stream pdfStream, string filename)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        using var doc = PdfDocument.Open(pdfStream);
        var pages = new List<string>();
        var pagesRead = 0;
        foreach (var page in doc.GetPages())
        {
            if (pagesRead >= MaxPagesToRead) break;
            pages.Add(ContentOrderTextExtractor.GetText(page));
            pagesRead++;
        }

        var text = string.Join("\n\n", pages).Trim();
        return new PdfExtractionResult(
            Filename: filename,
            Text: text,
            PageCount: doc.NumberOfPages,
            PagesRead: pagesRead,
            Truncated: doc.NumberOfPages > MaxPagesToRead);
    }
}

public sealed record PdfExtractionResult(
    string Filename,
    string Text,
    int PageCount,
    int PagesRead,
    bool Truncated);
