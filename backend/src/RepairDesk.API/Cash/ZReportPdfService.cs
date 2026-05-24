using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.API.Cash;

/// <summary>
/// Sprint 302 (Doc 80 Pillar A.1): Z-report PDF. Documento físico que cumpre
/// a obrigação fiscal PT de controlo de caixa (DL 28/2019 art. 6.º).
///
/// O PDF é gerado on-demand — o snapshot vive no <see cref="ICashService"/>
/// e é imutável depois de Closed, logo nunca diverge. Bruno pode imprimir
/// ou guardar digitalmente sem ter de produzir o ficheiro no momento do fecho.
/// </summary>
public sealed class ZReportPdfService : IZReportPdfService
{
    private static readonly CultureInfo PtPt = new("pt-PT");

    private readonly ICashService _cash;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;

    public ZReportPdfService(ICashService cash, ITenantRepository tenants, ITenantContext tenantContext)
    {
        _cash = cash;
        _tenants = tenants;
        _tenantContext = tenantContext;
    }

    public async Task<(byte[] Pdf, string Filename)> ForClosingAsync(Guid dailyClosingId, CancellationToken ct = default)
    {
        var detailed = await _cash.GetByIdAsync(dailyClosingId, ct)
            ?? throw new NotFoundException("DailyClosing", dailyClosingId);

        var tenant = _tenantContext.TenantId is { } tenantId
            ? await _tenants.FindByIdAsync(tenantId, ct)
            : null;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(tenant?.LegalName ?? tenant?.Name ?? "Mender").FontSize(18).Bold().FontColor("#0EA5E9");
                        if (!string.IsNullOrWhiteSpace(tenant?.Nif)) col.Item().Text($"NIF {tenant.Nif}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(tenant?.Address)) col.Item().Text(tenant.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(180).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text("FECHO DIÁRIO").FontSize(18).Bold();
                        col.Item().AlignRight().Text($"Z-{detailed.Date:yyyyMMdd}").FontSize(11).FontColor(Colors.Grey.Darken1);
                        col.Item().AlignRight().Text(detailed.Date.ToString("dd/MM/yyyy", PtPt)).FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (detailed.Status == DailyClosingStatus.Closed && detailed.ClosedAt is { } closedAt)
                            col.Item().AlignRight().Text($"Fechado às {closedAt:HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Spacing(14);

                    // === Resumo de saldos ===
                    col.Item().Background("#F1F5F9").Padding(12).Column(c =>
                    {
                        c.Spacing(6);
                        c.Item().Text("Resumo de Caixa").FontSize(12).Bold();
                        SummaryRow(c.Item(), "Saldo inicial (declarado)", detailed.OpeningCents);
                        SummaryRow(c.Item(), "Entradas em dinheiro", detailed.CashEntriesCents);
                        SummaryRow(c.Item(), "Saídas em dinheiro", -detailed.CashExitsCents);
                        c.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Lighten2);
                        SummaryRow(c.Item(), "Saldo esperado", detailed.ExpectedClosingCents, bold: true);
                        if (detailed.ActualClosingCents is { } actual)
                        {
                            SummaryRow(c.Item(), "Saldo contado", actual, bold: true);
                            var diff = detailed.DiffCents ?? 0;
                            var diffColor = diff == 0 ? "#16a34a" : (diff < 0 ? "#dc2626" : "#ca8a04");
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Diferença").FontSize(10);
                                r.AutoItem().Text(Money(diff)).FontSize(11).Bold().FontColor(diffColor);
                            });
                        }
                        else
                        {
                            c.Item().Text("[Caixa ainda em aberto — sem saldo contado]").FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                        }
                    });

                    // === Totais por método (não-dinheiro) ===
                    col.Item().Column(c =>
                    {
                        c.Item().Text("Totais por método de pagamento").FontSize(12).Bold();
                        c.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(d =>
                            {
                                d.RelativeColumn(3);
                                d.RelativeColumn(2);
                            });
                            MethodRow(t, "Dinheiro (líquido)", detailed.CashEntriesCents - detailed.CashExitsCents);
                            MethodRow(t, "MBWay", detailed.MbwayCents);
                            MethodRow(t, "Multibanco", detailed.MultibancoCents);
                            MethodRow(t, "Cartão", detailed.CardCents);
                            MethodRow(t, "Outros", detailed.OtherCents);
                        });
                    });

                    // === Movimentos ===
                    col.Item().Column(c =>
                    {
                        c.Item().Text($"Movimentos ({detailed.Movimentos.Count})").FontSize(12).Bold();
                        c.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(d =>
                            {
                                d.ConstantColumn(48);  // hora
                                d.RelativeColumn(4);   // descrição
                                d.RelativeColumn(1.5f); // método
                                d.RelativeColumn(1.2f); // tipo
                                d.RelativeColumn(1.2f); // valor
                            });

                            t.Header(h =>
                            {
                                HeaderCell(h.Cell(), "Hora");
                                HeaderCell(h.Cell(), "Descrição");
                                HeaderCell(h.Cell(), "Método");
                                HeaderCell(h.Cell(), "Tipo");
                                HeaderCell(h.Cell(), "Valor");
                            });

                            foreach (var m in detailed.Movimentos.OrderBy(x => x.OccurredAt))
                            {
                                var sign = IsExit(m.Type) ? -1 : 1;
                                t.Cell().Padding(4).Text(m.OccurredAt.ToString("HH:mm", PtPt)).FontSize(9);
                                t.Cell().Padding(4).Text(m.Descricao).FontSize(9);
                                t.Cell().Padding(4).Text(m.PaymentMethod.ToString()).FontSize(9);
                                t.Cell().Padding(4).Text(TypeLabel(m.Type)).FontSize(9);
                                t.Cell().Padding(4).AlignRight().Text(Money(sign * m.AmountCents))
                                    .FontSize(9).FontColor(sign < 0 ? "#dc2626" : Colors.Black);
                            }

                            if (detailed.Movimentos.Count == 0)
                            {
                                t.Cell().ColumnSpan(5).Padding(8).AlignCenter()
                                    .Text("Sem movimentos registados.").FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                            }
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(detailed.Notas))
                    {
                        col.Item().PaddingTop(8).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                        {
                            c.Item().Text("Notas").FontSize(10).Bold();
                            c.Item().Text(detailed.Notas).FontSize(9);
                        });
                    }

                    // === Assinatura ===
                    col.Item().PaddingTop(20).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Darken1)
                                .PaddingTop(4).Text("Operador / Responsável").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        r.ConstantItem(40);
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Darken1)
                                .PaddingTop(4).Text("Data / Hora").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });

                page.Footer().AlignCenter().Text($"{tenant?.Name ?? "Mender"} — Gerado pelo Mender — DL 28/2019")
                    .FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        }).GeneratePdf();

        return (pdf, $"ZReport_{detailed.Date:yyyyMMdd}.pdf");
    }

    private static bool IsExit(CashMovementType type) =>
        type is CashMovementType.Sangria or CashMovementType.DespesaCaixa or CashMovementType.Troco;

    private static string TypeLabel(CashMovementType type) => type switch
    {
        CashMovementType.PagamentoCliente => "Pagamento",
        CashMovementType.Reforco => "Reforço",
        CashMovementType.Sangria => "Sangria",
        CashMovementType.DespesaCaixa => "Despesa",
        CashMovementType.Troco => "Troco",
        CashMovementType.AjusteManual => "Ajuste",
        _ => type.ToString(),
    };

    private static void HeaderCell(IContainer cell, string text)
        => cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).FontSize(9).Bold();

    private static void SummaryRow(IContainer container, string label, int cents, bool bold = false)
    {
        container.Row(r =>
        {
            var labelStyle = r.RelativeItem().Text(label).FontSize(10);
            if (bold) labelStyle.Bold();
            var valueStyle = r.AutoItem().Text(Money(cents)).FontSize(10);
            if (bold) valueStyle.Bold();
        });
    }

    private static void MethodRow(TableDescriptor t, string method, int cents)
    {
        t.Cell().Padding(3).Text(method).FontSize(10);
        t.Cell().Padding(3).AlignRight().Text(Money(cents)).FontSize(10);
    }

    private static string Money(int cents) => (cents / 100m).ToString("C", PtPt);
}
