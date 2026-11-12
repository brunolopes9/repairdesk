using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Rotina;

public sealed record RotinaDiaResumoDto(DateOnly Dia, int Total, int Feitos, int Saltados, int Percent, bool Cumprido);
public sealed record RotinaHabitoDto(Guid ItemId, string Titulo, RotinaTipo Tipo, int SequenciaAtual, int MelhorSequencia, int Feitos30, int Previstos30);
public sealed record RotinaHistoricoDto(
    IReadOnlyList<RotinaDiaResumoDto> Dias, IReadOnlyList<RotinaHabitoDto> Habitos,
    int DiasCumpridosSeguidos, int MelhorSequenciaDias, int PercentSemana, int PercentMes);

/// <summary>
/// Sprint 626 (Doc 98): visão panorâmica da rotina — feita para ser justa, não para castigar.
/// <list type="bullet">
/// <item>Conta o que foi feito no dia, a qualquer hora (fazer fora de horas também conta).</item>
/// <item>«Saltado» com motivo (imprevisto, ajudar a família) sai das contas do dia — não é falha.</item>
/// <item>Um dia está cumprido com ≥ 80% do que ficou para fazer.</item>
/// <item>Sequências por hábito contam só os dias em que o hábito estava previsto; o dia de hoje ainda não quebra nada.</item>
/// </list>
/// Puro: recebe itens e registos já carregados.
/// </summary>
public static class RotinaHistorico
{
    public const int PercentCumprido = 80;

    public static RotinaHistoricoDto Calcular(IReadOnlyList<RotinaItem> itens, IReadOnlyList<RotinaRegisto> registos, DateOnly hoje, int dias)
    {
        var porChave = registos.Where(r => r.Estado is not null).ToDictionary(r => (r.RotinaItemId, r.Dia), r => r.Estado!.Value);
        RotinaRegistoEstado? Estado(Guid id, DateOnly d) => porChave.TryGetValue((id, d), out var e) ? e : null;
        bool Previsto(RotinaItem i, DateOnly d) => d >= Inicio(i) && RotinaCalendario.Acontece(i, d);

        var resumo = new List<RotinaDiaResumoDto>();
        for (var d = hoje.AddDays(-(dias - 1)); d <= hoje; d = d.AddDays(1))
        {
            var doDia = itens.Where(i => Previsto(i, d)).ToList();
            var saltados = doDia.Count(i => Estado(i.Id, d) == RotinaRegistoEstado.Saltado);
            var feitos = doDia.Count(i => Estado(i.Id, d) == RotinaRegistoEstado.Feito);
            var conta = doDia.Count - saltados;
            var pct = conta == 0 ? (doDia.Count == 0 ? 0 : 100) : (int)Math.Round(100.0 * feitos / conta);
            resumo.Add(new RotinaDiaResumoDto(d, doDia.Count, feitos, saltados, pct, doDia.Count > 0 && pct >= PercentCumprido));
        }

        // Dias cumpridos seguidos: de ontem para trás (hoje ainda está a decorrer — só soma se já estiver cumprido).
        var comItens = resumo.Where(r => r.Total > 0).ToList();
        var seguidos = 0;
        foreach (var r in comItens.AsEnumerable().Reverse())
        {
            if (r.Dia == hoje && !r.Cumprido) continue;
            if (!r.Cumprido) break;
            seguidos++;
        }
        int melhor = 0, corrente = 0;
        foreach (var r in comItens)
        {
            if (r.Dia == hoje && !r.Cumprido) continue;
            corrente = r.Cumprido ? corrente + 1 : 0;
            melhor = Math.Max(melhor, corrente);
        }

        var habitos = itens.Where(i => i.Ativo).Select(i =>
        {
            var previstos = new List<DateOnly>();
            for (var d = hoje.AddDays(-(dias - 1)); d <= hoje; d = d.AddDays(1))
                if (Previsto(i, d)) previstos.Add(d);
            int atual = 0, best = 0, run = 0;
            foreach (var d in previstos)
            {
                var e = Estado(i.Id, d);
                if (e == RotinaRegistoEstado.Saltado) continue; // imprevisto justificado: não soma nem quebra
                if (e == RotinaRegistoEstado.Feito) { run++; best = Math.Max(best, run); }
                else if (d != hoje) run = 0;
            }
            for (var k = previstos.Count - 1; k >= 0; k--)
            {
                var e = Estado(i.Id, previstos[k]);
                if (e == RotinaRegistoEstado.Saltado) continue;
                if (e == RotinaRegistoEstado.Feito) atual++;
                else if (previstos[k] == hoje) continue;
                else break;
            }
            var ultimos30 = previstos.Where(d => d > hoje.AddDays(-30)).ToList();
            return new RotinaHabitoDto(i.Id, i.Titulo, i.Tipo, atual, best,
                ultimos30.Count(d => Estado(i.Id, d) == RotinaRegistoEstado.Feito),
                ultimos30.Count(d => Estado(i.Id, d) != RotinaRegistoEstado.Saltado));
        }).OrderByDescending(h => h.SequenciaAtual).ThenBy(h => h.Titulo).ToList();

        int Pct(int n)
        {
            var janela = resumo.Where(r => r.Dia > hoje.AddDays(-n) && r.Total > 0).ToList();
            var conta = janela.Sum(r => r.Total - r.Saltados);
            return conta == 0 ? 0 : (int)Math.Round(100.0 * janela.Sum(r => r.Feitos) / conta);
        }

        return new RotinaHistoricoDto(resumo, habitos, seguidos, melhor, Pct(7), Pct(30));
    }

    private static DateOnly Inicio(RotinaItem i) => DateOnly.FromDateTime(RotinaCalendario.ParaLisboa(i.CreatedAt));
}
