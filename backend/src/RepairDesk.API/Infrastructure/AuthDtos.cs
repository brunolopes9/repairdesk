namespace RepairDesk.API.Infrastructure;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Sprint 420: payload para PUT /api/auth/me — editar perfil próprio.</summary>
public sealed record UpdateMeRequest(string DisplayName, string? PhoneNumber);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    UserInfo User);

public sealed record UserInfo(
    Guid Id,
    string Email,
    string DisplayName,
    Guid TenantId,
    IReadOnlyList<string> Roles,
    bool RequireChangePasswordOnNextLogin,
    /// <summary>Sprint 420: telefone do utilizador (opcional).</summary>
    string? PhoneNumber = null);
