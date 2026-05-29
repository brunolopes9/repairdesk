using System.Globalization;
using System.Text;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Appointments;

/// <summary>
/// Sprint 443: helpers de construção iCalendar partilhados entre AppointmentService
/// (download autenticado, S371) e PublicCalendarFeedController (subscrição token-auth,
/// S443). Extraído de AppointmentService para evitar duplicação.
///
/// Conforme com RFC 5545 (line folding a 73 chars, escape de ; , \ e newlines).
/// </summary>
public static class IcsBuilder
{
    /// <summary>
    /// Constrói um documento VCALENDAR completo a partir de uma lista de agendamentos.
    /// </summary>
    public static byte[] BuildCalendar(IEnumerable<Appointment> items, string? calendarName = null)
    {
        var sb = new StringBuilder();
        AppendRawIcsLine(sb, "BEGIN:VCALENDAR");
        AppendRawIcsLine(sb, "VERSION:2.0");
        AppendRawIcsLine(sb, "PRODID:-//Mender//Appointments//PT");
        AppendRawIcsLine(sb, "CALSCALE:GREGORIAN");
        AppendRawIcsLine(sb, "METHOD:PUBLISH");
        AppendIcsProperty(sb, "X-WR-CALNAME", calendarName ?? "Mender - Agendamentos");
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
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Sufixo "from_to" para nome de ficheiro de export pontual.</summary>
    public static string DateRangeStamp(DateTime fromUtc, DateTime toUtc)
    {
        var f = fromUtc.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var t = toUtc.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"{f}_{t}";
    }

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
