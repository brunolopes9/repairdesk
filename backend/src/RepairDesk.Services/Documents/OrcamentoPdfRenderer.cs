using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RepairDesk.Services.Documents;

public static class OrcamentoPdfRenderer
{
    private static readonly CultureInfo PtPt = new("pt-PT");
    private const string DefaultBrand = "#0EA5E9";

    public static byte[] Render(OrcamentoData o)
    {
        var brand = NormalizeColor(o.Emissor.PrimaryColor) ?? DefaultBrand;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).LineHeight(1.3f).FontColor("#0a0a0a"));

                page.Header().Element(c => Header(c, o, brand));
                page.Content().Element(c => Body(c, o, brand));
                page.Footer().Element(c => Footer(c, o));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, OrcamentoData o, string brand)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(o.Emissor.Nome).FontSize(20).Bold().FontColor(brand);

                var ident = new List<string?>
                {
                    o.Emissor.Nif is not null ? $"NIF {o.Emissor.Nif}" : null,
                    o.Emissor.CaePrincipal is not null ? $"CAE {o.Emissor.CaePrincipal}" : null,
                };
                var identLine = string.Join("  ·  ", ident.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(identLine))
                    col.Item().Text(identLine).FontSize(9).FontColor(Colors.Grey.Darken1);

                var moradaParts = new List<string?>
                {
                    o.Emissor.Morada,
                    string.Join(" ", new[] { o.Emissor.CodigoPostal, o.Emissor.Localidade }
                        .Where(x => !string.IsNullOrWhiteSpace(x))),
                };
                foreach (var m in moradaParts.Where(x => !string.IsNullOrWhiteSpace(x)))
                    col.Item().Text(m!).FontSize(9).FontColor(Colors.Grey.Darken1);

                var contactos = new List<string?>
                {
                    o.Emissor.Telefone,
                    o.Emissor.Email,
                    o.Emissor.Website,
                };
                var contactosLine = string.Join("  ·  ", contactos.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(contactosLine))
                    col.Item().Text(contactosLine).FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            row.ConstantItem(160).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("ORÇAMENTO").FontSize(20).Bold().FontColor(Colors.Grey.Darken3);
                col.Item().AlignRight().Text(o.Numero).FontSize(11).FontColor(Colors.Grey.Darken1);
                col.Item().AlignRight().Text($"Data: {o.Data.ToString("dd/MM/yyyy", PtPt)}").FontSize(9).FontColor(Colors.Grey.Darken1);
                col.Item().AlignRight().Text($"Válido até {o.ValidoAte.ToString("dd/MM/yyyy", PtPt)}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void Body(IContainer container, OrcamentoData o, string brand)
    {
        container.PaddingTop(20).Column(col =>
        {
            col.Spacing(15);

            // Cliente
            col.Item().Column(c =>
            {
                c.Item().Text("PARA").FontSize(8).Bold().LetterSpacing(0.1f).FontColor(Colors.Grey.Darken1);
                c.Item().Text(o.Cliente.Nome).FontSize(13).Bold();
                var parts = new[] { o.Cliente.Telefone, o.Cliente.Email, o.Cliente.Nif != null ? $"NIF {o.Cliente.Nif}" : null }
                    .Where(x => !string.IsNullOrWhiteSpace(x));
                if (parts.Any())
                    c.Item().Text(string.Join("  ·  ", parts)).FontSize(9).FontColor(Colors.Grey.Darken2);
            });

            // Trabalho / Reparação
            col.Item().Column(c =>
            {
                c.Item().Text(o.Tipo.ToUpperInvariant()).FontSize(8).Bold().LetterSpacing(0.1f).FontColor(Colors.Grey.Darken1);
                c.Item().Text(o.Titulo).FontSize(13).Bold();
                if (!string.IsNullOrWhiteSpace(o.Descricao))
                    c.Item().Text(o.Descricao!).FontSize(10).FontColor(Colors.Grey.Darken2);
            });

            // Tabela de linhas
            col.Item().Element(c => LinhasTable(c, o, brand));

            // Observações
            if (!string.IsNullOrWhiteSpace(o.Observacoes))
            {
                col.Item().PaddingTop(10).Column(c =>
                {
                    c.Item().Text("OBSERVAÇÕES").FontSize(8).Bold().LetterSpacing(0.1f).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(o.Observacoes!).FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            }

            // Pagamento (IBAN)
            if (!string.IsNullOrWhiteSpace(o.Emissor.Iban))
            {
                col.Item().PaddingTop(8).Column(c =>
                {
                    c.Item().Text("PAGAMENTO").FontSize(8).Bold().LetterSpacing(0.1f).FontColor(Colors.Grey.Darken1);
                    c.Item().Text($"IBAN  {FormatIban(o.Emissor.Iban!)}").FontSize(10).FontFamily("Courier").FontColor(Colors.Grey.Darken3);
                });
            }

            // Termos e condições
            if (!string.IsNullOrWhiteSpace(o.Emissor.TermosCondicoes))
            {
                col.Item().PaddingTop(8).Column(c =>
                {
                    c.Item().Text("TERMOS E CONDIÇÕES").FontSize(8).Bold().LetterSpacing(0.1f).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(o.Emissor.TermosCondicoes!).FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            }

            // QR para portal cliente (apenas reparações têm slug)
            if (!string.IsNullOrWhiteSpace(o.PortalUrl))
            {
                var qrPng = GenerateQrPng(o.PortalUrl!);
                col.Item().PaddingTop(10).Row(row =>
                {
                    row.ConstantItem(90).Image(qrPng);
                    row.RelativeItem().PaddingLeft(12).AlignMiddle().Column(c =>
                    {
                        c.Item().Text("ACOMPANHA A TUA REPARAÇÃO").FontSize(8).Bold().LetterSpacing(0.1f).FontColor(Colors.Grey.Darken2);
                        c.Item().PaddingTop(2).Text("Lê o QR ou abre o link no telemóvel para ver o estado em tempo real, aprovar o orçamento e contactar a loja.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        c.Item().PaddingTop(2).Text(o.PortalUrl!).FontSize(8).FontFamily("Courier").FontColor(Colors.Grey.Darken1);
                    });
                });
            }
        });
    }

    private static byte[] GenerateQrPng(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(8);
    }

    private static void LinhasTable(IContainer container, OrcamentoData o, string brand)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4);
                c.RelativeColumn(1);
            });

            table.Header(h =>
            {
                h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(5).Text("Descrição").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                    .Padding(5).AlignRight().Text("Valor").FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
            });

            if (o.Linhas.Count == 0)
            {
                table.Cell().ColumnSpan(2).Padding(5)
                    .Text("(orçamento global, sem detalhe por linha)")
                    .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
            }
            else
            {
                foreach (var linha in o.Linhas)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .Padding(5).Text(linha.Descricao).FontSize(10);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .Padding(5).AlignRight().Text(FormatMoney(linha.ValorCents)).FontSize(10);
                }
            }

            // Total
            table.Cell().Padding(5).PaddingTop(8)
                .Text("TOTAL").FontSize(11).Bold();
            table.Cell().Padding(5).PaddingTop(8).AlignRight()
                .Text(FormatMoney(o.TotalCents)).FontSize(14).Bold().FontColor(brand);
        });
    }

    private static void Footer(IContainer container, OrcamentoData o)
    {
        container.PaddingTop(10).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2)
            .PaddingTop(8).Column(col =>
        {
            col.Item().Text(text =>
            {
                text.Span("Este documento é um orçamento e ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.Span("NÃO constitui factura").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                text.Span(".").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(2).AlignCenter().Text(text =>
            {
                text.Span($"{o.Emissor.Nome} · Gerado pelo RepairDesk").FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        });
    }

    private static string FormatMoney(int cents) =>
        (cents / 100m).ToString("C", PtPt);

    private static string FormatIban(string iban)
    {
        var clean = iban.Replace(" ", "").ToUpperInvariant();
        var chunks = new List<string>();
        for (int i = 0; i < clean.Length; i += 4)
            chunks.Add(clean.Substring(i, Math.Min(4, clean.Length - i)));
        return string.Join(" ", chunks);
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
