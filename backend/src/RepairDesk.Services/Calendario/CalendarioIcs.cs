using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace RepairDesk.Services.Calendario;

public sealed record EventoIcs(string Uid, string Titulo, DateTime InicioUtc, DateTime FimUtc, bool DiaInteiro, string? Local);

/// <summary>
/// Sprint 619 (Doc 98 F4): lê um calendário iCalendar (.ics — ex.: «endereço secreto em formato iCal» do Google)
/// e devolve os eventos de um intervalo, já com as repetições (RRULE) expandidas. Puro: não faz pedidos.
/// </summary>
public static class CalendarioIcs
{
    public const int MaxEventos = 500;

    public static IReadOnlyList<EventoIcs> Eventos(string ics, DateTime deUtc, DateTime ateUtc)
    {
        if (string.IsNullOrWhiteSpace(ics) || !ics.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Não é um calendário iCal.");
        var cal = Calendar.Load(ics) ?? throw new FormatException("Calendário vazio.");
        var lista = new List<EventoIcs>();
        foreach (var o in cal.GetOccurrences(new CalDateTime(deUtc, "UTC"), new CalDateTime(ateUtc, "UTC")))
        {
            if (o.Source is not CalendarEvent ev) continue;
            if (string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
            var inicio = o.Period.StartTime.AsUtc;
            var fim = o.Period.EndTime?.AsUtc ?? inicio + (o.Period.Duration > TimeSpan.Zero ? o.Period.Duration : ev.Duration);
            if (fim <= inicio) fim = ev.IsAllDay ? inicio.AddDays(1) : inicio.AddMinutes(30);
            if (fim <= deUtc || inicio >= ateUtc) continue;
            lista.Add(new EventoIcs(ev.Uid ?? Guid.NewGuid().ToString("N"), string.IsNullOrWhiteSpace(ev.Summary) ? "(sem título)" : ev.Summary.Trim(),
                inicio, fim, ev.IsAllDay, string.IsNullOrWhiteSpace(ev.Location) ? null : ev.Location.Trim()));
            if (lista.Count >= MaxEventos) break;
        }
        return lista.OrderBy(e => e.InicioUtc).ToList();
    }
}
