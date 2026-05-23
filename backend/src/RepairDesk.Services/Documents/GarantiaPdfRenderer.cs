using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RepairDesk.Services.Documents;

public sealed record GarantiaPdfData(
    GarantiaPdfEmissor Emissor,
    string Slug,
    string PortalUrl,
    string OrigemLabel, // "Reparação" ou "Venda"
    string DocumentoReferencia, // "Reparação #00012" / "Venda #00045"
    DateTime DataInicio,
    DateTime DataFim,
    int DiasGarantia,
    string EquipamentoOuArtigo,
    string? ClienteNome,
    string? ClienteNif,
    IReadOnlyList<GarantiaPdfArtigo> Artigos,
    string Cobertura,
    string Exclusoes,
    bool Anulada,
    string? MotivoAnulacao,
    /// <summary>
    /// Sprint 129: condição do artigo (Novo/OpenBox/Recondicionado/Usado) que ditou o período.
    /// <c>null</c> para garantias de Reparação ou sem condição definida — não é renderizado.
    /// </summary>
    string? CondicaoLabel = null);

public sealed record GarantiaPdfEmissor(
    string Nome,
    string? Nif,
    string? Morada,
    string? CodigoPostal,
    string? Localidade,
    string? Telefone,
    string? Email,
    string? Website,
    string? PrimaryColor);

public sealed record GarantiaPdfArtigo(
    string Descricao,
    int Quantidade,
    /// <summary>IMEI completo (não mascarado) — este PDF é documento entregue ao cliente comprador.</summary>
    string? Imei);

public static class GarantiaPdfRenderer
{
    private static readonly CultureInfo PtPt = new("pt-PT");
    private const string DefaultBrand = "#0EA5E9";

    public static byte[] Render(GarantiaPdfData d)
    {
        var brand = NormalizeColor(d.Emissor.PrimaryColor) ?? DefaultBrand;
        var qrPng = GenerateQrPng(d.PortalUrl);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).LineHeight(1.35f).FontColor("#0a0a0a"));

                page.Header().Element(c => Header(c, d, brand));
                page.Content().Element(c => Body(c, d, brand, qrPng));
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, GarantiaPdfData d, string brand)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(d.Emissor.Nome).FontSize(20).Bold().FontColor(brand);
                var ident = new List<string?>
                {
                    d.Emissor.Nif is not null ? $"NIF {d.Emissor.Nif}" : null,
                };
                var identLine = string.Join("  ·  ", ident.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(identLine))
                    col.Item().Text(identLine).FontSize(9).FontColor(Colors.Grey.Darken1);

                var moradaParts = new List<string?>
                {
                    d.Emissor.Morada,
                    string.Join(" ", new[] { d.Emissor.CodigoPostal, d.Emissor.Localidade }
                        .Where(x => !string.IsNullOrWhiteSpace(x))),
                };
                foreach (var m in moradaParts.Where(x => !string.IsNullOrWhiteSpace(x)))
                    col.Item().Text(m!).FontSize(9).FontColor(Colors.Grey.Darken1);

                var contactos = new List<string?> { d.Emissor.Telefone, d.Emissor.Email, d.Emissor.Website };
                var contactosLine = string.Join("  ·  ", contactos.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(contactosLine))
                    col.Item().Text(contactosLine).FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("CERTIFICADO").FontSize(16).Bold().FontColor(Colors.Grey.Darken3);
                col.Item().AlignRight().Text("DE GARANTIA").FontSize(16).Bold().FontColor(Colors.Grey.Darken3);
                col.Item().PaddingTop(4).AlignRight().Text(d.DocumentoReferencia).FontSize(10).FontColor(Colors.Grey.Darken1);
                col.Item().AlignRight().Text($"Emitida em {d.DataInicio.ToString("dd/MM/yyyy", PtPt)}").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void Body(IContainer container, GarantiaPdfData d, string brand, byte[] qrPng)
    {
        container.PaddingTop(20).Column(col =>
        {
            if (d.Anulada)
            {
                col.Item().PaddingBottom(10).Border(1).BorderColor("#dc2626").Background("#fef2f2").Padding(8)
                    .Column(inner =>
                    {
                        inner.Item().Text("GARANTIA ANULADA").FontSize(12).Bold().FontColor("#991b1b");
                        if (!string.IsNullOrWhiteSpace(d.MotivoAnulacao))
                            inner.Item().PaddingTop(2).Text($"Motivo: {d.MotivoAnulacao}").FontSize(9).FontColor("#7f1d1d");
                    });
            }

            // Linha topo: validade + período + QR
            col.Item().PaddingBottom(12).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(d.EquipamentoOuArtigo).FontSize(14).Bold();
                    left.Item().PaddingTop(2).Text(d.OrigemLabel).FontSize(9).FontColor(Colors.Grey.Darken1);

                    left.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Início").FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(d.DataInicio.ToString("dd/MM/yyyy", PtPt)).FontSize(11).Bold();
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Fim").FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(d.DataFim.ToString("dd/MM/yyyy", PtPt)).FontSize(11).Bold();
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Período").FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(FormatPeriodo(d.DiasGarantia)).FontSize(11).Bold();
                            if (!string.IsNullOrWhiteSpace(d.CondicaoLabel))
                                c.Item().Text(d.CondicaoLabel).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(d.ClienteNome))
                    {
                        left.Item().PaddingTop(10).Text("Cliente").FontSize(8).FontColor(Colors.Grey.Darken1);
                        var clienteLine = string.IsNullOrWhiteSpace(d.ClienteNif)
                            ? d.ClienteNome!
                            : $"{d.ClienteNome}  ·  NIF {d.ClienteNif}";
                        left.Item().Text(clienteLine).FontSize(10);
                    }
                });

                row.ConstantItem(110).Column(right =>
                {
                    right.Item().AlignRight().Image(qrPng).FitArea();
                    right.Item().PaddingTop(4).AlignRight().Text("Verificar online").FontSize(7).FontColor(Colors.Grey.Darken1);
                    right.Item().AlignRight().Text(d.Slug).FontSize(8).FontColor(brand);
                });
            });

            // Artigos (se origem venda + tem artigos)
            if (d.Artigos.Count > 0)
            {
                col.Item().PaddingTop(6).Text("Artigos abrangidos").FontSize(11).Bold().FontColor(brand);
                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4)
                            .Text("Descrição").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignRight()
                            .Text("Qtd").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                        h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignRight()
                            .Text("IMEI").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                    });
                    foreach (var a in d.Artigos)
                    {
                        table.Cell().PaddingVertical(3).Text(a.Descricao).FontSize(9);
                        table.Cell().PaddingVertical(3).AlignRight().Text(a.Quantidade.ToString(PtPt)).FontSize(9);
                        table.Cell().PaddingVertical(3).AlignRight().Text(a.Imei ?? "—").FontSize(8).FontFamily("Courier");
                    }
                });
            }

            // Cobertura
            col.Item().PaddingTop(14).Text("Cobertura").FontSize(11).Bold().FontColor(brand);
            col.Item().PaddingTop(2).Text(d.Cobertura).FontSize(9);

            // Exclusões
            col.Item().PaddingTop(10).Text("Exclusões").FontSize(11).Bold().FontColor(brand);
            col.Item().PaddingTop(2).Text(d.Exclusoes).FontSize(9);

            // Direitos legais
            col.Item().PaddingTop(16).Border(1).BorderColor(Colors.Grey.Lighten2).Background("#f8fafc").Padding(8)
                .Column(legal =>
                {
                    legal.Item().Text("Direitos do consumidor (DL 84/2021)").FontSize(10).Bold();
                    legal.Item().PaddingTop(2).Text(
                        "Em caso de falta de conformidade do bem, o consumidor tem direito à reposição da conformidade " +
                        "(reparação ou substituição), à redução proporcional do preço ou à resolução do contrato (art. 15.º). " +
                        "Durante os primeiros 2 anos, presume-se que a falta de conformidade já existia à data da entrega (art. 13.º)."
                    ).FontSize(8).FontColor(Colors.Grey.Darken2);
                });
        });
    }

    private static void Footer(IContainer container)
    {
        container.AlignCenter().Text(t =>
        {
            t.Span("Gerado por ").FontSize(7).FontColor(Colors.Grey.Darken1);
            t.Span("Mender").FontSize(7).Bold().FontColor(Colors.Grey.Darken2);
            t.Span(" · este documento é a representação imprimível da garantia digital. Verifica sempre online via QR code para o estado actual.")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    private static byte[] GenerateQrPng(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(8);
    }

    private static string? NormalizeColor(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return null;
        var s = c.Trim();
        return s.StartsWith('#') ? s : $"#{s}";
    }

    /// <summary>
    /// Sprint 129: formata o período de garantia em linguagem humana.
    /// Casos canónicos (DL 84/2021) ganham label direta; resto fica em meses ou dias.
    /// </summary>
    public static string FormatPeriodo(int dias) => dias switch
    {
        1095 => "3 anos",
        730 => "2 anos",
        540 => "18 meses",
        var d when d % 365 == 0 => $"{d / 365} anos",
        var d when d % 30 == 0 && d >= 60 => $"{d / 30} meses",
        var d => $"{d} dias",
    };
}
