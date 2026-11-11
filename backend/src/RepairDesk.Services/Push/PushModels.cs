using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Push;

public sealed record BrowserPushSubscriptionDto(
    string Endpoint,
    long? ExpirationTime,
    BrowserPushKeysDto Keys);

public sealed record BrowserPushKeysDto(string P256dh, string Auth);

public sealed record UnsubscribePushRequest(string Endpoint);

public sealed record PushSubscriptionResultDto(bool Subscribed);

public sealed record VapidPublicKeyDto(string PublicKey);

public sealed record RepairStatusChangedPushJob(Guid VendaId);

// Sprint 366: notificação push para os dispositivos de staff de um tenant.
// Sprint 615: UserId (só os dispositivos desse utilizador), botões na notificação e «insistente»
// (fica no ecrã até tocar — requireInteraction).
public sealed record StaffPushJob(Guid TenantId, string Title, string Body, string Url, string Tag)
{
    public Guid? UserId { get; init; }
    public IReadOnlyList<PushAcao>? Acoes { get; init; }
    public bool Insistente { get; init; }
}

/// <summary>Botão de uma notificação: o service worker abre <see cref="Url"/> quando é tocado.</summary>
public sealed record PushAcao(string Action, string Title, string Url);

internal sealed record StaffPushPayload(string Title, string Body, string Url, string Tag, IReadOnlyList<PushAcao>? Actions, bool RequireInteraction);

public sealed record VapidKeys(string PublicKey, string PrivateKey, string Subject);

public sealed record WebPushTarget(string Endpoint, string P256dh, string Auth);

internal sealed record PushNotificationPayload(
    string Title,
    string Body,
    string Url,
    string Tag,
    VendaEstado Estado);
