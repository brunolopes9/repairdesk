using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface IVendaPdfService
{
    Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, string? portalBaseUrl = null, CancellationToken ct = default);
}

public class VendaPdfService : IVendaPdfService
{
    private static readonly CultureInfo PtPt = new("pt-PT");
    private readonly IVendaRepository _vendas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IGarantiaRepository _garantias;

    public VendaPdfService(
        IVendaRepository vendas,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IGarantiaRepository garantias)
    {
        _vendas = vendas;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _garantias = garantias;
    }

    public async Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, string? portalBaseUrl = null, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        var tenant = _tenantContext.TenantId is { } tenantId
            ? await _tenants.FindByIdAsync(tenantId, ct)
            : null;

        // Sprint 81: se a venda tem garantia digital, gera QR + URL para o portal.
        var baseUrl = string.IsNullOrWhiteSpace(portalBaseUrl) ? "https://app.lopestech.pt" : portalBaseUrl.TrimEnd('/');
        var garantia = await _garantias.FindByVendaAsync(vendaId, ct);
        var garantiaQrPng = garantia is not null && !garantia.Anulada
            ? GenerateQrPng($"{baseUrl}/g/{garantia.Slug}")
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
                        col.Item().Text(tenant?.LegalName ?? tenant?.Name ?? "Reparo").FontSize(18).Bold().FontColor("#0EA5E9");
                        if (!string.IsNullOrWhiteSpace(tenant?.Nif)) col.Item().Text($"NIF {tenant.Nif}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(tenant?.Address)) col.Item().Text(tenant.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(180).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text("RECIBO DE VENDA").FontSize(18).Bold();
                        col.Item().AlignRight().Text($"V-{venda.Numero:D5}").FontSize(11).FontColor(Colors.Grey.Darken1);
                        col.Item().AlignRight().Text(venda.Data.ToString("dd/MM/yyyy HH:mm", PtPt)).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().PaddingTop(24).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Text(venda.Cliente is not null
                        ? $"Cliente: {venda.Cliente.Nome}{(string.IsNullOrWhiteSpace(venda.Cliente.Nif) ? "" : $" - NIF {venda.Cliente.Nif}")}"
                        : "Cliente: Consumidor final")
                        .FontSize(11).Bold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.2f);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Descricao");
                            HeaderCell(h.Cell(), "Qtd");
                            HeaderCell(h.Cell(), "IVA");
                            HeaderCell(h.Cell(), "Unit.");
                            HeaderCell(h.Cell(), "Total");
                        });

                        foreach (var item in venda.Items)
                        {
                            // Sprint 68: descrição mostra IMEI por baixo quando presente
                            // (não-mascarado no recibo do cliente — é o documento dele).
                            table.Cell().Padding(5).Column(desc =>
                            {
                                desc.Item().Text(item.Descricao);
                                if (!string.IsNullOrEmpty(item.Imei))
                                {
                                    desc.Item().Text($"IMEI: {item.Imei}")
                                        .FontSize(8).FontFamily("Courier").FontColor("#525252");
                                }
                                if (!string.IsNullOrEmpty(item.Imei2))
                                {
                                    desc.Item().Text($"IMEI 2: {item.Imei2}")
                                        .FontSize(8).FontFamily("Courier").FontColor("#525252");
                                }
                            });
                            table.Cell().Padding(5).AlignRight().Text(item.Quantidade.ToString(PtPt));
                            table.Cell().Padding(5).AlignRight().Text($"{item.IvaRate:0.##}%");
                            table.Cell().Padding(5).AlignRight().Text(Money(item.PrecoUnitarioCents));
                            table.Cell().Padding(5).AlignRight().Text(Money(item.TotalCents));
                        }
                    });

                    col.Item().AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"IVA incluido: {Money(venda.IvaCents)}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        totals.Item().Text($"Total: {Money(venda.TotalCents)}").FontSize(16).Bold().FontColor("#0EA5E9");
                    });

                    if (venda.InvoiceExternalId is null)
                    {
                        col.Item().PaddingTop(16).Border(1).BorderColor(Colors.Red.Lighten2).Background(Colors.Red.Lighten5).Padding(10)
                            .Text("Documento nao fiscal - emitir fatura no software certificado.")
                            .FontSize(10).Bold().FontColor(Colors.Red.Darken2);
                    }
                    else
                    {
                        col.Item().PaddingTop(10).Text($"Fatura: {venda.InvoiceNumber ?? venda.InvoiceExternalId}")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                    }

                    // Sprint 81: bloco garantia digital com QR code
                    if (garantia is not null && garantiaQrPng is not null)
                    {
                        col.Item().PaddingTop(16).Border(1).BorderColor("#10b981").Background("#ecfdf5").Padding(12)
                            .Row(grantia =>
                            {
                                grantia.RelativeItem().Column(info =>
                                {
                                    info.Item().Text("GARANTIA DIGITAL").FontSize(11).Bold().FontColor("#065f46");
                                    info.Item().PaddingTop(2).Text($"Vigente até {garantia.DataFim.ToString("dd/MM/yyyy", PtPt)}")
                                        .FontSize(10).FontColor("#065f46");
                                    info.Item().PaddingTop(2).Text($"Período: {garantia.DiasGarantia} dias (DL 84/2021)")
                                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                                    info.Item().PaddingTop(6).Text("Verifica a garantia em qualquer altura:")
                                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                                    info.Item().Text($"{baseUrl}/g/{garantia.Slug}")
                                        .FontSize(8).FontFamily("Courier").FontColor("#065f46");
                                });
                                grantia.ConstantItem(90).Image(garantiaQrPng).FitArea();
                            });
                    }
                });

                page.Footer().AlignCenter().Text($"{tenant?.Name ?? "Reparo"} - Gerado pelo Reparo").FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        }).GeneratePdf();

        return (pdf, $"Recibo_V-{venda.Numero:D5}.pdf");
    }

    private static void HeaderCell(IContainer cell, string text)
        => cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text).FontSize(9).Bold();

    private static string Money(int cents) => (cents / 100m).ToString("C", PtPt);

    private static byte[] GenerateQrPng(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(8);
    }
}
