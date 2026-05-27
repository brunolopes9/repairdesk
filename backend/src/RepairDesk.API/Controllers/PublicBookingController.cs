using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 389 (Doc 84): booking online público. O cliente marca uma hora a partir do site da loja.
/// **Sem autenticação** — segurança via IntakeSlug não-adivinhável + rate limiting + honeypot.
/// Cria um <see cref="Appointment"/> com Source=Online e Status=Agendado que o staff vê em
/// /agendamentos (e é avisado por push). O staff confirma/reagenda; sem garantia de slot livre v1.
/// </summary>
[ApiController]
[Route("api/public/booking")]
[AllowAnonymous]
[EnableRateLimiting("public-portal")]
public sealed class PublicBookingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAppointmentRepository _repo;
    private readonly IStaffPushQueue _staffPush;
    private readonly ILogger<PublicBookingController> _logger;

    public PublicBookingController(AppDbContext db, IAppointmentRepository repo, IStaffPushQueue staffPush, ILogger<PublicBookingController> logger)
    {
        _db = db;
        _repo = repo;
        _staffPush = staffPush;
        _logger = logger;
    }

    public sealed record BookingInfo(string LojaNome, string? PrimaryColor);
    public sealed record SubmitBooking(
        string Nome, string? Telefone, string? Email, string? Equipamento,
        string? Notas, DateTime ScheduledAt, int? DurationMin, string? Website);
    public sealed record SubmitResult(bool Ok);

    /// <summary>GET leve: branding da loja para o widget de marcação.</summary>
    [HttpGet("{intakeSlug}")]
    public async Task<ActionResult<BookingInfo>> Info(string intakeSlug, CancellationToken ct)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.IntakeSlug == intakeSlug && t.IsActive, ct);
        if (tenant is null) return NotFound();
        return Ok(new BookingInfo(tenant.LegalName ?? tenant.Name, tenant.PrimaryColor));
    }

    [HttpPost("{intakeSlug}")]
    public async Task<ActionResult<SubmitResult>> Submit(string intakeSlug, [FromBody] SubmitBooking req, CancellationToken ct)
    {
        // Honeypot: campo escondido por CSS — bots preenchem-no. 200 falso para não dar feedback.
        if (!string.IsNullOrWhiteSpace(req.Website))
        {
            _logger.LogInformation("Booking honeypot disparou para slug {Slug}", intakeSlug);
            return Ok(new SubmitResult(true));
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.IntakeSlug == intakeSlug && t.IsActive, ct);
        if (tenant is null) return NotFound(new { code = "booking_not_found" });

        var nome = (req.Nome ?? "").Trim();
        var telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim();
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (nome.Length is < 2 or > 120) return BadRequest(new { code = "invalid_nome" });
        if (telefone is null && email is null) return BadRequest(new { code = "missing_contact", message = "Indica telefone ou email." });

        var when = req.ScheduledAt.ToUniversalTime();
        if (when <= DateTime.UtcNow.AddMinutes(-5)) return BadRequest(new { code = "invalid_datetime", message = "Escolhe uma data futura." });
        if (when > DateTime.UtcNow.AddDays(90)) return BadRequest(new { code = "too_far", message = "Marca dentro dos próximos 90 dias." });

        var duration = req.DurationMin is >= 10 and <= 240 ? req.DurationMin!.Value : 30;

        var entity = new Appointment
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Nome = nome,
            Telefone = telefone,
            Email = email,
            Equipamento = string.IsNullOrWhiteSpace(req.Equipamento) ? null : req.Equipamento!.Trim(),
            Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas!.Trim(),
            ScheduledAt = when,
            DurationMin = duration,
            Status = AppointmentStatus.Agendado,
            Source = AppointmentSource.Online,
        };
        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);

        // Avisa o staff (push). Fire-and-forget via fila — não bloqueia o cliente.
        await _staffPush.EnqueueAsync(new StaffPushJob(
            tenant.Id,
            "Nova marcação online",
            $"{nome} — {when.ToLocalTime():dd/MM HH:mm}{(entity.Equipamento != null ? $" · {entity.Equipamento}" : "")}",
            "/agendamentos",
            "appointment"), ct);

        return Ok(new SubmitResult(true));
    }
}
