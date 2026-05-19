using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface IVendaPdfService
{
    Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default);
}

public class VendaPdfService : IVendaPdfService
{
    private static readonly CultureInfo PtPt = new("pt-PT");
    private readonly IVendaRepository _vendas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;

    public VendaPdfService(IVendaRepository vendas, ITenantRepository tenants, ITenantContext tenantContext)
    {
        _vendas = vendas;
        _tenants = tenants;
        _tenantContext = tenantContext;
    }

    public async Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
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
                        col.Item().Text(tenant?.LegalName ?? tenant?.Name ?? "RepairDesk").FontSize(18).Bold().FontColor("#0EA5E9");
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
                            table.Cell().Padding(5).Text(item.Descricao);
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
                });

                page.Footer().AlignCenter().Text($"{tenant?.Name ?? "RepairDesk"} - Gerado pelo RepairDesk").FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        }).GeneratePdf();

        return (pdf, $"Recibo_V-{venda.Numero:D5}.pdf");
    }

    private static void HeaderCell(IContainer cell, string text)
        => cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text).FontSize(9).Bold();

    private static string Money(int cents) => (cents / 100m).ToString("C", PtPt);
}
