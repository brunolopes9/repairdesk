using Microsoft.Extensions.Caching.Memory;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Calendario;

public sealed record CalendarioExternoDto(Guid Id, string Nome, string Dominio, bool Ativo, DateTime? UltimaLeitura, string? UltimoErro);
public sealed record LigarCalendarioRequest(string Nome, string Url);
public sealed record EventoExternoDto(Guid CalendarioId, string Calendario, string Titulo, DateTime InicioUtc, DateTime FimUtc, bool DiaInteiro, string? Local);
public sealed record EventosExternosDto(IReadOnlyList<EventoExternoDto> Eventos, IReadOnlyList<CalendarioExternoDto> Calendarios);

public interface ICalendarioExternoService
{
    Task<IReadOnlyList<CalendarioExternoDto>> ListAsync(CancellationToken ct = default);
    Task<CalendarioExternoDto> LigarAsync(LigarCalendarioRequest req, CancellationToken ct = default);
    Task RemoverAsync(Guid id, CancellationToken ct = default);
    Task<EventosExternosDto> EventosAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default);
}

/// <summary>
/// Sprint 619 (Doc 98 F4): mostrar no Mender os eventos do Google/Outlook/iCloud (só leitura).
/// Cada calendário é lido no máximo de 10 em 10 minutos (cache em memória). Um calendário com erro não
/// impede os outros: o erro fica visível no próprio calendário.
/// </summary>
public sealed class CalendarioExternoService : ICalendarioExternoService
{
    public const int MaxCalendarios = 5;
    private static readonly TimeSpan Cache = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxIntervalo = TimeSpan.FromDays(62);

    private readonly ICalendarioExternoRepository _repo;
    private readonly ISecretProtector _secrets;
    private readonly IIcsFetcher _fetcher;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _user;
    private readonly ITenantContext _tenant;

    public CalendarioExternoService(ICalendarioExternoRepository repo, ISecretProtector secrets, IIcsFetcher fetcher,
        IMemoryCache cache, ICurrentUser user, ITenantContext tenant)
    {
        _repo = repo;
        _secrets = secrets;
        _fetcher = fetcher;
        _cache = cache;
        _user = user;
        _tenant = tenant;
    }

    private Guid UserId => _user.UserId ?? throw new ForbiddenException("user_required", "Sem utilizador autenticado.");

    public async Task<IReadOnlyList<CalendarioExternoDto>> ListAsync(CancellationToken ct = default)
        => (await _repo.DoUtilizadorAsync(UserId, ct)).Select(Dto).ToList();

    public async Task<CalendarioExternoDto> LigarAsync(LigarCalendarioRequest req, CancellationToken ct = default)
    {
        var userId = UserId;
        var nome = req.Nome?.Trim() ?? string.Empty;
        if (nome.Length is 0 or > 60) throw new ValidationException("nome_invalido", "Dá um nome ao calendário (até 60 caracteres).");
        var url = CalendarioUrl.Normalizar(req.Url)
            ?? throw new ValidationException("url_invalido", "Cola o endereço secreto em formato iCal (https://calendar.google.com/…/basic.ics). Também aceita Outlook e iCloud.");
        if ((await _repo.DoUtilizadorAsync(userId, ct)).Count >= MaxCalendarios)
            throw new ValidationException("demasiados_calendarios", $"No máximo {MaxCalendarios} calendários.");

        var c = new CalendarioExterno
        {
            TenantId = _tenant.TenantId ?? Guid.Empty,
            UserId = userId,
            Nome = nome,
            UrlCifrada = _secrets.Protect(url.ToString()),
            Dominio = url.Host,
        };
        await _repo.AddAsync(c, ct);
        await _repo.SaveAsync(ct);
        return Dto(c);
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _repo.FindAsync(id, ct);
        if (c is null || c.UserId != UserId) throw new NotFoundException("CalendarioExterno", id);
        _repo.Remove(c);
        await _repo.SaveAsync(ct);
        _cache.Remove(ChaveCache(c.Id));
    }

    public async Task<EventosExternosDto> EventosAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default)
    {
        if (ateUtc <= deUtc || ateUtc - deUtc > MaxIntervalo)
            throw new ValidationException("intervalo_invalido", "Intervalo de 1 a 62 dias.");
        var calendarios = (await _repo.DoUtilizadorAsync(UserId, ct)).Where(c => c.Ativo).ToList();
        var eventos = new List<EventoExternoDto>();
        var mudou = false;
        foreach (var c in calendarios)
        {
            try
            {
                var ics = await _cache.GetOrCreateAsync(ChaveCache(c.Id), async e =>
                {
                    e.AbsoluteExpirationRelativeToNow = Cache;
                    var texto = await _fetcher.ObterAsync(new Uri(_secrets.Unprotect(c.UrlCifrada)), ct);
                    c.UltimaLeitura = DateTime.UtcNow;
                    c.UltimoErro = null;
                    mudou = true;
                    return texto;
                });
                eventos.AddRange(CalendarioIcs.Eventos(ics!, deUtc, ateUtc)
                    .Select(ev => new EventoExternoDto(c.Id, c.Nome, ev.Titulo, ev.InicioUtc, ev.FimUtc, ev.DiaInteiro, ev.Local)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var erro = ex is FormatException ? "O endereço não devolveu um calendário iCal." : ex.Message;
                c.UltimoErro = erro.Length > 300 ? erro[..300] : erro;
                mudou = true;
            }
        }
        if (mudou) await _repo.SaveAsync(ct);
        return new EventosExternosDto(eventos.OrderBy(e => e.InicioUtc).ToList(), calendarios.Select(Dto).ToList());
    }

    private static string ChaveCache(Guid id) => $"cal-externo:{id:N}";

    private static CalendarioExternoDto Dto(CalendarioExterno c) => new(c.Id, c.Nome, c.Dominio, c.Ativo, c.UltimaLeitura, c.UltimoErro);
}
