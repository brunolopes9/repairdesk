namespace RepairDesk.Core.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool IsInRole(string role);
    /// <summary>
    /// Id da ServiceApiKey que autenticou o request (null para JWT de utilizador).
    /// Lido do claim "service_api_key_id" adicionado pelo ApiKeyAuthHandler.
    /// </summary>
    Guid? ServiceApiKeyId { get; }
}
