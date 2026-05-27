using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Appointments;

public sealed record AppointmentDto(
    Guid Id,
    Guid? ClienteId,
    string Nome,
    string? Telefone,
    string? Email,
    string? Equipamento,
    string? Notas,
    DateTime ScheduledAt,
    int DurationMin,
    string Status,
    string Source);

public sealed record CreateAppointmentRequest(
    Guid? ClienteId,
    string Nome,
    string? Telefone,
    string? Email,
    string? Equipamento,
    string? Notas,
    DateTime ScheduledAt,
    int? DurationMin);

public sealed record UpdateAppointmentStatusRequest(string Status);
public sealed record RescheduleAppointmentRequest(DateTime ScheduledAt, int? DurationMin);

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentDto>> ListAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status, CancellationToken ct = default);
    Task<AppointmentDto> CreateAsync(CreateAppointmentRequest req, AppointmentSource source, CancellationToken ct = default);
    Task<AppointmentDto> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default);
    Task<AppointmentDto> RescheduleAsync(Guid id, DateTime scheduledAt, int? durationMin, CancellationToken ct = default);
}

public sealed class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _repo;
    private readonly ITenantContext _tenant;

    public AppointmentService(IAppointmentRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AppointmentDto>> ListAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status, CancellationToken ct = default)
    {
        var items = await _repo.ListByRangeAsync(fromUtc.ToUniversalTime(), toUtc.ToUniversalTime(), status, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<AppointmentDto> CreateAsync(CreateAppointmentRequest req, AppointmentSource source, CancellationToken ct = default)
    {
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length is < 2 or > 160)
            throw new ValidationException("appointment_nome_invalido", "Nome obrigatório (2 a 160 caracteres).");
        if (req.ScheduledAt == default)
            throw new ValidationException("appointment_data_invalida", "Data/hora obrigatória.");

        var entity = new Appointment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId ?? Guid.Empty,
            ClienteId = req.ClienteId,
            Nome = nome,
            Telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Equipamento = string.IsNullOrWhiteSpace(req.Equipamento) ? null : req.Equipamento.Trim(),
            Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim(),
            ScheduledAt = DateTime.SpecifyKind(req.ScheduledAt, DateTimeKind.Utc),
            DurationMin = Math.Clamp(req.DurationMin ?? 30, 5, 480),
            Status = AppointmentStatus.Agendado,
            Source = source,
        };
        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);
        return ToDto(entity);
    }

    public async Task<AppointmentDto> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        if (!Enum.TryParse<AppointmentStatus>(status, true, out var parsed))
            throw new ValidationException("appointment_estado_invalido", "Estado inválido.");
        var entity = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Appointment", id);
        entity.Status = parsed;
        await _repo.SaveAsync(ct);
        return ToDto(entity);
    }

    public async Task<AppointmentDto> RescheduleAsync(Guid id, DateTime scheduledAt, int? durationMin, CancellationToken ct = default)
    {
        if (scheduledAt == default)
            throw new ValidationException("appointment_data_invalida", "Data/hora obrigatória.");
        var entity = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Appointment", id);
        entity.ScheduledAt = DateTime.SpecifyKind(scheduledAt, DateTimeKind.Utc);
        if (durationMin is { } d) entity.DurationMin = Math.Clamp(d, 5, 480);
        await _repo.SaveAsync(ct);
        return ToDto(entity);
    }

    private static AppointmentDto ToDto(Appointment a) => new(
        a.Id, a.ClienteId, a.Nome, a.Telefone, a.Email, a.Equipamento, a.Notas,
        DateTime.SpecifyKind(a.ScheduledAt, DateTimeKind.Utc), a.DurationMin, a.Status.ToString(), a.Source.ToString());
}
