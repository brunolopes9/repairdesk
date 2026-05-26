using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 349 (Doc 83 Pillar 6): time tracker por reparação.
/// Apenas um timer activo por user; <c>POST /start</c> fecha automaticamente
/// qualquer timer anterior do mesmo user.
/// </summary>
[ApiController]
[Route("api/time-entries")]
[Authorize]
public sealed class TimeEntriesController : ControllerBase
{
    private readonly IReparacaoTimeEntryRepository _repo;
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _time;

    public TimeEntriesController(
        IReparacaoTimeEntryRepository repo,
        IReparacaoRepository reparacoes,
        ITenantContext tenant,
        ICurrentUser user,
        TimeProvider time)
    {
        _repo = repo;
        _reparacoes = reparacoes;
        _tenant = tenant;
        _user = user;
        _time = time;
    }

    public sealed record TimeEntryDto(Guid Id, Guid ReparacaoId, Guid UserId, DateTime StartedAt, DateTime? EndedAt, int? DuracaoMinutos, string? Notas);
    public sealed record StartRequest(Guid ReparacaoId, string? Notas);
    public sealed record StopRequest(string? Notas);
    public sealed record ActiveTimerDto(Guid Id, Guid ReparacaoId, int ReparacaoNumero, DateTime StartedAt);

    /// <summary>Timer activo deste utilizador (null se nenhum).</summary>
    [HttpGet("active")]
    public async Task<ActionResult<ActiveTimerDto?>> Active(CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var entry = await _repo.FindActiveForUserAsync(userId, ct);
        if (entry is null) return Ok((ActiveTimerDto?)null);
        var rep = await _reparacoes.FindByIdAsync(entry.ReparacaoId, ct);
        return Ok(new ActiveTimerDto(entry.Id, entry.ReparacaoId, rep?.Numero ?? 0, entry.StartedAt));
    }

    [HttpGet("by-reparacao/{reparacaoId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TimeEntryDto>>> ListForReparacao(Guid reparacaoId, CancellationToken ct)
    {
        var entries = await _repo.ListByReparacaoAsync(reparacaoId, ct);
        return Ok(entries.Select(MapDto).ToList());
    }

    /// <summary>Inicia timer; fecha automaticamente timer activo prévio do mesmo user.</summary>
    [HttpPost("start")]
    public async Task<ActionResult<TimeEntryDto>> Start([FromBody] StartRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return Unauthorized();
        if (_user.UserId is not { } userId) return Unauthorized();

        var rep = await _reparacoes.FindByIdAsync(req.ReparacaoId, ct);
        if (rep is null) return NotFound(new { code = "reparacao_not_found" });

        var now = _time.GetUtcNow().UtcDateTime;

        // Auto-fecha qualquer timer activo prévio deste user.
        var active = await _repo.FindActiveForUserAsync(userId, ct);
        if (active is not null)
        {
            active.EndedAt = now;
            await _repo.UpdateAsync(active, ct);
        }

        var entry = new ReparacaoTimeEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReparacaoId = req.ReparacaoId,
            UserId = userId,
            StartedAt = now,
            Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim(),
        };
        await _repo.AddAsync(entry, ct);
        return Ok(MapDto(entry));
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<ActionResult<TimeEntryDto>> Stop(Guid id, [FromBody] StopRequest? req, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var entry = await _repo.FindByIdAsync(id, ct);
        if (entry is null) return NotFound();
        if (entry.UserId != userId && !_user.IsInRole("Admin"))
            return Forbid();
        if (entry.EndedAt is not null) return Conflict(new { code = "already_stopped" });

        entry.EndedAt = _time.GetUtcNow().UtcDateTime;
        if (!string.IsNullOrWhiteSpace(req?.Notas)) entry.Notas = req.Notas.Trim();
        await _repo.UpdateAsync(entry, ct);
        return Ok(MapDto(entry));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entry = await _repo.FindByIdAsync(id, ct);
        if (entry is null) return NotFound();
        await _repo.DeleteAsync(entry, ct);
        return NoContent();
    }

    public sealed record TimeStatsRowDto(Guid UserId, string DisplayName, int TotalMinutos, int Sessoes, int Reparacoes);

    /// <summary>Sprint 349/350: minutos por user num intervalo [from, to) + DisplayName resolvido.</summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<TimeStatsRowDto>>> Stats(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromServices] UserManager<AppUser> userManager,
        CancellationToken ct)
    {
        if (to <= from) return BadRequest(new { code = "invalid_range" });
        var rows = await _repo.StatsByUserAsync(from.ToUniversalTime(), to.ToUniversalTime(), ct);
        var userIds = rows.Select(r => r.UserId).ToList();
        var users = await userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(ct);
        var nameById = users.ToDictionary(u => u.Id, u => string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? u.Id.ToString()) : u.DisplayName);

        var dtos = rows.Select(r => new TimeStatsRowDto(
            r.UserId,
            nameById.TryGetValue(r.UserId, out var n) ? n : r.UserId.ToString(),
            r.TotalMinutos, r.Sessoes, r.Reparacoes)).ToList();
        return Ok(dtos);
    }

    private static TimeEntryDto MapDto(ReparacaoTimeEntry e) =>
        new(e.Id, e.ReparacaoId, e.UserId, e.StartedAt, e.EndedAt, e.DuracaoMinutos, e.Notas);
}
