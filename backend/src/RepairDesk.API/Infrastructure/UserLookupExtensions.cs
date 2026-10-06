using Microsoft.AspNetCore.Identity;
using RepairDesk.Core.Entities;

namespace RepairDesk.API.Infrastructure;

public static class UserLookupExtensions
{
    /// <summary>
    /// Encontra um utilizador por email ou username. Usernames não podem conter '@'
    /// (ver <see cref="UserNameRules"/>), por isso a presença de '@' decide o tipo de lookup.
    /// </summary>
    public static async Task<AppUser?> FindByLoginAsync(this UserManager<AppUser> users, string? login)
    {
        var identifier = login?.Trim();
        if (string.IsNullOrEmpty(identifier)) return null;

        return identifier.Contains('@')
            ? await users.FindByEmailAsync(identifier)
            : await users.FindByNameAsync(identifier);
    }
}

public static class UserNameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    /// <summary>Letras, números, ponto, hífen e underscore. Sem '@' para nunca colidir com emails.</summary>
    public static bool IsValid(string userName)
        => userName.Length is >= MinLength and <= MaxLength
           && userName.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}
