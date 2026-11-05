using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Reparacoes;

namespace RepairDesk.Services.Documents;

public sealed record OrdemAssinatura(byte[] Png, DateTime Em, bool PeloLink);

public sealed record OrdemReparacaoData(
    string Numero,
    OrcamentoEmissor Loja,
    string Cliente,
    string? Telefone,
    string? Morada,
    string? Nif,
    DateTime Entrada,
    DateTime? Previsao,
    DateTime? Saida,
    string Equipamento,
    string? Imei,
    string Avaria,
    SenhaTipo SenhaTipo,
    /// <summary>Null na via do cliente (a senha nunca lá aparece).</summary>
    string? Senha,
    bool? PermiteTestes,
    string? EstadoFisico,
    string? Acessorios,
    IReadOnlyList<OrcamentoLinha> Linhas,
    int TotalCents,
    IReadOnlyList<ChecklistItemDto> Checklist,
    string? Termos,
    string Aviso,
    OrdemAssinatura? AssinaturaCliente,
    OrdemAssinatura? AssinaturaTecnico,
    string? PortalUrl,
    /// <summary>Nome fiscal do titular, quando difere da marca (vai em letra pequena com o NIF).</summary>
    string? NomeFiscal = null);

/// <summary>
/// Sprint 595: ordem de reparação em A4 para cortar ao meio — em cima a via da loja (com a senha), em baixo a via do
/// cliente (sem a senha, com o aviso de levantamento). 2.ª folha: a checklist de testes, também em 2 vias.
/// Cada via encolhe para caber em meia folha (ScaleToFit), por mais linhas que a reparação tenha.
/// </summary>
public static class OrdemReparacaoPdfRenderer
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");
    private const float MeiaFolha = 136; // mm por via (A4 = 297 mm, menos margens e a linha de corte)

    public static byte[] Render(OrdemReparacaoData d, bool soCliente) => Documento(d, soCliente).GeneratePdf();

    public static Document Documento(OrdemReparacaoData d, bool soCliente)
    {
        var cor = string.IsNullOrWhiteSpace(d.Loja.PrimaryColor) ? "#0f766e" : d.Loja.PrimaryColor!;
        return Document.Create(doc =>
        {
            doc.Page(p => Folha(p, col =>
            {
                if (!soCliente)
                {
                    col.Item().Height(MeiaFolha, Unit.Millimetre).ScaleToFit().Element(c => Ordem(c, d, cor, viaCliente: false));
                    Corte(col);
                }
                col.Item().Height(MeiaFolha, Unit.Millimetre).ScaleToFit().Element(c => Ordem(c, d, cor, viaCliente: true));
            }));
            doc.Page(p => Folha(p, col =>
            {
                if (!soCliente)
                {
                    col.Item().Height(MeiaFolha, Unit.Millimetre).ScaleToFit().Element(c => Checklist(c, d, cor, viaCliente: false));
                    Corte(col);
                }
                col.Item().Height(MeiaFolha, Unit.Millimetre).ScaleToFit().Element(c => Checklist(c, d, cor, viaCliente: true));
            }));
        });
    }

    private static void Folha(PageDescriptor p, Action<ColumnDescriptor> conteudo)
    {
        p.Size(PageSizes.A4);
        p.Margin(8, Unit.Millimetre);
        p.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(8.5f).FontColor("#18181b"));
        p.Content().Column(conteudo);
    }

    private static void Corte(ColumnDescriptor col)
        => col.Item().PaddingVertical(2, Unit.Millimetre).AlignCenter().Text("- - - - - - - - - - - - - - - - - - - -  corte aqui  - - - - - - - - - - - - - - - - - - - -")
            .FontSize(7).FontColor(Colors.Grey.Medium);

    private static void Cabecalho(IContainer c, OrdemReparacaoData d, string cor, string titulo, bool viaCliente)
    {
        c.Row(r =>
        {
            r.RelativeItem().Column(col =>
            {
                col.Item().Text(d.Loja.Nome).FontSize(13).Bold().FontColor(cor);
                var contactos = string.Join(" · ", new[] { d.Loja.Telefone, d.Loja.Email, d.Loja.Website }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (contactos.Length > 0) col.Item().Text(contactos).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                var morada = string.Join(", ", new[] { d.Loja.Morada, d.Loja.CodigoPostal, d.Loja.Localidade }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (morada.Length > 0) col.Item().Text(morada).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                var fiscal = string.Join(" · ", new[] { d.NomeFiscal != d.Loja.Nome ? d.NomeFiscal : null, d.Loja.Nif is null ? null : $"NIF {d.Loja.Nif}" }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (fiscal.Length > 0) col.Item().Text(fiscal).FontSize(6.5f).FontColor(Colors.Grey.Medium);
            });
            r.AutoItem().AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(titulo).FontSize(11).Bold();
                col.Item().AlignRight().Text($"Nº {d.Numero}").FontSize(10).Bold().FontColor(cor);
                col.Item().AlignRight().Text(viaCliente ? "VIA DO CLIENTE" : "VIA DA LOJA").FontSize(7).Bold().FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void Ordem(IContainer c, OrdemReparacaoData d, string cor, bool viaCliente)
    {
        c.Border(0.6f).BorderColor(Colors.Grey.Lighten1).Padding(3, Unit.Millimetre).Column(col =>
        {
            col.Spacing(4);
            col.Item().Element(x => Cabecalho(x, d, cor, "ORDEM DE REPARAÇÃO", viaCliente));
            col.Item().LineHorizontal(0.6f).LineColor(Colors.Grey.Lighten2);
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(cc =>
                {
                    Secao(cc, "CLIENTE", cor);
                    Campo(cc, "Nome", d.Cliente);
                    Campo(cc, "Telefone", d.Telefone);
                    Campo(cc, "Morada", d.Morada);
                    if (d.Nif is not null) Campo(cc, "NIF", d.Nif);
                    Secao(cc, "DATAS", cor);
                    Campo(cc, "Entrada", d.Entrada.ToString("dd/MM/yyyy HH:mm", Pt));
                    Campo(cc, "Previsão", d.Previsao?.ToString("dd/MM/yyyy", Pt) ?? "—");
                    Campo(cc, "Saída", d.Saida?.ToString("dd/MM/yyyy", Pt) ?? "____/____/________");
                });
                r.ConstantItem(6);
                r.RelativeItem().Column(cc =>
                {
                    Secao(cc, "EQUIPAMENTO", cor);
                    Campo(cc, "Equipamento", d.Equipamento);
                    Campo(cc, "IMEI / Nº série", d.Imei ?? "—");
                    Campo(cc, "Avaria", d.Avaria);
                    Campo(cc, "Permite testes", d.PermiteTestes switch { true => "Sim", false => "Não", null => "—" });
                    if (d.EstadoFisico is not null) Campo(cc, "Estado físico", d.EstadoFisico);
                    if (d.Acessorios is not null) Campo(cc, "Acessórios", d.Acessorios);
                    Senha(cc, d, viaCliente);
                });
            });

            col.Item().Column(cc =>
            {
                Secao(cc, "REPARAÇÃO A REALIZAR", cor);
                if (d.Linhas.Count == 0) cc.Item().Text("A definir após diagnóstico.").FontColor(Colors.Grey.Darken1);
                foreach (var l in d.Linhas)
                    cc.Item().Row(r =>
                    {
                        r.RelativeItem().Text(l.Descricao);
                        r.AutoItem().Text(Eur(l.ValorCents));
                    });
                cc.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text("Preço da reparação (c/ IVA)").Bold();
                    r.AutoItem().Text(d.TotalCents > 0 ? Eur(d.TotalCents) : "a orçamentar").Bold();
                });
            });

            col.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem().Element(x => Assinatura(x, "Assinatura do técnico", d.AssinaturaTecnico));
                r.ConstantItem(10);
                r.RelativeItem().Element(x => Assinatura(x, "Assinatura do cliente", d.AssinaturaCliente));
            });

            if (viaCliente)
            {
                col.Item().Text(d.Aviso).FontSize(7).Italic().FontColor(Colors.Grey.Darken2);
                if (d.PortalUrl is not null)
                    col.Item().Text(t =>
                    {
                        t.Span("Acompanhe a reparação: ").FontSize(7).FontColor(Colors.Grey.Darken1);
                        t.Span(d.PortalUrl).FontSize(7).FontColor(cor);
                    });
            }
            else if (!string.IsNullOrWhiteSpace(d.Termos))
            {
                col.Item().Text(d.Termos!.Length > 600 ? d.Termos[..600] + "…" : d.Termos).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void Senha(ColumnDescriptor cc, OrdemReparacaoData d, bool viaCliente)
    {
        var tipo = d.SenhaTipo switch
        {
            SenhaTipo.Pin => "PIN",
            SenhaTipo.Alfanumerica => "Palavra-passe",
            SenhaTipo.Padrao => "Padrão",
            _ => "Sem senha",
        };
        if (viaCliente || d.SenhaTipo == SenhaTipo.Nenhuma)
        {
            Campo(cc, "Senha", d.SenhaTipo == SenhaTipo.Nenhuma ? "Sem senha" : $"{tipo} — registado na via da loja");
            return;
        }
        if (d.SenhaTipo == SenhaTipo.Padrao && d.Senha is not null)
        {
            cc.Item().Row(r =>
            {
                r.ConstantItem(62).Text("Senha (padrão)").FontColor(Colors.Grey.Darken1);
                r.ConstantItem(48).Height(48).Svg(PadraoSvg(d.Senha));
                r.RelativeItem().PaddingLeft(4).AlignBottom().Text(d.Senha).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
            return;
        }
        Campo(cc, $"Senha ({tipo})", d.Senha ?? "____________________");
    }

    /// <summary>Os 9 pontos do padrão com o desenho do cliente (início a verde).</summary>
    internal static string PadraoSvg(string sequencia)
    {
        static (int X, int Y) Ponto(int n) => (12 + (n - 1) % 3 * 24, 12 + (n - 1) / 3 * 24);
        var pontos = sequencia.Split('-').Select(int.Parse).ToList();
        var sb = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 72 72\">");
        for (var n = 1; n <= 9; n++) { var (x, y) = Ponto(n); sb.Append($"<circle cx=\"{x}\" cy=\"{y}\" r=\"3\" fill=\"#a1a1aa\"/>"); }
        sb.Append("<polyline fill=\"none\" stroke=\"#18181b\" stroke-width=\"2.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"");
        sb.Append(string.Join(' ', pontos.Select(n => { var (x, y) = Ponto(n); return $"{x},{y}"; })));
        sb.Append("\"/>");
        var (sx, sy) = Ponto(pontos[0]);
        sb.Append($"<circle cx=\"{sx}\" cy=\"{sy}\" r=\"5\" fill=\"#16a34a\"/></svg>");
        return sb.ToString();
    }

    private static void Checklist(IContainer c, OrdemReparacaoData d, string cor, bool viaCliente)
    {
        c.Border(0.6f).BorderColor(Colors.Grey.Lighten1).Padding(3, Unit.Millimetre).Column(col =>
        {
            col.Spacing(4);
            col.Item().Element(x => Cabecalho(x, d, cor, "CHECKLIST DE TESTES", viaCliente));
            col.Item().Text($"{d.Equipamento}{(d.Imei is null ? "" : $" · IMEI {d.Imei}")} · Cliente: {d.Cliente}").FontSize(8);
            col.Item().Text("OK = funciona · Falha = não funciona · N/T = não testado (não liga, sem senha…)").FontSize(7).FontColor(Colors.Grey.Darken1);
            var metade = (d.Checklist.Count + 1) / 2;
            col.Item().Row(r =>
            {
                r.RelativeItem().Element(x => Tabela(x, d.Checklist.Take(metade)));
                r.ConstantItem(8);
                r.RelativeItem().Element(x => Tabela(x, d.Checklist.Skip(metade)));
            });
            col.Item().Text("Observações: ______________________________________________________________________________________").FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem().Element(x => Assinatura(x, "Assinatura do técnico", d.AssinaturaTecnico));
                r.ConstantItem(10);
                r.RelativeItem().Element(x => Assinatura(x, "Assinatura do cliente", d.AssinaturaCliente));
            });
        });
    }

    private static void Tabela(IContainer c, IEnumerable<ChecklistItemDto> itens)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(38); cd.ConstantColumn(38); });
            t.Header(h =>
            {
                h.Cell().BorderBottom(0.8f).PaddingVertical(2).Text("Teste").Bold().FontSize(7.5f);
                h.Cell().BorderBottom(0.8f).PaddingVertical(2).AlignCenter().Text("Entrada").Bold().FontSize(7.5f);
                h.Cell().BorderBottom(0.8f).PaddingVertical(2).AlignCenter().Text("Saída").Bold().FontSize(7.5f);
            });
            foreach (var i in itens)
            {
                t.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2.5f).Text(i.Label).FontSize(8);
                t.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2.5f).AlignCenter().Element(x => Resultado(x, i.Entrada));
                t.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2.5f).AlignCenter().Element(x => Resultado(x, i.Saida));
            }
        });
    }

    private static void Resultado(IContainer c, string? r)
    {
        var (texto, corTexto) = r switch
        {
            "ok" => ("OK", "#15803d"),
            "falha" => ("Falha", "#b91c1c"),
            "nt" => ("N/T", "#71717a"),
            _ => ("", "#a1a1aa"),
        };
        if (r is null) c.Text("___").FontSize(8).FontColor(corTexto);
        else c.Text(texto).FontSize(8).Bold().FontColor(corTexto);
    }

    private static void Assinatura(IContainer c, string titulo, OrdemAssinatura? a)
    {
        c.Column(col =>
        {
            if (a is not null) col.Item().Height(26).AlignCenter().Image(a.Png).FitArea();
            else col.Item().Height(26);
            col.Item().LineHorizontal(0.6f).LineColor(Colors.Grey.Darken1);
            col.Item().Text(t =>
            {
                t.Span(titulo).FontSize(7).FontColor(Colors.Grey.Darken1);
                if (a is not null)
                    t.Span($" · assinada {(a.PeloLink ? "no link" : "digitalmente")} em {a.Em.ToLocalTime():dd/MM/yyyy HH:mm}").FontSize(6.5f).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private static void Secao(ColumnDescriptor col, string titulo, string cor)
        => col.Item().PaddingTop(2).Text(titulo).FontSize(7).Bold().FontColor(cor);

    private static void Campo(ColumnDescriptor col, string label, string? valor)
        => col.Item().Row(r =>
        {
            r.ConstantItem(62).Text(label).FontColor(Colors.Grey.Darken1);
            r.RelativeItem().Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor);
        });

    private static string Eur(int cents) => (cents / 100m).ToString("#,##0.00", Pt) + " €";
}
