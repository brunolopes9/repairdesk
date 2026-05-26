using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RepairDesk.Services.Documents;

public sealed record LabelPdfData(
    string Numero,
    string ClienteNome,
    string? ClienteTelefone,
    string Equipamento,
    string? Imei,
    string QrPayload,
    string? TenantNome);

/// <summary>
/// Sprint 347 (Doc 83 Pillar 4): renderiza etiqueta 62×29mm (formato Brother QL DK-22210
/// e equivalentes). Pensado para imprimir em térmica via PDF "fit-to-page".
/// </summary>
public static class LabelPdfRenderer
{
    public static byte[] Render(LabelPdfData d)
    {
        var qrPng = GenerateQrPng(d.QrPayload);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(62, 29, Unit.Millimetre);
                page.Margin(2, Unit.Millimetre);
                // Helvetica: embutida no QuestPDF e disponível no container Linux.
                // Arial não existe em Linux sem fontes instaladas → render falha (500).
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(7));

                page.Content().Row(row =>
                {
                    row.RelativeItem(3).Column(col =>
                    {
                        col.Item().Text(d.Numero).FontSize(14).Bold();
                        col.Item().PaddingTop(1).Text(Trunc(d.ClienteNome, 28)).FontSize(7).SemiBold();
                        if (!string.IsNullOrWhiteSpace(d.ClienteTelefone))
                            col.Item().Text($"Tel: {d.ClienteTelefone}").FontSize(6);
                        col.Item().Text(Trunc(d.Equipamento, 32)).FontSize(6);
                        if (!string.IsNullOrWhiteSpace(d.Imei))
                            col.Item().Text($"IMEI: {d.Imei}").FontSize(5);
                        if (!string.IsNullOrWhiteSpace(d.TenantNome))
                            col.Item().AlignBottom().Text(d.TenantNome!).FontSize(5).Italic();
                    });

                    row.ConstantItem(22, Unit.Millimetre).AlignRight().AlignMiddle()
                        .Width(22, Unit.Millimetre).Height(22, Unit.Millimetre)
                        .Image(qrPng);
                });
            });
        }).GeneratePdf();
    }

    private static string Trunc(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");

    private static byte[] GenerateQrPng(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(10);
    }
}
