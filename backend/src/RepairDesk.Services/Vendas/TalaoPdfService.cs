using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Vendas;

public interface ITalaoPdfService
{
    Task<byte[]> GerarAsync(Guid vendaId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 569: talão de 80 mm para a impressora de talões (Epson) do balcão — resumo da venda com o nº da
/// fatura-recibo emitida no Moloni ON. O documento fiscal é o do Moloni; o talão é o comprovativo rápido.
/// Página de 80 mm de largura e altura contínua (o rolo corta onde acaba).
/// </summary>
public class TalaoPdfService : ITalaoPdfService
{
    private readonly IVendaService _vendas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;

    public TalaoPdfService(IVendaService vendas, ITenantRepository tenants, ITenantContext tenant)
    {
        _vendas = vendas;
        _tenants = tenants;
        _tenant = tenant;
    }

    public async Task<byte[]> GerarAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.GetAsync(vendaId, ct);
        if (_tenant.TenantId is not { } tid) throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var loja = await _tenants.FindByIdAsync(tid, ct);
        var pt = CultureInfo.GetCultureInfo("pt-PT");
        string Eur(int cents) => (cents / 100m).ToString("0.00", pt) + " €";

        return Document.Create(doc => doc.Page(page =>
        {
            page.ContinuousSize(80, Unit.Millimetre);
            page.Margin(4, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Black));
            page.Content().Column(col =>
            {
                col.Spacing(2);
                col.Item().AlignCenter().Text(loja?.Name ?? "Loja").Bold().FontSize(11);
                var morada = string.Join(", ", new[] { loja?.Address, loja?.PostalCode, loja?.Locality }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (morada.Length > 0) col.Item().AlignCenter().Text(morada).FontSize(7);
                if (!string.IsNullOrWhiteSpace(loja?.Nif)) col.Item().AlignCenter().Text($"NIF {loja!.Nif}").FontSize(7);
                if (!string.IsNullOrWhiteSpace(loja?.Phone)) col.Item().AlignCenter().Text($"Tel. {loja!.Phone}").FontSize(7);
                col.Item().PaddingVertical(2).LineHorizontal(0.5f);

                col.Item().Text(venda.InvoiceNumber is { } n ? (n.StartsWith("FR") ? $"Fatura-recibo {n}" : $"Fatura {n}") : $"Venda #{venda.Numero}").Bold();
                col.Item().Text($"{HoraDeLisboa(venda.Data):dd/MM/yyyy HH:mm} · Venda #{venda.Numero}").FontSize(7);
                if (venda.Cliente is { } c) col.Item().Text(c.Nome).FontSize(7);
                col.Item().PaddingVertical(2).LineHorizontal(0.5f);

                foreach (var i in venda.Items)
                {
                    col.Item().Text(i.Descricao);
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"{i.Quantidade} × {Eur(i.PrecoUnitarioCents)}  (IVA {i.IvaRate.ToString("0.#", pt)}%)").FontSize(7);
                        r.AutoItem().Text(Eur(i.TotalCents));
                    });
                }
                col.Item().PaddingVertical(2).LineHorizontal(0.5f);
                col.Item().Row(r => { r.RelativeItem().Text("TOTAL").Bold().FontSize(11); r.AutoItem().Text(Eur(venda.TotalCents)).Bold().FontSize(11); });
                col.Item().Row(r => { r.RelativeItem().Text("IVA incluído").FontSize(7); r.AutoItem().Text(Eur(venda.IvaCents)).FontSize(7); });
                col.Item().Text($"Pago: {Pagamento(venda.PaymentMethod)}").FontSize(7);
                col.Item().PaddingTop(4).AlignCenter().Text(venda.InvoiceNumber is null
                    ? "Este talão não serve de fatura."
                    : "O documento fiscal original foi emitido no Moloni ON.").FontSize(6.5f).Italic();
                col.Item().AlignCenter().Text("Obrigado pela preferência!").FontSize(7);
            });
        })).GeneratePdf();
    }

    /// <summary>O servidor corre em UTC; o talão mostra a hora da loja.</summary>
    private static DateTime HoraDeLisboa(DateTime utc)
    {
        try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon")); }
        catch (TimeZoneNotFoundException) { return utc; }
    }

    private static string Pagamento(Core.Enums.PaymentMethod p) => p switch
    {
        Core.Enums.PaymentMethod.Dinheiro => "Dinheiro",
        Core.Enums.PaymentMethod.Multibanco => "Multibanco",
        Core.Enums.PaymentMethod.MBWay => "MB Way",
        Core.Enums.PaymentMethod.TransferenciaBancaria => "Transferência",
        Core.Enums.PaymentMethod.Cartao => "Cartão",
        _ => "Outro",
    };
}
