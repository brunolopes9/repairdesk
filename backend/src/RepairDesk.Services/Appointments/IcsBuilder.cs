using System.Globalization;
using System.Text;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

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

        foreach (var a in items) AppendAppointmentEvent(sb, a);

        AppendRawIcsLine(sb, "END:VCALENDAR");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Sprint 446: feed combinado — agendamentos + reparações em curso com ETA.
    /// Usado pelo PublicCalendarFeedController para subscrição externa (Google/Apple
    /// Calendar). O auth'd download (S371) continua a usar BuildCalendar(appointments)
    /// porque "Exportar agendamentos" é semanticamente só agendamentos.
    /// </summary>
    public static byte[] BuildCalendar(
        IEnumerable<Appointment> appointments,
        IEnumerable<Reparacao> reparacoesComEta,
        string? calendarName = null)
    {
        var sb = new StringBuilder();
        AppendRawIcsLine(sb, "BEGIN:VCALENDAR");
        AppendRawIcsLine(sb, "VERSION:2.0");
        AppendRawIcsLine(sb, "PRODID:-//Mender//Calendar//PT");
        AppendRawIcsLine(sb, "CALSCALE:GREGORIAN");
        AppendRawIcsLine(sb, "METHOD:PUBLISH");
        AppendIcsProperty(sb, "X-WR-CALNAME", calendarName ?? "Mender - Calendario");
        AppendIcsProperty(sb, "X-WR-CALDESC", "Agendamentos + reparacoes com ETA");

        foreach (var a in appointments) AppendAppointmentEvent(sb, a);
        foreach (var r in reparacoesComEta) AppendReparacaoEvent(sb, r);

        AppendRawIcsLine(sb, "END:VCALENDAR");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void AppendAppointmentEvent(StringBuilder sb, Appointment a)
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

    /// <summary>
    /// Sprint 446: reparação com PrevistoEntregueEm → VEVENT 30min como placeholder
    /// (sem duração real, é só um lembrete "telemóvel X tem de estar pronto a esta hora").
    /// STATUS depende do RepairStatus: Pronto=CONFIRMED; Cancelado=CANCELLED; resto=TENTATIVE.
    /// </summary>
    private static void AppendReparacaoEvent(StringBuilder sb, Reparacao r)
    {
        if (r.PrevistoEntregueEm is not { } eta) return;
        var start = DateTime.SpecifyKind(eta, DateTimeKind.Utc);
        var end = start.AddMinutes(30);
        AppendRawIcsLine(sb, "BEGIN:VEVENT");
        // Prefixo "rep-" garante que o UID não colide com Appointments.
        AppendIcsProperty(sb, "UID", $"rep-{r.Id}@mender");
        AppendRawIcsLine(sb, $"DTSTAMP:{ToIcsUtc(DateTime.UtcNow)}");
        AppendRawIcsLine(sb, $"DTSTART:{ToIcsUtc(start)}");
        AppendRawIcsLine(sb, $"DTEND:{ToIcsUtc(end)}");
        AppendRawIcsLine(sb, $"STATUS:{ToIcsStatusForReparacao(r.Estado)}");
        var cliente = r.Cliente?.Nome ?? "Sem cliente";
        AppendIcsProperty(sb, "SUMMARY", $"Reparação #{r.Numero} · {r.Equipamento} ({cliente})");
        var lines = new List<string>
        {
            $"Cliente: {cliente}",
            $"Equipamento: {r.Equipamento}",
            $"Estado: {r.Estado}",
            $"Avaria: {r.Avaria}",
        };
        if (!string.IsNullOrWhiteSpace(r.Imei)) lines.Add($"IMEI: {r.Imei}");
        if (r.Cliente?.Telefone is { Length: > 0 } tel) lines.Add($"Telefone: {tel}");
        AppendIcsProperty(sb, "DESCRIPTION", string.Join("\n", lines));
        if (!string.IsNullOrWhiteSpace(r.Equipamento))
            AppendIcsProperty(sb, "LOCATION", r.Equipamento);
        AppendRawIcsLine(sb, "END:VEVENT");
    }

    private static string ToIcsStatusForReparacao(RepairStatus s) => s switch
    {
        RepairStatus.Cancelado => "CANCELLED",
        RepairStatus.Pronto or RepairStatus.Entregue => "CONFIRMED",
        _ => "TENTATIVE",
    };

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
