using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 449 (Doc 91 ponto 4): "Comprovativo de entrada de equipamento".
/// PDF A4 que o cliente assina quando deixa o telemóvel. Serve como recibo
/// legal de depósito + termos de armazenamento + dados de contacto.
///
/// Layout: cabeçalho com loja → dados cliente → equipamento + avaria →
/// termos curtos → duas linhas para assinaturas (cliente + colaborador) →
/// rodapé com URL portal para acompanhar online.
/// </summary>
public static class EntradaPdfRenderer
{
    private static readonly CultureInfo PtPt = new("pt-PT");
    private const string DefaultBrand = "#0EA5E9";

    public static byte[] Render(EntradaEquipamentoData d)
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

    private static void Header(IContainer container, EntradaEquipamentoData d, string brand)
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

                var contactos = new List<string?>
                {
                    d.Emissor.Telefone,
                    d.Emissor.Email,
                };
                var contactosLine = string.Join("  ·  ", contactos.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(contactosLine))
                    col.Item().Text(contactosLine).FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("RECIBO DE ENTRADA").FontSize(15).Bold().FontColor(Colors.Grey.Darken3);
                col.Item().AlignRight().Text($"Nº {d.Numero}").FontSize(11).FontColor(Colors.Grey.Darken1);
                col.Item().AlignRight().Text($"Data: {d.RecebidoEm.ToString("dd/MM/yyyy HH:mm", PtPt)}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void Body(IContainer container, EntradaEquipamentoData d, string brand)
    {
        container.PaddingTop(15).Column(col =>
        {
            // Cliente
            col.Item().PaddingTop(10).Background(Colors.Grey.Lighten5).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("CLIENTE").FontSize(9).Bold().FontColor(brand);
                    c.Item().PaddingTop(2).Text(d.Cliente.Nome).FontSize(12).Bold();
                    if (!string.IsNullOrWhiteSpace(d.Cliente.Telefone))
                        c.Item().Text($"Telefone: {d.Cliente.Telefone}").FontSize(10);
                    if (!string.IsNullOrWhiteSpace(d.Cliente.Email))
                        c.Item().Text($"Email: {d.Cliente.Email}").FontSize(10);
                    if (!string.IsNullOrWhiteSpace(d.Cliente.Nif))
                        c.Item().Text($"NIF: {d.Cliente.Nif}").FontSize(10);
                });
            });

            // Equipamento + avaria
            col.Item().PaddingTop(10).Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
            {
                c.Item().Text("EQUIPAMENTO").FontSize(9).Bold().FontColor(brand);
                c.Item().PaddingTop(2).Text(d.Equipamento).FontSize(12).Bold();
                // Sprint 490: tipo/categoria do equipamento (S475), quando classificado.
                if (!string.IsNullOrWhiteSpace(d.Tipo))
                    c.Item().Text($"Tipo: {d.Tipo}").FontSize(10);
                if (!string.IsNullOrWhiteSpace(d.Imei))
                    c.Item().Text($"IMEI / Nº série: {d.Imei}").FontSize(10);

                c.Item().PaddingTop(8).Text("AVARIA REPORTADA").FontSize(9).Bold().FontColor(brand);
                c.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(d.Avaria) ? "—" : d.Avaria).FontSize(10);

                if (!string.IsNullOrWhiteSpace(d.EstadoFisico))
                {
                    c.Item().PaddingTop(8).Text("ESTADO FÍSICO À ENTRADA").FontSize(9).Bold().FontColor(brand);
                    c.Item().PaddingTop(2).Text(d.EstadoFisico).FontSize(10);
                }

                if (d.CamposEquipamento is { Count: > 0 })
                {
                    c.Item().PaddingTop(8).Text("DADOS DO EQUIPAMENTO").FontSize(9).Bold().FontColor(brand);
                    foreach (var campo in d.CamposEquipamento)
                        c.Item().PaddingTop(1).Text($"{campo.Label}: {campo.Value}").FontSize(10);
                }
            });

            // Termos
            col.Item().PaddingTop(12).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
            {
                c.Item().Text("TERMOS DE DEPÓSITO").FontSize(9).Bold().FontColor(brand);
                c.Item().PaddingTop(4).Text(text =>
                {
                    text.Span("1. O equipamento fica depositado na loja até ser levantado pelo cliente. ").FontSize(9);
                    text.Span("2. Os dados acima foram fornecidos pelo cliente e conferem com a observação visual do colaborador. ").FontSize(9);
                    text.Span("3. A loja não se responsabiliza por equipamentos por levantar há mais de 90 dias após a comunicação de conclusão. ").FontSize(9);
                    text.Span("4. A análise pode revelar avarias adicionais não detectadas na inspeção inicial — o cliente será contactado antes de qualquer custo extra. ").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(d.TermosLoja))
                    {
                        text.Span("\n\n").FontSize(9);
                        text.Span(d.TermosLoja).FontSize(9);
                    }
                });
            });

            // Assinaturas — digital (S551) quando recolhida no balcão; senão espaço para preencher.
            col.Item().PaddingTop(20).Row(row =>
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
                        c.Item().Text($"Data: ___/___/____").FontSize(8).FontColor(Colors.Grey.Lighten1);
                    }
                });
                row.ConstantItem(30);
                row.RelativeItem().Column(c =>
                {
                    c.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Darken1).Height(40);
                    c.Item().PaddingTop(2).Text("Colaborador").FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(d.RecebidoPor ?? "—").FontSize(8).FontColor(Colors.Grey.Lighten1);
                });
            });

            // QR portal cliente (se há URL).
            if (!string.IsNullOrWhiteSpace(d.PortalUrl))
            {
                col.Item().PaddingTop(15).Row(row =>
                {
                    row.ConstantItem(100).Image(GenerateQrPng(d.PortalUrl!));
                    row.RelativeItem().PaddingLeft(10).Column(c =>
                    {
                        c.Item().Text("Acompanhar online").FontSize(10).Bold().FontColor(brand);
                        c.Item().PaddingTop(2).Text(text =>
                        {
                            text.Span("Scan ou abre: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span(d.PortalUrl!).FontSize(9).FontColor(brand);
                        });
                        c.Item().PaddingTop(2).Text("Recebes notificações no telemóvel quando o estado mudar.")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            }
        });
    }

    private static void Footer(IContainer container, EntradaEquipamentoData d)
    {
        container.PaddingTop(8).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2)
            .PaddingTop(6).AlignCenter().Text(text =>
        {
            text.Span($"{d.Emissor.Nome} · Recibo de entrada · Gerado pelo Mender")
                .FontSize(7).FontColor(Colors.Grey.Lighten1);
        });
    }

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
