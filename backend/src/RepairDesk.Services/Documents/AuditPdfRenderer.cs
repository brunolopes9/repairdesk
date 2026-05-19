using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Services.Audit;

namespace RepairDesk.Services.Documents;

public static class AuditPdfRenderer
{
    private static readonly CultureInfo PtPt = new("pt-PT");

    public static byte[] Render(IReadOnlyList<AuditEntryDto> rows, int total, DateTime? from, DateTime? to)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(9).FontColor("#18181b"));
                page.Header().Column(col =>
                {
                    col.Item().Text("Relatorio de auditoria").FontSize(20).Bold().FontColor("#0EA5E9");
                    var periodo = $"{(from is null ? "inicio" : from.Value.ToString("dd/MM/yyyy", PtPt))} a {(to is null ? "agora" : to.Value.ToString("dd/MM/yyyy", PtPt))}";
                    col.Item().Text($"{total} eventos filtrados - {periodo}").FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(2.2f);
                    });
                    table.Header(h =>
                    {
                        Header(h.Cell(), "Quando");
                        Header(h.Cell(), "Quem");
                        Header(h.Cell(), "Acao");
                        Header(h.Cell(), "Entidade");
                        Header(h.Cell(), "Detalhe");
                    });
                    foreach (var row in rows)
                    {
                        Cell(table.Cell(), row.CreatedAt.ToString("dd/MM HH:mm", PtPt));
                        Cell(table.Cell(), row.AppUserDisplayName ?? row.AppUserEmail ?? row.AppUserId?.ToString() ?? "Sistema");
                        Cell(table.Cell(), row.Action.ToString());
                        Cell(table.Cell(), row.EntityType);
                        Cell(table.Cell(), Truncate(row.ChangesJson ?? row.EntityId?.ToString() ?? "", 140));
                    }
                });
                page.Footer().AlignRight().Text($"Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer c, string text) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).Bold().FontSize(8);
    private static void Cell(IContainer c, string text) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(text).FontSize(8);
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...";
}
