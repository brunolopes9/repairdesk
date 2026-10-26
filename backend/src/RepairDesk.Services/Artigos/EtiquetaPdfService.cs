using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Artigos;

public sealed record EtiquetaPedido(Guid ArtigoId, int Copias = 1);

/// <summary>Tamanho do rolo em mm (Marklife P50: 50×30 por defeito). Uma etiqueta por página.</summary>
public sealed record EtiquetasRequest(IReadOnlyList<EtiquetaPedido> Itens, decimal LarguraMm = 50, decimal AlturaMm = 30, bool MostrarPreco = true);

public interface IEtiquetaPdfService
{
    Task<byte[]> GerarAsync(EtiquetasRequest req, CancellationToken ct = default);
}

/// <summary>
/// Sprint 566: etiquetas dos artigos — nome, preço, código de barras EAN-13 e SKU. Uma etiqueta por página
/// do tamanho do rolo, que é como as impressoras de etiquetas (Marklife, Brother, Dymo) imprimem PDFs.
/// </summary>
public class EtiquetaPdfService : IEtiquetaPdfService
{
    private const int MaxEtiquetas = 500;
    private readonly IArtigoService _artigos;

    public EtiquetaPdfService(IArtigoService artigos) => _artigos = artigos;

    public async Task<byte[]> GerarAsync(EtiquetasRequest req, CancellationToken ct = default)
    {
        if (req.Itens is null || req.Itens.Count == 0)
            throw new ValidationException("sem_etiquetas", "Escolhe pelo menos um artigo.");
        if (req.LarguraMm is < 20 or > 120 || req.AlturaMm is < 10 or > 120)
            throw new ValidationException("tamanho_invalido", "Tamanho da etiqueta entre 20×10 e 120×120 mm.");
        var total = req.Itens.Sum(i => Math.Max(0, i.Copias));
        if (total is 0 or > MaxEtiquetas)
            throw new ValidationException("copias_invalidas", $"Entre 1 e {MaxEtiquetas} etiquetas de cada vez.");

        var etiquetas = new List<ArtigoDto>();
        foreach (var item in req.Itens.Where(i => i.Copias > 0))
        {
            var a = (await _artigos.GetAsync(item.ArtigoId, ct)).Artigo;
            etiquetas.AddRange(Enumerable.Repeat(a, item.Copias));
        }

        var largura = (float)req.LarguraMm;
        var altura = (float)req.AlturaMm;
        const float margem = 1.5f;
        var pequena = altura < 20; // rolos estreitos: nome numa linha, letra mais pequena
        var cabecalhoMm = pequena ? 3f : 6.5f;
        var rodapeMm = pequena ? 2.2f : 2.8f;
        // As barras ocupam exatamente o espaço do meio (largura toda) — a proporção vai no próprio SVG.
        var barrasMm = Math.Max(3f, altura - 2 * margem - cabecalhoMm - rodapeMm - 1f);
        var alturaModulos = (int)Math.Round(113 * barrasMm / (largura - 2 * margem));
        var pt = CultureInfo.GetCultureInfo("pt-PT");

        return Document.Create(doc =>
        {
            foreach (var a in etiquetas)
            {
                doc.Page(page =>
                {
                    page.Size(largura, altura, Unit.Millimetre);
                    page.Margin(margem, Unit.Millimetre);
                    page.DefaultTextStyle(t => t.FontSize(pequena ? 5.5f : 7).FontColor(Colors.Black).LineHeight(1f));
                    page.Header().Height(cabecalhoMm, Unit.Millimetre).Row(row =>
                    {
                        row.RelativeItem().Text(a.Nome).Bold().ClampLines(pequena ? 1 : 2);
                        if (req.MostrarPreco && a.PrecoEfetivoCents is { } p)
                            row.AutoItem().PaddingLeft(1, Unit.Millimetre).AlignRight()
                                .Text((p / 100m).ToString("0.00", pt) + " €").Bold().FontSize(pequena ? 7 : 10);
                    });
                    page.Content().PaddingVertical(0.5f, Unit.Millimetre).Element(e =>
                    {
                        if (Ean13.Valido(a.CodigoBarras)) e.Svg(Ean13.Svg(a.CodigoBarras, alturaModulos)).FitArea();
                    });
                    page.Footer().Height(rodapeMm, Unit.Millimetre).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Text(a.CodigoBarras).FontSize(pequena ? 5 : 6).LetterSpacing(0.15f);
                        row.AutoItem().Text(a.Sku).FontSize(pequena ? 5 : 6).Bold();
                    });
                });
            }
        }).GeneratePdf();
    }
}
