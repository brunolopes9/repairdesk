using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Rotina;

/// <summary>Sprint 615: regras puras de quando um item da rotina acontece (hora de Lisboa).</summary>
public static class RotinaCalendario
{
    public static readonly TimeZoneInfo Lisboa = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    public const int TodosOsDias = 127;

    public static int Bit(DayOfWeek d) => 1 << (int)d;

    public static bool Acontece(RotinaItem item, DateOnly dia)
    {
        if (item.IntervaloDias is { } n && n > 0)
        {
            var inicio = item.DataInicio ?? DateOnly.FromDateTime(item.CreatedAt);
            var diff = dia.DayNumber - inicio.DayNumber;
            return diff >= 0 && diff % n == 0;
        }
        return (item.Dias & Bit(dia.DayOfWeek)) != 0;
    }

    /// <summary>Início do item nesse dia, em UTC.</summary>
    public static DateTime InicioUtc(RotinaItem item, DateOnly dia) => ParaUtc(dia.ToDateTime(item.Hora));

    public static DateTime FimUtc(RotinaItem item, DateOnly dia) => InicioUtc(item, dia).AddMinutes(item.DuracaoMin);

    public static DateOnly HojeLisboa(DateTime agoraUtc) => DateOnly.FromDateTime(ParaLisboa(agoraUtc));

    public static DateTime ParaLisboa(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Lisboa);

    public static DateTime ParaUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Lisboa);

    public static IReadOnlyList<int> MinutosAntes(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var m) ? m : 0)
            .Where(m => m is > 0 and <= 240)
            .Distinct().OrderByDescending(m => m).ToList();
}
