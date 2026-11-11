using System.Globalization;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Push;

namespace RepairDesk.Services.Rotina;

public interface IRotinaAvisosService
{
    /// <summary>Uma passagem (o trabalho de fundo chama de minuto a minuto). Idempotente. Devolve os avisos enviados.</summary>
    Task<int> ProcessarAsync(DateTime agoraUtc, CancellationToken ct = default);
}

/// <summary>
/// Sprint 615 (Doc 98): avisos da rotina no telemóvel do próprio utilizador, por níveis:
/// <list type="number">
/// <item>antes (ex.: 15 e 5 min antes do fecho do PC);</item>
/// <item>à hora, com os botões «✓ Feito» e «+15 min»;</item>
/// <item>se não registar e o item for de insistir: +15 min e +30 min (fica no ecrã até tocar);</item>
/// <item>no fim de um bloco (para não passar do tempo) e quando acaba um adiamento.</item>
/// </list>
/// Cada aviso fica registado em <see cref="RotinaRegisto.Avisos"/> e só sai uma vez. Se o servidor
/// esteve parado, só sai o aviso mais recente dos últimos 10 minutos — nunca uma rajada de atrasados.
/// </summary>
public sealed class RotinaAvisosService : IRotinaAvisosService
{
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(10);
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    private readonly IRotinaRepository _repo;
    private readonly IStaffPushQueue _push;

    public RotinaAvisosService(IRotinaRepository repo, IStaffPushQueue push)
    {
        _repo = repo;
        _push = push;
    }

    private sealed record Aviso(string Chave, DateTime Quando, string Titulo, string Corpo, bool ComBotoes, bool Insistente);

    public async Task<int> ProcessarAsync(DateTime agoraUtc, CancellationToken ct = default)
    {
        var hoje = RotinaCalendario.HojeLisboa(agoraUtc);
        var itens = (await _repo.TodosItensAtivosAsync(ct)).Where(i => RotinaCalendario.Acontece(i, hoje)).ToList();
        if (itens.Count == 0) return 0;
        var registos = (await _repo.TodosRegistosDoDiaAsync(hoje, ct)).ToDictionary(r => r.RotinaItemId);
        var porUser = itens.GroupBy(i => i.UserId).ToDictionary(g => g.Key, g => g.OrderBy(i => i.Hora).ToList());

        var envios = new List<StaffPushJob>();
        foreach (var item in itens)
        {
            registos.TryGetValue(item.Id, out var reg);
            if (reg?.Estado is RotinaRegistoEstado.Feito or RotinaRegistoEstado.Saltado) continue;

            var jaEnviados = (reg?.Avisos ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var devidos = Candidatos(item, hoje, reg, porUser[item.UserId])
                .Where(a => a.Quando <= agoraUtc && agoraUtc < a.Quando + Janela && !jaEnviados.Contains(a.Chave))
                .ToList();
            if (devidos.Count == 0) continue;

            if (reg is null)
            {
                reg = new RotinaRegisto { TenantId = item.TenantId, UserId = item.UserId, RotinaItemId = item.Id, Dia = hoje };
                await _repo.AddRegistoAsync(reg, ct);
                registos[item.Id] = reg;
            }
            reg.Avisos = string.Join('|', jaEnviados.Concat(devidos.Select(a => a.Chave)));
            if (reg.Avisos.Length > 400) reg.Avisos = reg.Avisos[^400..];

            var aviso = devidos.MaxBy(a => a.Quando)!;
            envios.Add(Job(item, hoje, aviso));
        }
        if (envios.Count == 0) return 0;

        // Marca primeiro, envia depois: se a gravação falhar, nada sai (e tenta no minuto seguinte).
        await _repo.SaveAsync(ct);
        foreach (var job in envios) await _push.EnqueueAsync(job, ct);
        return envios.Count;
    }

    private static IEnumerable<Aviso> Candidatos(RotinaItem i, DateOnly dia, RotinaRegisto? reg, IReadOnlyList<RotinaItem> doUser)
    {
        var inicio = RotinaCalendario.InicioUtc(i, dia);
        var fim = RotinaCalendario.FimUtc(i, dia);
        var icone = Icone(i.Tipo);
        var hora = i.Hora.ToString("HH:mm", Pt);
        var limite = i.Tipo == RotinaTipo.Limite;

        if (reg?.Estado == RotinaRegistoEstado.Adiado && reg.AdiadoAte is { } ate)
        {
            var k = $"adiado{ate:HHmm}";
            yield return new(k, ate, limite ? $"🚨 Acabou o tempo extra — {i.Titulo}" : $"{icone} Agora sim: {i.Titulo}",
                limite ? "Já passaste do limite. Desliga agora." : Corpo(i), true, true);
            if (i.Insistir)
                yield return new(k + "i", ate.AddMinutes(15), $"⚠️ Continuas sem cumprir: {i.Titulo}",
                    "Adiar outra vez não é plano. Faz agora ou salta e diz porquê.", true, true);
            if (i.AvisarFim && fim > ate) yield return Fim(i, fim, doUser, dia);
            yield break;
        }

        foreach (var m in RotinaCalendario.MinutosAntes(i.AvisosAntes))
        {
            var titulo = limite
                ? (m >= 10 ? $"⚠️ {m} min para: {i.Titulo}" : $"🔴 {m} min: fecha o que estás a fazer")
                : $"⏳ Daqui a {m} min: {i.Titulo}";
            var corpo = limite
                ? (m >= 10 ? "Começa a terminar. Ideias novas vão para a Inbox, não para agora." : "Guarda, fecha e prepara-te para desligar.")
                : Corpo(i);
            yield return new($"antes{m}", inicio.AddMinutes(-m), titulo, corpo, false, false);
        }

        yield return new("inicio", inicio,
            limite ? $"🚨 {hora} — {i.Titulo}" : $"{icone} {hora} {i.Titulo}",
            limite ? Corpo(i) ?? "Acabou. Desliga os ecrãs." : Corpo(i) ?? "Agora só existe esta coisa.",
            true, limite);

        if (i.Insistir)
        {
            yield return new("insistir1", inicio.AddMinutes(15),
                limite ? $"🚨 Ainda no PC? Eram {hora}." : $"⚠️ Ainda não registaste: {i.Titulo}",
                limite ? "O dia de PC acabou às " + hora + ". Desliga ou diz porquê (+15 min)." : "Vais cumprir o plano? Toca em Feito quando estiver.",
                true, true);
            yield return new("insistir2", inicio.AddMinutes(30),
                limite ? "🔴 30 min depois do limite" : $"🔴 {i.Titulo} — 30 min de atraso",
                limite ? "Isto é o ciclo que queres quebrar. Desliga agora." : "Pára o que estás a fazer e cumpre este bloco.",
                true, true);
        }

        if (i.AvisarFim) yield return Fim(i, fim, doUser, dia);
    }

    private static Aviso Fim(RotinaItem i, DateTime fim, IReadOnlyList<RotinaItem> doUser, DateOnly dia)
    {
        var seguinte = doUser.FirstOrDefault(o => o.Id != i.Id && RotinaCalendario.InicioUtc(o, dia) >= fim.AddMinutes(-5));
        var corpo = seguinte is null
            ? "Fecha este bloco. Ideias novas vão para a Inbox."
            : $"A seguir: {seguinte.Hora.ToString("HH:mm", Pt)} {seguinte.Titulo}. Ideias novas vão para a Inbox.";
        return new("fim", fim, $"⏱️ Acabou o tempo: {i.Titulo}", corpo, true, false);
    }

    private static StaffPushJob Job(RotinaItem i, DateOnly dia, Aviso a)
    {
        var baseUrl = $"/rotina?item={i.Id}&dia={dia:yyyy-MM-dd}";
        return new StaffPushJob(i.TenantId, a.Titulo, a.Corpo, baseUrl, $"rotina-{i.Id:N}-{dia:yyyyMMdd}")
        {
            UserId = i.UserId,
            Insistente = a.Insistente,
            Acoes = a.ComBotoes
                ? new[]
                {
                    new PushAcao("feito", "✓ Feito", baseUrl + "&acao=feito"),
                    new PushAcao("adiar", "+15 min", baseUrl + "&acao=adiar"),
                }
                : null,
        };
    }

    private static string? Corpo(RotinaItem i)
    {
        if (string.IsNullOrWhiteSpace(i.Detalhe)) return null;
        var linhas = i.Detalhe.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimStart('-', '•', '*', ' '));
        var texto = string.Join(" · ", linhas);
        return texto.Length > 180 ? texto[..177] + "…" : texto;
    }

    public static string Icone(RotinaTipo t) => t switch
    {
        RotinaTipo.Refeicao => "🍽️",
        RotinaTipo.Suplemento => "💊",
        RotinaTipo.Meditacao => "🧘",
        RotinaTipo.Exercicio => "🏋️",
        RotinaTipo.Trading => "📈",
        RotinaTipo.Trabalho => "💻",
        RotinaTipo.Limite => "🔴",
        RotinaTipo.Pessoal => "❤️",
        RotinaTipo.Peso => "⚖️",
        _ => "⏰",
    };
}
