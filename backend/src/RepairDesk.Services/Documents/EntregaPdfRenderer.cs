using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 451 (Doc 91 ponto 4): "Recibo de entrega" — assinado pelo cliente
/// quando vem buscar o equipamento reparado. Par natural do S450 (Recibo de
/// entrada). Inclui resumo da intervenção, valor pago, garantia da reparação.
/// </summary>
public static class EntregaPdfRenderer
{
    private static readonly CultureInfo PtPt = new("pt-PT");
    private const string DefaultBrand = "#0EA5E9";

    public static byte[] Render(EntregaEquipamentoData d)
    {
        var brand = NormalizeColor(d.Emissor.PrimaryColor) ?? DefaultBrand;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).LineHeight(1.3f).FontColor("#0a0a0a"));

                page.Header().Element(c => Header(c, d, brand));
                page.Content().Element(c => Body(c, d, brand));
                page.Footer().Element(c => Footer(c, d));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, EntregaEquipamentoData d, string brand)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(d.Emissor.Nome).FontSize(20).Bold().FontColor(brand);
                if (!string.IsNullOrWhiteSpace(d.Emissor.Nif))
                    col.Item().Text($"NIF {d.Emissor.Nif}").FontSize(9).FontColor(Colors.Grey.Darken1);
                var morada = string.Join(" ", new[] { d.Emissor.Morada, d.Emissor.CodigoPostal, d.Emissor.Localidade }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(morada))
                    col.Item().Text(morada).FontSize(9).FontColor(Colors.Grey.Darken1);
                var contactos = string.Join("  ·  ", new[] { d.Emissor.Telefone, d.Emissor.Email }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(contactos))
                    col.Item().Text(contactos).FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("RECIBO DE ENTREGA").FontSize(15).Bold().FontColor(Colors.Grey.Darken3);
                col.Item().AlignRight().Text($"Nº {d.Numero}").FontSize(11).FontColor(Colors.Grey.Darken1);
                col.Item().AlignRight().Text($"Data: {d.EntregueEm.ToString("dd/MM/yyyy HH:mm", PtPt)}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void Body(IContainer container, EntregaEquipamentoData d, string brand)
    {
        container.PaddingTop(15).Column(col =>
        {
            // Cliente + equipamento side by side.
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
                {
                    c.Item().Text("CLIENTE").FontSize(9).Bold().FontColor(brand);
                    c.Item().PaddingTop(2).Text(d.Cliente.Nome).FontSize(12).Bold();
                    if (!string.IsNullOrWhiteSpace(d.Cliente.Telefone))
                        c.Item().Text($"Telefone: {d.Cliente.Telefone}").FontSize(10);
                    if (!string.IsNullOrWhiteSpace(d.Cliente.Nif))
                        c.Item().Text($"NIF: {d.Cliente.Nif}").FontSize(10);
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
                {
                    c.Item().Text("EQUIPAMENTO").FontSize(9).Bold().FontColor(brand);
                    c.Item().PaddingTop(2).Text(d.Equipamento).FontSize(12).Bold();
                    // Sprint 491: tipo/categoria do equipamento (S475), quando classificado.
                    if (!string.IsNullOrWhiteSpace(d.Tipo))
                        c.Item().Text($"Tipo: {d.Tipo}").FontSize(10);
                    if (!string.IsNullOrWhiteSpace(d.Imei))
                        c.Item().Text($"IMEI / Nº série: {d.Imei}").FontSize(10);
                });
            });

            // Intervenção realizada.
            col.Item().PaddingTop(10).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text("INTERVENÇÃO REALIZADA").FontSize(9).Bold().FontColor(brand);
                if (!string.IsNullOrWhiteSpace(d.Diagnostico))
                {
                    c.Item().PaddingTop(2).Text("Diagnóstico:").FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(d.Diagnostico).FontSize(10);
                }
                c.Item().PaddingTop(4).Text("Resumo da reparação:").FontSize(9).FontColor(Colors.Grey.Darken1);
                c.Item().Text(string.IsNullOrWhiteSpace(d.ResumoIntervencao) ? "—" : d.ResumoIntervencao).FontSize(10);

                if (d.Linhas.Count > 0)
                {
                    c.Item().PaddingTop(8).Text("Peças e mão-de-obra").FontSize(9).Bold().FontColor(brand);
                    c.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(4);
                            cd.RelativeColumn(1);
                        });
                        foreach (var linha in d.Linhas)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                .Text(linha.Descricao).FontSize(9);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).AlignRight()
                                .Text(FormatMoney(linha.ValorCents)).FontSize(9);
                        }
                    });
                }
            });

            // Total + garantia.
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Background("#F0FDF4").Padding(10).Column(c =>
                {
                    c.Item().Text("TOTAL PAGO").FontSize(9).Bold().FontColor("#15803D");
                    c.Item().PaddingTop(2).Text(FormatMoney(d.TotalPagoCents)).FontSize(20).Bold().FontColor("#166534");
                });
                row.ConstantItem(10);
                row.RelativeItem().Background("#EFF6FF").Padding(10).Column(c =>
                {
                    c.Item().Text("GARANTIA DA REPARAÇÃO").FontSize(9).Bold().FontColor("#1D4ED8");
                    var ate = d.EntregueEm.AddDays(d.DiasGarantia);
                    c.Item().PaddingTop(2).Text($"{d.DiasGarantia} dias").FontSize(13).Bold().FontColor("#1E40AF");
                    c.Item().Text($"Até {ate.ToString("dd/MM/yyyy", PtPt)}").FontSize(9).FontColor("#1E40AF");
                });
            });

            if (!string.IsNullOrWhiteSpace(d.GarantiaCobertura))
            {
                col.Item().PaddingTop(8).Text("Cobertura da garantia").FontSize(9).Bold().FontColor(brand);
                col.Item().PaddingTop(2).Text(d.GarantiaCobertura).FontSize(9);
            }

            // Termos curtos + assinaturas.
            col.Item().PaddingTop(12).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingTop(8).Column(c =>
            {
                c.Item().Text("DECLARAÇÃO DO CLIENTE").FontSize(9).Bold().FontColor(brand);
                c.Item().PaddingTop(2).Text("Confirmo o levantamento do equipamento acima identificado e a conformidade da reparação com o orçamento aprovado.").FontSize(9);
            });

            // Assinatura digital (S551) quando recolhida no balcão; senão espaço para preencher.
            col.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    if (d.AssinaturaPng is { Length: > 0 })
                    {
                        c.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Darken1).Height(40)
                            .AlignCenter().AlignBottom().Image(d.AssinaturaPng).FitArea();
                        c.Item().PaddingTop(2).Text("Assinatura do cliente").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"Assinado digitalmente em {d.AssinaturaEm:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Lighten1);
                    }
                    else
                    {
                        c.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Darken1).Height(40);
                        c.Item().PaddingTop(2).Text("Assinatura do cliente").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"Data: {d.EntregueEm.ToString("dd/MM/yyyy", PtPt)}").FontSize(8).FontColor(Colors.Grey.Lighten1);
                    }
                });
                row.ConstantItem(30);
                row.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Darken1).Height(40);
                    c.Item().PaddingTop(2).Text("Colaborador").FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(d.EntreguePor ?? "—").FontSize(8).FontColor(Colors.Grey.Lighten1);
                });
            });

            // QR garantia (preferencial) ou portal cliente.
            var qrUrl = d.GarantiaUrl ?? d.PortalUrl;
            if (!string.IsNullOrWhiteSpace(qrUrl))
            {
                col.Item().PaddingTop(15).Row(row =>
                {
                    row.ConstantItem(100).Image(GenerateQrPng(qrUrl!));
                    row.RelativeItem().PaddingLeft(10).Column(c =>
                    {
                        var label = d.GarantiaUrl is not null ? "Reclamar garantia" : "Acompanhar online";
                        c.Item().Text(label).FontSize(10).Bold().FontColor(brand);
                        c.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Scan ou abre: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span(qrUrl!).FontSize(9).FontColor(brand);
                        });
                    });
                });
            }
        });
    }

    private static void Footer(IContainer container, EntregaEquipamentoData d)
    {
        container.PaddingTop(8).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2)
            .PaddingTop(6).AlignCenter().Text(text =>
        {
            text.Span($"{d.Emissor.Nome} · Recibo de entrega · Gerado pelo Mender")
                .FontSize(7).FontColor(Colors.Grey.Lighten1);
        });
    }

    private static string FormatMoney(int cents) => (cents / 100m).ToString("C", PtPt);

    private static byte[] GenerateQrPng(string url)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(10);
    }

    private static string? NormalizeColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var s = hex.Trim();
        if (!s.StartsWith('#')) s = "#" + s;
        if (s.Length is not (4 or 7)) return null;
        for (int i = 1; i < s.Length; i++)
        {
            if (!Uri.IsHexDigit(s[i])) return null;
        }
        return s;
    }
}
