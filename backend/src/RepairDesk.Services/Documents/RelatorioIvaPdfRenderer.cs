using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.Services.Documents;

public static class RelatorioIvaPdfRenderer
{
    private static readonly CultureInfo PtPt = new("pt-PT");

    public static byte[] Render(string tenantName, string? nif, RelatorioIvaResponse report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(9).FontColor("#18181b"));
                page.Header().Column(col =>
                {
                    col.Item().Text("Relatorio IVA trimestral").FontSize(20).Bold().FontColor("#0EA5E9");
                    col.Item().Text($"{tenantName}{(string.IsNullOrWhiteSpace(nif) ? "" : $" - NIF {nif}")}").FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"T{report.Trimestre} {report.Ano} - {report.PeriodoDe:dd/MM/yyyy} a {report.PeriodoAte.AddDays(-1):dd/MM/yyyy}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Row(row =>
                    {
                        Kpi(row.RelativeItem(), "Total sem IVA", report.TotalSemIvaCents);
                        Kpi(row.RelativeItem(), "IVA liquidado", report.IvaLiquidadoCents);
                        Kpi(row.RelativeItem(), "IVA a entregar", report.IvaAEntregarCents);
                    });
                    col.Item().Text("Documentos").FontSize(12).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(58);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            Header(h.Cell(), "Data");
                            Header(h.Cell(), "Tipo");
                            Header(h.Cell(), "Numero");
                            Header(h.Cell(), "Cliente");
                            Header(h.Cell(), "Base");
                            Header(h.Cell(), "IVA");
                            Header(h.Cell(), "Total");
                        });
                        foreach (var d in report.Documentos)
                        {
                            Cell(table.Cell(), d.Data.ToString("dd/MM/yyyy", PtPt));
                            Cell(table.Cell(), d.Tipo);
                            Cell(table.Cell(), d.NumeroDocumento);
                            Cell(table.Cell(), d.Cliente);
                            Money(table.Cell(), d.BaseCents);
                            Money(table.Cell(), d.IvaCents);
                            Money(table.Cell(), d.TotalCents);
                        }
                    });
                });
                page.Footer().AlignRight().Text($"Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static void Kpi(IContainer c, string label, int cents)
    {
        c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().Text(FormatMoney(cents)).FontSize(14).Bold();
        });
    }

    private static void Header(IContainer c, string text) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).Bold().FontSize(8);
    private static void Cell(IContainer c, string text) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(text).FontSize(8);
    private static void Money(IContainer c, int cents) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(FormatMoney(cents)).FontSize(8);
    private static string FormatMoney(int cents) => (cents / 100m).ToString("C", PtPt);
}
