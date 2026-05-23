namespace RepairDesk.API.Infrastructure;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

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
    bool RequireChangePasswordOnNextLogin);
