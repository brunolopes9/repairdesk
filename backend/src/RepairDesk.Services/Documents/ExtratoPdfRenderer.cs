using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Services.Documentos;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 542: PDF do Extrato unificado (Vendas + Compras + Despesas por data) — o documento que o
/// Bruno entrega ao contabilista. Segue o estilo do RelatorioIvaPdfRenderer.
/// </summary>
public static class ExtratoPdfRenderer
{
    private static readonly CultureInfo PtPt = new("pt-PT");

    public static byte[] Render(
        string tenantName,
        string? nif,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<DocumentoDto> vendas,
        IReadOnlyList<IvaDeducaoLinha> compras,
        IReadOnlyList<IvaDeducaoLinha> despesas,
        ExtratoTotais totais)
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
                    col.Item().Text("Extrato unificado").FontSize(20).Bold().FontColor("#0EA5E9");
                    col.Item().Text($"{tenantName}{(string.IsNullOrWhiteSpace(nif) ? "" : $" - NIF {nif}")}").FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text($"{fromUtc:dd/MM/yyyy} a {toUtc.AddDays(-1):dd/MM/yyyy} — Vendas · Compras · Despesas").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Row(row =>
                    {
                        Kpi(row.RelativeItem(), "Faturado (c/ IVA)", totais.FaturadoCents);
                        Kpi(row.RelativeItem(), "Compras stock", totais.ComprasCents);
                        Kpi(row.RelativeItem(), "Despesas OpEx", totais.DespesasCents);
                        Kpi(row.RelativeItem(), "Resultado simples", totais.ResultadoCents);
                    });

                    // === VENDAS (lista única: local + Moloni) ===
                    col.Item().Text($"Vendas — documentos emitidos ({vendas.Count})").FontSize(12).Bold();
                    if (vendas.Count == 0)
                    {
                        col.Item().Text("Sem documentos no período.").FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(58);   // data
                                c.ConstantColumn(28);   // tipo
                                c.RelativeColumn(1.3f); // numero
                                c.RelativeColumn(2);    // cliente
                                c.RelativeColumn(0.9f); // estado
                                c.RelativeColumn(1);    // base
                                c.RelativeColumn(0.8f); // iva
                                c.RelativeColumn(1);    // total
                            });
                            table.Header(h =>
                            {
                                Header(h.Cell(), "Data");
                                Header(h.Cell(), "Tipo");
                                Header(h.Cell(), "Numero");
                                Header(h.Cell(), "Cliente");
                                Header(h.Cell(), "Estado");
                                Header(h.Cell(), "Base");
                                Header(h.Cell(), "IVA");
                                Header(h.Cell(), "Total");
                            });
                            foreach (var d in vendas.OrderBy(v => v.Data))
                            {
                                var sinal = d.TipoCodigo == "NC" ? -1 : 1;
                                Cell(table.Cell(), d.Data.ToString("dd/MM/yyyy", PtPt));
                                Cell(table.Cell(), d.TipoCodigo);
                                Cell(table.Cell(), d.Numero ?? $"#{d.NumeroInterno}");
                                Cell(table.Cell(), string.IsNullOrWhiteSpace(d.ClienteNome) ? "Consumidor final" : d.ClienteNome!);
                                Cell(table.Cell(), d.Estado);
                                Money(table.Cell(), sinal * d.BaseCents);
                                Money(table.Cell(), sinal * d.IvaCents);
                                Money(table.Cell(), sinal * d.TotalCents);
                            }
                        });
                        col.Item().Text("Notas de Crédito subtraem ao faturado. Recibos (RG) são liquidações e Anulados/Rascunhos aparecem mas não somam.")
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                    }

                    // === COMPRAS DE STOCK ===
                    col.Item().PaddingTop(6).Text($"Compras de stock ({compras.Count})").FontSize(12).Bold();
                    LinhaTable(col, compras, "Fornecedor");

                    // === DESPESAS OPERACIONAIS ===
                    col.Item().PaddingTop(6).Text($"Despesas operacionais ({despesas.Count})").FontSize(12).Bold();
                    LinhaTable(col, despesas, "Fornecedor");

                    col.Item().PaddingTop(6).Text("Valores com IVA. 'Resultado simples' = faturado − compras − despesas (visão de tesouraria; não substitui a contabilidade).")
                        .FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Mender — extrato unificado").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                        t.Span($"Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC — pág. ");
                        t.CurrentPageNumber();
                        t.Span("/");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void LinhaTable(ColumnDescriptor col, IReadOnlyList<IvaDeducaoLinha> linhas, string contraparteLabel)
    {
        if (linhas.Count == 0)
        {
            col.Item().Text("Sem movimentos no período.").FontColor(Colors.Grey.Darken1);
            return;
        }
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(58);   // data
                c.RelativeColumn(2.4f); // descricao
                c.RelativeColumn(1.2f); // contraparte
                c.RelativeColumn(0.8f); // iva
                c.RelativeColumn(1);    // total
            });
            table.Header(h =>
            {
                Header(h.Cell(), "Data");
                Header(h.Cell(), "Descricao");
                Header(h.Cell(), contraparteLabel);
                Header(h.Cell(), "IVA");
                Header(h.Cell(), "Total");
            });
            foreach (var l in linhas.OrderBy(x => x.Data))
            {
                Cell(table.Cell(), l.Data.ToString("dd/MM/yyyy", PtPt));
                Cell(table.Cell(), l.Descricao);
                Cell(table.Cell(), l.Fornecedor ?? "—");
                Money(table.Cell(), l.IvaCents);
                Money(table.Cell(), l.ValorComIvaCents);
            }
        });
    }

    private static void Kpi(IContainer c, string label, int cents)
    {
        c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().Text(FormatMoney(cents)).FontSize(13).Bold();
        });
    }

    private static void Header(IContainer c, string text) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).Bold().FontSize(8);
    private static void Cell(IContainer c, string text) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(text).FontSize(8);
    private static void Money(IContainer c, int cents) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(FormatMoney(cents)).FontSize(8);
    private static string FormatMoney(int cents) => (cents / 100m).ToString("C", PtPt);
}
