using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Appointments;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 443 (Doc 91 ponto 3): endpoint público para subscrição .ics em Google
/// Calendar / Apple Calendar / Outlook. Segurança via token não-adivinhável
/// (CalendarFeedToken, 128 bits entropia) + rate limiting.
///
/// O cliente do calendário faz GET periodicamente (Google ~12h, Apple ~5min);
/// retornamos sempre o estado actual filtrado pelo intervalo solicitado.
/// </summary>
[ApiController]
[Route("api/public/calendar-feed")]
[AllowAnonymous]
[EnableRateLimiting("public-portal")]
public sealed class PublicCalendarFeedController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<PublicCalendarFeedController> _logger;

    public PublicCalendarFeedController(AppDbContext db, ILogger<PublicCalendarFeedController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("{token}.ics")]
    public async Task<IActionResult> GetFeed(string token, [FromQuery] int? days, CancellationToken ct)
    {
        // Validações rápidas antes de ir à BD (defensa contra fishing).
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 16 or > 64)
            return NotFound();
        // Token hex apenas — qualquer outro carácter é tentativa de injecção.
        if (!System.Text.RegularExpressions.Regex.IsMatch(token, "^[a-zA-Z0-9]+$"))
            return NotFound();

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.CalendarFeedToken == token && t.IsActive, ct);
        if (tenant is null)
        {
            _logger.LogInformation("Calendar feed token inválido: {Prefix}...", token[..Math.Min(8, token.Length)]);
            return NotFound();
        }

        // Default: 60 dias passado + 60 dias futuro (cobre histórico recente + agendamentos
        // a chegar). Cap em 365 dias total para não saturar quando alguém abusa.
        var window = Math.Clamp(days ?? 60, 7, 365);
        var fromUtc = DateTime.UtcNow.AddDays(-window).Date;
        var toUtc = DateTime.UtcNow.AddDays(window).Date;

        // Lê agendamentos do tenant directamente — bypass do ITenantContext porque
        // este endpoint é anonymous (não tem sessão).
        var items = await _db.Appointments
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenant.Id
                     && a.ScheduledAt >= fromUtc
                     && a.ScheduledAt < toUtc)
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync(ct);

        var bytes = IcsBuilder.BuildCalendar(items, calendarName: $"Mender — {tenant.Name}");
        // Cache-Control curto: clientes calendário (Google) cachiam ~12h; deixamos
        // refresh mais frequente para reflectir mudanças do staff dentro do dia.
        Response.Headers["Cache-Control"] = "public, max-age=900"; // 15 minutos
        return File(bytes, "text/calendar; charset=utf-8");
    }
}
