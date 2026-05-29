using System.Globalization;
using System.Text;
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
public sealed record AppointmentCalendarExport(byte[] Content, string Filename);

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentDto>> ListAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status, CancellationToken ct = default);
    Task<AppointmentCalendarExport> ExportIcsAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status, CancellationToken ct = default);
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

    public async Task<AppointmentCalendarExport> ExportIcsAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status, CancellationToken ct = default)
    {
        var items = await _repo.ListByRangeAsync(fromUtc.ToUniversalTime(), toUtc.ToUniversalTime(), status, ct);
        var sb = new StringBuilder();
        AppendRawIcsLine(sb, "BEGIN:VCALENDAR");
        AppendRawIcsLine(sb, "VERSION:2.0");
        AppendRawIcsLine(sb, "PRODID:-//Mender//Appointments//PT");
        AppendRawIcsLine(sb, "CALSCALE:GREGORIAN");
        AppendRawIcsLine(sb, "METHOD:PUBLISH");
        AppendIcsProperty(sb, "X-WR-CALNAME", "Mender - Agendamentos");
        AppendIcsProperty(sb, "X-WR-CALDESC", "Agenda de marcacoes do Mender");

        foreach (var a in items)
        {
            var start = DateTime.SpecifyKind(a.ScheduledAt, DateTimeKind.Utc);
            var end = start.AddMinutes(Math.Max(5, a.DurationMin));
            AppendRawIcsLine(sb, "BEGIN:VEVENT");
            AppendIcsProperty(sb, "UID", $"{a.Id}@mender");
            AppendRawIcsLine(sb, $"DTSTAMP:{ToIcsUtc(DateTime.UtcNow)}");
            AppendRawIcsLine(sb, $"DTSTART:{ToIcsUtc(start)}");
            AppendRawIcsLine(sb, $"DTEND:{ToIcsUtc(end)}");
            AppendRawIcsLine(sb, $"STATUS:{ToIcsStatus(a.Status)}");
            AppendIcsProperty(sb, "SUMMARY", BuildSummary(a));
            AppendIcsProperty(sb, "DESCRIPTION", BuildDescription(a));
            if (!string.IsNullOrWhiteSpace(a.Equipamento))
                AppendIcsProperty(sb, "LOCATION", a.Equipamento);
            AppendRawIcsLine(sb, "END:VEVENT");
        }

        AppendRawIcsLine(sb, "END:VCALENDAR");
        var fromStamp = fromUtc.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var toStamp = toUtc.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return new AppointmentCalendarExport(Encoding.UTF8.GetBytes(sb.ToString()), $"mender-agendamentos_{fromStamp}_{toStamp}.ics");
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

    private static string BuildSummary(Appointment a)
        => string.IsNullOrWhiteSpace(a.Equipamento) ? $"Agendamento: {a.Nome}" : $"{a.Equipamento} - {a.Nome}";

    private static string BuildDescription(Appointment a)
    {
        var lines = new List<string>
        {
            $"Cliente: {a.Nome}",
            $"Estado: {a.Status}",
            $"Origem: {a.Source}",
        };
        if (!string.IsNullOrWhiteSpace(a.Telefone)) lines.Add($"Telefone: {a.Telefone}");
        if (!string.IsNullOrWhiteSpace(a.Email)) lines.Add($"Email: {a.Email}");
        if (!string.IsNullOrWhiteSpace(a.Equipamento)) lines.Add($"Equipamento: {a.Equipamento}");
        if (!string.IsNullOrWhiteSpace(a.Notas)) lines.Add($"Notas: {a.Notas}");
        return string.Join("\n", lines);
    }

    private static string ToIcsStatus(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Cancelado or AppointmentStatus.NaoCompareceu => "CANCELLED",
        AppointmentStatus.Confirmado or AppointmentStatus.Concluido => "CONFIRMED",
        _ => "TENTATIVE",
    };

    private static string ToIcsUtc(DateTime value)
        => value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static void AppendIcsProperty(StringBuilder sb, string name, string value)
        => AppendRawIcsLine(sb, $"{name}:{EscapeIcs(value)}");

    private static string EscapeIcs(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void AppendRawIcsLine(StringBuilder sb, string line)
    {
        const int maxLineLength = 73;
        while (line.Length > maxLineLength)
        {
            sb.Append(line[..maxLineLength]).Append("\r\n ");
            line = line[maxLineLength..];
        }
        sb.Append(line).Append("\r\n");
    }
}
