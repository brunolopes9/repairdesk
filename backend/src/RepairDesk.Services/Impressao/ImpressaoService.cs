using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Moloni;
using RepairDesk.Services.Vendas;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace RepairDesk.Services.Impressao;

/// <summary>O que vai no PDF: a lista (com totais) seguida das faturas, sÃ³ as faturas, ou sÃ³ a lista.</summary>
public enum ConteudoImpressao { Tudo, Faturas, Lista }

/// <summary>Filtros da lista de despesas (os mesmos do ecrÃ£).</summary>
public sealed record DespesaFiltroImpressao(
    string? Query, DespesaCategoria? Categoria, bool? IsRecorrente, DateTime? De, DateTime? Ate);

public interface IImpressaoService
{
    Task<(byte[] Pdf, string Nome)> VendasAsync(VendaFiltro filtro, ConteudoImpressao conteudo, CancellationToken ct = default);
    Task<(byte[] Pdf, string Nome)> ComprasAsync(CompraFiltro filtro, ConteudoImpressao conteudo, CancellationToken ct = default);
    Task<(byte[] Pdf, string Nome)> DespesasAsync(DespesaFiltroImpressao filtro, ConteudoImpressao conteudo, CancellationToken ct = default);
}

/// <summary>
/// Sprint 593: imprimir de uma vez â€” as vendas de hoje, as compras do mÃªs, as despesas de um fornecedorâ€¦ Usa os
/// mesmos filtros das listas e devolve UM PDF: primeiro uma pÃ¡gina com a lista (nÂº, data, cliente/fornecedor,
/// documento, IVA e total, com os totais â€” para a contabilidade), depois cada fatura pela ordem das datas.
/// Documentos sem PDF ficam na lista assinalados. MÃ¡ximo <see cref="Maximo"/> documentos por impressÃ£o.
/// </summary>
public sealed class ImpressaoService : IImpressaoService
{
    public const int Maximo = 300;
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    private readonly IVendaService _vendas;
    private readonly IVendaFaturaService _faturasVenda;
    private readonly IMoloniService _moloni;
    private readonly ICompraService _compras;
    private readonly IDespesaService _despesas;
    private readonly ISupplierInvoiceImportService _faturasFornecedor;
    private readonly IFornecedorRepository _fornecedores;

    public ImpressaoService(IVendaService vendas, IVendaFaturaService faturasVenda, IMoloniService moloni, ICompraService compras,
        IDespesaService despesas, ISupplierInvoiceImportService faturasFornecedor, IFornecedorRepository fornecedores)
    {
        _vendas = vendas;
        _faturasVenda = faturasVenda;
        _moloni = moloni;
        _compras = compras;
        _despesas = despesas;
        _faturasFornecedor = faturasFornecedor;
        _fornecedores = fornecedores;
    }

    private sealed record Linha(string Numero, DateTime Data, string Entidade, string Documento, decimal? Iva, decimal Total,
        Func<CancellationToken, Task<byte[]?>>? Pdf, string? SemPdf);

    public async Task<(byte[] Pdf, string Nome)> VendasAsync(VendaFiltro filtro, ConteudoImpressao conteudo, CancellationToken ct = default)
    {
        filtro = filtro with { Ordem = new Ordenacao("data", false) };
        var vendas = await TodasAsync((p, n) => _vendas.SearchAsync(filtro, p, n, ct), 100);
        var linhas = vendas.Select(v => new Linha(
            $"#{v.Numero}", v.InvoiceEmittedAt ?? v.Data, v.Cliente?.Nome ?? "Consumidor final",
            v.InvoiceNumber ?? (v.Estado == VendaEstado.Cancelada ? "cancelada" : "sem fatura"),
            v.IvaCents / 100m, v.TotalCents / 100m,
            v.TemFaturaPdf ? async c => (await _faturasVenda.PdfAsync(v.Id, c)).Pdf
                : v.MoloniDocumentoTipo is not null ? async c => (await _moloni.PdfAsync(v.Id, "fatura", c)).Pdf
                : null,
            v.InvoiceNumber is null ? "sem fatura" : null)).ToList();
        var titulo = "Vendas" + Periodo(filtro.FromUtc, filtro.ToUtc?.AddDays(-1)) + Pesquisa(filtro.Q);
        return (await MontarAsync(titulo, "Cliente", linhas, conteudo, ct), Nome("Vendas", filtro.FromUtc, filtro.ToUtc?.AddDays(-1)));
    }

    public async Task<(byte[] Pdf, string Nome)> ComprasAsync(CompraFiltro filtro, ConteudoImpressao conteudo, CancellationToken ct = default)
    {
        filtro = filtro with { Ordem = new Ordenacao("data", false) };
        var compras = await TodasAsync((p, n) => _compras.SearchAsync(filtro, p, n, ct), 200);
        var linhas = compras.Select(c => new Linha(
            c.Numero.ToString(Pt), c.Data, c.FornecedorNome,
            c.NumeroFatura ?? (c.NumerosEncomenda.Count > 0 ? $"Enc. {string.Join(", ", c.NumerosEncomenda)}" : "sem referÃªncia"),
            null, c.TotalDocumento ?? c.TotalCalculado,
            c.SupplierInvoiceImportId is { } imp ? async k => await _faturasFornecedor.GetPdfAsync(imp, k) : null,
            c.FaturaEmFalta ? "fatura em falta" : null)).ToList();
        var fornecedor = filtro.FornecedorId is { } fid ? (await _fornecedores.FindByIdAsync(fid, ct))?.Name : null;
        var titulo = "Compras" + (fornecedor is null ? "" : $" Â· {fornecedor}") + Periodo(filtro.DeUtc, filtro.AteUtc?.AddDays(-1)) + Pesquisa(filtro.Query);
        return (await MontarAsync(titulo, "Fornecedor", linhas, conteudo, ct), Nome("Compras", filtro.DeUtc, filtro.AteUtc?.AddDays(-1)));
    }

    public async Task<(byte[] Pdf, string Nome)> DespesasAsync(DespesaFiltroImpressao filtro, ConteudoImpressao conteudo, CancellationToken ct = default)
    {
        var despesas = await TodasAsync((p, n) => _despesas.SearchAsync(filtro.Query, filtro.Categoria, null, false, false,
            filtro.De, filtro.Ate, filtro.IsRecorrente, p, n, ct), 100);
        var linhas = despesas.OrderBy(d => d.Data).ThenBy(d => d.Numero).Select(d => new Linha(
            d.Numero.ToString(Pt), d.Data, d.Fornecedor ?? "â€”", d.NumeroEncomenda ?? d.Descricao,
            d.ReverseCharge ? 0 : d.IvaCents / 100m, d.ValorCents / 100m,
            d.SupplierInvoiceImportId is { } imp ? async k => await _faturasFornecedor.GetPdfAsync(imp, k) : null,
            null)).ToList();
        var titulo = "Despesas" + Periodo(filtro.De, filtro.Ate) + Pesquisa(filtro.Query);
        return (await MontarAsync(titulo, "Fornecedor", linhas, conteudo, ct), Nome("Despesas", filtro.De, filtro.Ate));
    }

    // ---------- montagem ----------

    /// <summary>Junta todas as pÃ¡ginas da pesquisa (os serviÃ§os paginam), atÃ© ao mÃ¡ximo.</summary>
    private static async Task<List<T>> TodasAsync<T>(Func<int, int, Task<PagedResult<T>>> pagina, int tamanho)
    {
        var todas = new List<T>();
        for (var p = 1; ; p++)
        {
            var r = await pagina(p, tamanho);
            if (r.Total > Maximo)
                throw new ValidationException("demasiados_documentos",
                    $"SÃ£o {r.Total} documentos â€” o mÃ¡ximo por impressÃ£o Ã© {Maximo}. Escolhe um perÃ­odo mais curto (ex.: um mÃªs).");
            todas.AddRange(r.Items);
            if (r.Items.Count == 0 || todas.Count >= r.Total) return todas;
        }
    }

    private static async Task<byte[]> MontarAsync(string titulo, string colunaEntidade, List<Linha> linhas, ConteudoImpressao conteudo, CancellationToken ct)
    {
        if (linhas.Count == 0) throw new ValidationException("sem_documentos", "NÃ£o hÃ¡ documentos com estes filtros.");
        var partes = new List<byte[]>();
        var notas = new string?[linhas.Count];
        if (conteudo != ConteudoImpressao.Lista)
        {
            for (var i = 0; i < linhas.Count; i++)
            {
                var l = linhas[i];
                if (l.Pdf is null) { notas[i] = l.SemPdf ?? "sem PDF"; continue; }
                byte[]? bytes;
                try { bytes = await l.Pdf(ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { bytes = null; }
                var pdf = bytes is null ? null : ComoPdf(bytes);
                if (pdf is null) { notas[i] = "PDF indisponÃ­vel"; continue; }
                partes.Add(pdf);
            }
        }
        else
        {
            for (var i = 0; i < linhas.Count; i++) notas[i] = linhas[i].Pdf is null ? linhas[i].SemPdf ?? "sem PDF" : null;
        }

        var resultado = new List<byte[]>();
        if (conteudo != ConteudoImpressao.Faturas || partes.Count == 0)
            resultado.Add(Lista(titulo, colunaEntidade, linhas, notas, conteudo != ConteudoImpressao.Lista));
        resultado.AddRange(partes);
        return resultado.Count == 1 ? resultado[0] : PdfMerger.Merge(resultado.ToArray());
    }

    /// <summary>PDF tal como estÃ¡; uma imagem (foto da fatura) vira uma pÃ¡gina A4. Null se nÃ£o se consegue abrir.</summary>
    private static byte[]? ComoPdf(byte[] bytes)
    {
        if (bytes.Length > 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F')
        {
            try
            {
                using var doc = PdfDocument.Open(bytes);
                return doc.NumberOfPages > 0 ? bytes : null;
            }
            catch { return null; }
        }
        try
        {
            return Document.Create(c => c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(1, Unit.Centimetre);
                p.Content().AlignCenter().AlignMiddle().Image(bytes).FitArea();
            })).GeneratePdf();
        }
        catch { return null; }
    }

    private static byte[] Lista(string titulo, string colunaEntidade, List<Linha> linhas, string?[] notas, bool comFaturas)
    {
        var temIva = linhas.Any(l => l.Iva is not null);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(9).FontColor("#18181b"));
            page.Header().Column(col =>
            {
                col.Item().Text(titulo).FontSize(16).Bold();
                var semPdf = notas.Count(n => n is not null);
                col.Item().Text($"{linhas.Count} documento(s)" + (comFaturas
                    ? semPdf == 0 ? " â€” as faturas seguem nas pÃ¡ginas seguintes, pela mesma ordem." : $" â€” {semPdf} sem PDF (assinalados); as outras seguem a seguir."
                    : "")).FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingTop(12).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(38);
                    c.ConstantColumn(58);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.8f);
                    if (temIva) c.ConstantColumn(58);
                    c.ConstantColumn(64);
                });
                table.Header(h =>
                {
                    Th(h.Cell(), "NÂº");
                    Th(h.Cell(), "Data");
                    Th(h.Cell(), colunaEntidade);
                    Th(h.Cell(), "Documento");
                    if (temIva) Th(h.Cell(), "IVA", true);
                    Th(h.Cell(), "Total", true);
                });
                for (var i = 0; i < linhas.Count; i++)
                {
                    var l = linhas[i];
                    Td(table.Cell(), l.Numero);
                    Td(table.Cell(), l.Data.ToString("dd/MM/yyyy", Pt));
                    Td(table.Cell(), l.Entidade);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(t =>
                    {
                        t.Span(l.Documento).FontSize(8);
                        if (notas[i] is { } n) t.Span($"  ({n})").FontSize(7).FontColor(Colors.Orange.Darken2);
                    });
                    if (temIva) Td(table.Cell(), l.Iva is { } iva ? Eur(iva) : "â€”", true);
                    Td(table.Cell(), Eur(l.Total), true);
                }
                table.Cell().ColumnSpan(4).PaddingTop(6).Text("Total").Bold();
                if (temIva) table.Cell().PaddingTop(6).AlignRight().Text(Eur(linhas.Sum(l => l.Iva ?? 0))).Bold();
                table.Cell().PaddingTop(6).AlignRight().Text(Eur(linhas.Sum(l => l.Total))).Bold();
            });
            page.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Impresso em {DateTime.Now:dd/MM/yyyy HH:mm} Â· Mender").FontSize(7).FontColor(Colors.Grey.Darken1);
                r.AutoItem().Text(t => { t.CurrentPageNumber().FontSize(7); t.Span(" / ").FontSize(7); t.TotalPages().FontSize(7); });
            });
        })).GeneratePdf();
    }

    private static void Th(IContainer c, string texto, bool direita = false)
    {
        var cell = c.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(3);
        (direita ? cell.AlignRight() : cell).Text(texto).Bold().FontSize(8);
    }

    private static void Td(IContainer c, string texto, bool direita = false)
    {
        var cell = c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(3);
        (direita ? cell.AlignRight() : cell).Text(texto).FontSize(8);
    }

    private static string Eur(decimal v) => v.ToString("#,##0.00", Pt) + " â‚¬";

    private static string Periodo(DateTime? de, DateTime? ate) => (de, ate) switch
    {
        (null, null) => "",
        ({ } a, { } b) when a.Date == b.Date => $" Â· {a:dd/MM/yyyy}",
        ({ } a, { } b) => $" Â· {a:dd/MM/yyyy} a {b:dd/MM/yyyy}",
        ({ } a, null) => $" Â· desde {a:dd/MM/yyyy}",
        (null, { } b) => $" Â· atÃ© {b:dd/MM/yyyy}",
    };

    private static string Pesquisa(string? q) => string.IsNullOrWhiteSpace(q) ? "" : $" Â· \"{q.Trim()}\"";

    private static string Nome(string o, DateTime? de, DateTime? ate)
        => o + (de is { } a ? $"_{a:yyyy-MM-dd}" : "") + (ate is { } b && b.Date != de?.Date ? $"_a_{b:yyyy-MM-dd}" : "") + ".pdf";
}
