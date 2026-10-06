namespace RepairDesk.API.Infrastructure;

/// <summary>
/// <paramref name="Login"/> aceita email OU username. <paramref name="Email"/> mantém-se só para
/// compatibilidade com clientes antigos (PWA em cache) que ainda enviam <c>{ email, password }</c>.
/// </summary>
public sealed record LoginRequest(string? Login, string Password, string? Email = null)
{
    public string Identifier => (Login ?? Email ?? string.Empty).Trim();
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Pedido de recuperação: email ou username. A resposta é sempre igual (não revela se a conta existe).</summary>
public sealed record ForgotPasswordRequest(string Login);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

/// <summary>Sprint 420: payload para PUT /api/auth/me — editar perfil próprio. UserName opcional (login alternativo ao email).</summary>
public sealed record UpdateMeRequest(string DisplayName, string? PhoneNumber, string? UserName = null);

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
    string? PhoneNumber = null,
    /// <summary>Username para login. Null enquanto o utilizador só tiver o email (UserName == Email).</summary>
    string? UserName = null);
