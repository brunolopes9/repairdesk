using System.Runtime.CompilerServices;

namespace RepairDesk.Common.Helpers;

public static class Guard
{
    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? param = null)
        where T : class
        => value ?? throw new ArgumentNullException(param);

    public static string NotNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? param = null)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", param)
            : value;

    public static Guid NotEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? param = null)
        => value == Guid.Empty
            ? throw new ArgumentException("Value cannot be empty Guid.", param)
            : value;
}
