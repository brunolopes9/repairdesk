using System.Globalization;
using System.Text;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Compras.Import;

namespace RepairDesk.Services.Financas;

public sealed record FinancasImportResultado(int Importadas, int JaExistiam, int Ignoradas, int Fixas, IReadOnlyList<string> Avisos);

public interface IFinancasExcelImporter
{
    Task<FinancasImportResultado> ImportarAsync(Stream xlsx, CancellationToken ct = default);
}

/// <summary>
/// Sprint 634 (Doc 99): importa o histórico do Excel «Gestão Financeira» (folhas Despesas e Fixas).
/// <list type="bullet">
/// <item>Pessoal / Segurança Social / IVA-Estado entram sempre (o Mender não os tinha).</item>
/// <item>Informática / Trading / Geral (despesas de negócio) só entram se não houver já no Mender uma despesa com o
/// mesmo valor a ±3 dias (muitas já vieram das faturas).</item>
/// <item>Camisolas: ignoradas (em espera, sem enquadramento fiscal).</item>
/// <item>Fixas: as despesas cuja descrição contém o texto da fixa ficam marcadas como recorrentes.</item>
/// <item>Os trabalhos de Informática e os payouts NÃO se importam — as vendas do Mender são a fonte.</item>
/// </list>
/// Idempotente: correr outra vez não duplica (mesma data, valor e descrição).
/// </summary>
public sealed class FinancasExcelImporter : IFinancasExcelImporter
{
    private readonly IDespesaRepository _despesas;
    private readonly ITenantContext _tenant;

    public FinancasExcelImporter(IDespesaRepository despesas, ITenantContext tenant)
    {
        _despesas = despesas;
        _tenant = tenant;
    }

    public async Task<FinancasImportResultado> ImportarAsync(Stream xlsx, CancellationToken ct = default)
    {
        var x = XlsxReader.Read(xlsx);
        var folha = x.Sheet("Despesas");
        if (folha.Count == 0) throw new Core.Exceptions.ValidationException("sem_despesas", "O ficheiro não tem a folha «Despesas» do Excel de gestão.");

        var fixas = x.Sheet("Fixas")
            .Where(kv => kv.Key >= 5 && XlsxReader.Text(kv.Value, "A") is { Length: > 1 })
            .Select(kv => Norm(XlsxReader.Text(kv.Value, "A")!))
            .ToList();

        int importadas = 0, existiam = 0, ignoradas = 0, nFixas = 0;
        var avisos = new List<string>();
        foreach (var (n, row) in folha.Where(kv => kv.Key >= 8).OrderBy(kv => kv.Key))
        {
            var data = XlsxReader.Date(row, "A");
            var desc = XlsxReader.Text(row, "B");
            var valor = XlsxReader.Number(row, "E");
            if (data is null || desc is null || valor is not > 0) continue;
            var cat = XlsxReader.Text(row, "C");
            var pertence = XlsxReader.Text(row, "D");
            var natureza = Norm(XlsxReader.Text(row, "F") ?? "").StartsWith("invest") ? DespesaNatureza.Investimento : DespesaNatureza.Gasto;
            var cents = (int)Math.Round(valor.Value * 100m, MidpointRounding.AwayFromZero);

            var (ambito, afetacao, ok) = Mapear(pertence);
            if (!ok)
            {
                ignoradas++;
                avisos.Add($"Linha {n}: «{desc}» ({pertence ?? "sem «pertence a»"}) ficou de fora.");
                continue;
            }
            var categoria = Categoria(cat);
            if (categoria is DespesaCategoria.SegurancaSocial) ambito = DespesaAmbito.Obrigacao;
            if (Norm(pertence!).StartsWith("iva")) { ambito = DespesaAmbito.Obrigacao; categoria = DespesaCategoria.PagamentoIva; }

            var (perto, _) = await _despesas.SearchAsync(null, null, null, false, false, data.Value.AddDays(-3), data.Value.AddDays(4), null, 1, 500, ct);
            var mesmo = perto.Any(d => d.ValorCents == cents
                && (ambito == DespesaAmbito.Negocio ? true : d.Data.Date == data.Value.Date && Norm(d.Descricao) == Norm(desc)));
            if (mesmo)
            {
                existiam++;
                continue;
            }

            var recorrente = fixas.Any(f => Norm(desc).Contains(f));
            if (recorrente) nFixas++;
            await _despesas.AddAsync(new Despesa
            {
                TenantId = _tenant.TenantId ?? Guid.Empty,
                Descricao = desc.Length > 200 ? desc[..200] : desc,
                Categoria = categoria,
                ValorCents = cents,
                Data = data.Value,
                Notas = $"Importado do Excel de gestão ({data.Value:MM/yyyy}){(XlsxReader.Text(row, "H") is { } nota ? " — " + nota : "")}",
                Ambito = ambito,
                Afetacao = afetacao,
                Natureza = natureza,
                IsRecorrente = recorrente,
                PeriodicidadeMeses = recorrente ? 1 : null,
                DeducaoPercent = ambito == DespesaAmbito.Negocio ? 100 : 0,
            }, ct);
            importadas++;
        }
        await _despesas.SaveAsync(ct);
        return new FinancasImportResultado(importadas, existiam, ignoradas, nFixas, avisos);
    }

    private static (DespesaAmbito Ambito, AfetacaoIva Afetacao, bool Ok) Mapear(string? pertence) => Norm(pertence ?? "") switch
    {
        var p when p.StartsWith("inform") => (DespesaAmbito.Negocio, AfetacaoIva.LopesTech, true),
        var p when p.StartsWith("trading") => (DespesaAmbito.Negocio, AfetacaoIva.Trading, true),
        var p when p.StartsWith("geral") => (DespesaAmbito.Negocio, AfetacaoIva.Mista, true),
        var p when p.StartsWith("pessoal") => (DespesaAmbito.Pessoal, AfetacaoIva.LopesTech, true),
        var p when p.StartsWith("iva") => (DespesaAmbito.Obrigacao, AfetacaoIva.LopesTech, true),
        _ => (DespesaAmbito.Negocio, AfetacaoIva.LopesTech, false), // Camisolas (em espera) ou vazio
    };

    private static DespesaCategoria Categoria(string? texto) => Norm(texto ?? "") switch
    {
        var c when c.StartsWith("alimenta") => DespesaCategoria.Alimentacao,
        var c when c.StartsWith("cafe") || c.Contains("restaurante") => DespesaCategoria.CafeRestaurante,
        var c when c.StartsWith("casa") => DespesaCategoria.Casa,
        var c when c.StartsWith("transporte") || c.Contains("combust") => DespesaCategoria.Transporte,
        var c when c.StartsWith("ginasio") => DespesaCategoria.Ginasio,
        var c when c.StartsWith("suplement") => DespesaCategoria.Suplementacao,
        var c when c.StartsWith("lazer") => DespesaCategoria.Lazer,
        var c when c.StartsWith("saude") => DespesaCategoria.Saude,
        var c when c.StartsWith("vestu") || c.StartsWith("roupa") => DespesaCategoria.Vestuario,
        var c when c.StartsWith("seguranca social") => DespesaCategoria.SegurancaSocial,
        var c when c.StartsWith("software") || c.StartsWith("subscri") => DespesaCategoria.Subscricoes,
        var c when c.StartsWith("ferramentas ia") => DespesaCategoria.FerramentasIa,
        var c when c.StartsWith("ferrament") => DespesaCategoria.Ferramenta,
        var c when c.StartsWith("vps") || c.Contains("copier") => DespesaCategoria.VpsCopier,
        var c when c.Contains("prop") || c.Contains("challenge") => DespesaCategoria.PropFirms,
        var c when c.StartsWith("material") => DespesaCategoria.Material,
        var c when c.StartsWith("marketing") || c.StartsWith("publicidade") => DespesaCategoria.Marketing,
        var c when c.StartsWith("comunica") => DespesaCategoria.Comunicacoes,
        var c when c == "iva" => DespesaCategoria.PagamentoIva,
        _ => DespesaCategoria.Outro,
    };

    /// <summary>Minúsculas sem acentos (o Excel tem «Saúde», «Ginásio», «Café/Restaurante»…).</summary>
    public static string Norm(string s)
    {
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
