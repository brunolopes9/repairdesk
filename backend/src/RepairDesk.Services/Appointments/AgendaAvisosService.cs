using System.Globalization;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Services.Push;

namespace RepairDesk.Services.Appointments;

public sealed record AgendaAvisosResultado(int Lembretes, int ResumosDia, int ResumosSemana, int AvisosPecas = 0);

public interface IAgendaAvisosService
{
    /// <summary>Uma passagem (o trabalho de fundo chama de minuto a minuto). Idempotente.</summary>
    Task<AgendaAvisosResultado> ProcessarAsync(DateTime agoraUtc, CancellationToken ct = default);
}

/// <summary>
/// Sprint 611: avisos da agenda no telemóvel (push de staff):
/// - lembrete ~1 hora antes de cada agendamento (uma vez; reagendar volta a avisar);
/// - resumo do dia às 08:00 (hora de Lisboa) se houver agendamentos nesse dia;
/// - resumo da semana à segunda-feira às 08:00 se houver agendamentos na semana.
/// Os resumos ficam registados em <see cref="AgendaAviso"/> para nunca saírem duas vezes.
/// </summary>
public sealed class AgendaAvisosService : IAgendaAvisosService
{
    public static readonly TimeSpan Antecedencia = TimeSpan.FromMinutes(60);
    private const int HoraResumo = 8;
    /// <summary>Se o servidor esteve parado às 08:00, o resumo ainda sai até às 10:00 — depois já não faz sentido.</summary>
    private const int HoraLimiteResumo = 10;
    private const int HoraPecas1 = 10 * 60 + 40;
    private const int HoraPecas2 = 11 * 60 + 15;
    private const int LimitePecas = 11 * 60 + 30;
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");
    private static readonly TimeZoneInfo Lisboa = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly IAgendaAvisosRepository _repo;
    private readonly IStaffPushQueue _push;

    public AgendaAvisosService(IAgendaAvisosRepository repo, IStaffPushQueue push)
    {
        _repo = repo;
        _push = push;
    }

    public async Task<AgendaAvisosResultado> ProcessarAsync(DateTime agoraUtc, CancellationToken ct = default)
    {
        var lembretes = await LembretesAsync(agoraUtc, ct);
        var local = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, Lisboa);
        var semana = 0;
        var dia = 0;
        if (local.Hour is >= HoraResumo and < HoraLimiteResumo)
        {
            if (local.DayOfWeek == DayOfWeek.Monday) semana = await ResumoSemanaAsync(local, ct);
            dia = await ResumoDiaAsync(local, ct);
        }
        var pecas = 0;
        if (local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            var minuto = local.Hour * 60 + local.Minute;
            if (minuto is >= HoraPecas1 and < HoraPecas2) pecas = await AvisoPecasAsync(local, "pecas", false, ct);
            else if (minuto is >= HoraPecas2 and < LimitePecas) pecas = await AvisoPecasAsync(local, "pecas2", true, ct);
        }
        return new AgendaAvisosResultado(lembretes, dia, semana, pecas);
    }

    private async Task<int> LembretesAsync(DateTime agoraUtc, CancellationToken ct)
    {
        // Não avisa de agendamentos que já começaram há mais de 5 min (ex.: servidor parado).
        var proximos = await _repo.SemLembreteEntreAsync(agoraUtc.AddMinutes(-5), agoraUtc + Antecedencia, ct);
        foreach (var a in proximos)
        {
            var faltam = (int)Math.Round((a.ScheduledAt - agoraUtc).TotalMinutes);
            var quando = faltam <= 1 ? "agora" : $"daqui a {faltam} min";
            await _push.EnqueueAsync(new StaffPushJob(
                a.TenantId,
                $"⏰ {Hora(a.ScheduledAt)} {a.Nome} — {quando}",
                OQue(a) ?? "Agendamento",
                "/agendamentos",
                $"agendamento-{a.Id}"), ct);
            a.LembreteEnviadoEm = agoraUtc;
        }
        if (proximos.Count > 0) await _repo.SaveAsync(ct);
        return proximos.Count;
    }

    /// <summary>
    /// Sprint 618 (Doc 98 F3): os fornecedores aceitam encomendas para o próprio dia até às 11:30.
    /// 10:40 — lista do que falta encomendar; 11:15 — último aviso se ainda houver. Só em dias úteis e só se houver peças.
    /// </summary>
    private async Task<int> AvisoPecasAsync(DateTime local, string prefixo, bool ultimo, CancellationToken ct)
    {
        var chave = $"{prefixo}:{local:yyyy-MM-dd}";
        var pecas = await _repo.PecasPorEncomendarAsync(ct);
        var enviados = 0;
        foreach (var grupo in pecas.GroupBy(p => p.TenantId))
        {
            if (!await Marcar(grupo.Key, chave, ct)) continue;
            var lista = grupo.ToList();
            var n = lista.Sum(p => p.Quantidade);
            const int max = 5;
            var corpo = string.Join("\n", lista.Take(max).Select(p =>
                $"{(p.Quantidade > 1 ? $"{p.Quantidade}× " : "")}{p.Descricao} — #{p.VendaNumero}{(p.Equipamento is { Length: > 0 } eq ? $" {eq}" : "")}"));
            if (lista.Count > max) corpo += $"\n+ {lista.Count - max} mais";
            await _push.EnqueueAsync(new StaffPushJob(
                grupo.Key,
                ultimo
                    ? $"⏰ 15 min para o limite: {n} peça(s) por encomendar"
                    : $"📦 {n} peça(s) por encomendar — fornecedor até às 11:30",
                corpo,
                "/vendas?vista=pecas",
                chave) { Insistente = ultimo }, ct);
            enviados++;
        }
        return enviados;
    }

    private async Task<int> ResumoDiaAsync(DateTime local, CancellationToken ct)
    {
        var inicio = local.Date;
        var chave = $"dia:{inicio:yyyy-MM-dd}";
        var itens = await _repo.AtivosEntreAsync(ParaUtc(inicio), ParaUtc(inicio.AddDays(1)), ct);
        var enviados = 0;
        foreach (var grupo in itens.GroupBy(a => a.TenantId))
        {
            if (!await Marcar(grupo.Key, chave, ct)) continue;
            var lista = grupo.ToList();
            await _push.EnqueueAsync(new StaffPushJob(
                grupo.Key,
                lista.Count == 1 ? "📅 Hoje tens 1 agendamento" : $"📅 Hoje tens {lista.Count} agendamentos",
                Linhas(lista, a => $"{Hora(a.ScheduledAt)} {Resumo(a)}"),
                "/agendamentos",
                chave), ct);
            enviados++;
        }
        return enviados;
    }

    private async Task<int> ResumoSemanaAsync(DateTime local, CancellationToken ct)
    {
        var segunda = local.Date;
        var chave = $"semana:{ISOWeek.GetYear(segunda)}-W{ISOWeek.GetWeekOfYear(segunda):00}";
        var itens = await _repo.AtivosEntreAsync(ParaUtc(segunda), ParaUtc(segunda.AddDays(7)), ct);
        var enviados = 0;
        foreach (var grupo in itens.GroupBy(a => a.TenantId))
        {
            if (!await Marcar(grupo.Key, chave, ct)) continue;
            var lista = grupo.ToList();
            await _push.EnqueueAsync(new StaffPushJob(
                grupo.Key,
                lista.Count == 1 ? "🗓️ Esta semana tens 1 agendamento" : $"🗓️ Esta semana tens {lista.Count} agendamentos",
                Linhas(lista, a => $"{Dia(a.ScheduledAt)} {Hora(a.ScheduledAt)} {Resumo(a)}"),
                "/agendamentos",
                chave), ct);
            enviados++;
        }
        return enviados;
    }

    /// <summary>Regista o resumo antes de o enviar; se já existia, não volta a sair.</summary>
    private async Task<bool> Marcar(Guid tenantId, string chave, CancellationToken ct)
    {
        if (await _repo.AvisoJaEnviadoAsync(tenantId, chave, ct)) return false;
        await _repo.AddAvisoAsync(new AgendaAviso { TenantId = tenantId, Chave = chave }, ct);
        await _repo.SaveAsync(ct);
        return true;
    }

    private static string Linhas(IReadOnlyList<Appointment> itens, Func<Appointment, string> linha)
    {
        const int max = 5;
        var texto = string.Join("\n", itens.Take(max).Select(linha));
        return itens.Count > max ? $"{texto}\n+ {itens.Count - max} mais" : texto;
    }

    private static string? OQue(Appointment a)
    {
        var partes = new[] { a.Equipamento, a.Notas }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim());
        var texto = string.Join(" · ", partes);
        return texto.Length == 0 ? null : texto.Length > 120 ? texto[..117] + "…" : texto;
    }

    private static string Resumo(Appointment a) => a.Equipamento is { Length: > 0 } eq ? $"{a.Nome} — {eq}" : a.Nome;
    private static string Hora(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Lisboa).ToString("HH:mm", Pt);
    private static string Dia(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Lisboa).ToString("ddd d", Pt);
    private static DateTime ParaUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Lisboa);
}
