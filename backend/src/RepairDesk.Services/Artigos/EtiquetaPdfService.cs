using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Artigos;

public sealed record EtiquetaPedido(Guid ArtigoId, int Copias = 1);

/// <summary>Tamanho do rolo em mm (Marklife P50: 40×30 por defeito). Uma etiqueta por página.</summary>
public sealed record EtiquetasRequest(IReadOnlyList<EtiquetaPedido> Itens, decimal LarguraMm = 40, decimal AlturaMm = 30, bool MostrarPreco = true);

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
    private readonly ITenantRepository? _tenants;
    private readonly ITenantContext? _tenant;

    public EtiquetaPdfService(IArtigoService artigos, ITenantRepository? tenants = null, ITenantContext? tenant = null)
    {
        _artigos = artigos;
        _tenants = tenants;
        _tenant = tenant;
    }

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

        // Sprint 596: a marca da loja no topo (preto e branco, nítida numa térmica), o nome em português (o que sai
        // na fatura), o preço em grande, o código de barras e o SKU — numa etiqueta de 40×30 mm.
        var loja = _tenant?.TenantId is { } tid && _tenants is not null ? await _tenants.FindByIdAsync(tid, ct) : null;
        return Documento(etiquetas, req, loja?.Name ?? "LopesTech").GeneratePdf();
    }

    /// <summary>O desenho das etiquetas (separado para se poder ver em imagem nos testes).</summary>
    public static Document Documento(IReadOnlyList<ArtigoDto> etiquetas, EtiquetasRequest req, string marca)
    {
        var largura = (float)req.LarguraMm;
        var altura = (float)req.AlturaMm;
        const float margem = 1.2f;
        var pequena = altura < 20; // rolos estreitos: só preço, nome numa linha e barras
        var topoMm = pequena ? 3.2f : 4.6f;
        var nomeMm = pequena ? 2.4f : 5.2f;
        var rodapeMm = pequena ? 2.2f : 2.6f;
        var barrasMm = Math.Max(3f, altura - 2 * margem - topoMm - nomeMm - rodapeMm - 0.8f);
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
                    page.DefaultTextStyle(t => t.FontSize(pequena ? 5.5f : 6.5f).FontColor(Colors.Black).LineHeight(1f));
                    page.Content().Column(col =>
                    {
                        col.Item().Height(topoMm, Unit.Millimetre).Row(row =>
                        {
                            row.RelativeItem().AlignBottom().Text(marca).Bold().FontSize(pequena ? 5.5f : 7).LetterSpacing(0.02f);
                            if (req.MostrarPreco && a.PrecoEfetivoCents is { } p)
                                row.AutoItem().AlignRight().AlignBottom()
                                    .Text((p / 100m).ToString("0.00", pt) + " €").Bold().FontSize(pequena ? 8 : 12);
                        });
                        col.Item().Height(nomeMm, Unit.Millimetre).AlignMiddle()
                            .Text(a.NomeFatura ?? a.Nome).SemiBold().ClampLines(pequena ? 1 : 2);
                        col.Item().Height(barrasMm, Unit.Millimetre).PaddingTop(0.4f, Unit.Millimetre).Element(e =>
                        {
                            if (Ean13.Valido(a.CodigoBarras)) e.Svg(Ean13.Svg(a.CodigoBarras, alturaModulos)).FitArea();
                        });
                        col.Item().Height(rodapeMm, Unit.Millimetre).Row(row =>
                        {
                            row.RelativeItem().AlignCenter().AlignBottom().Text(a.CodigoBarras).FontSize(pequena ? 4.8f : 5.5f).LetterSpacing(0.12f);
                            row.AutoItem().AlignBottom().Text(a.Sku).FontSize(pequena ? 4.8f : 5.5f).Bold();
                        });
                    });
                });
            }
        });
    }
}
