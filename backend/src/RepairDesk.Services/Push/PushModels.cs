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

public sealed record RepairStatusChangedPushJob(Guid ReparacaoId);

// Sprint 366: notificação push para os dispositivos de staff de um tenant.
public sealed record StaffPushJob(Guid TenantId, string Title, string Body, string Url, string Tag);

internal sealed record StaffPushPayload(string Title, string Body, string Url, string Tag);

public sealed record VapidKeys(string PublicKey, string PrivateKey, string Subject);

public sealed record WebPushTarget(string Endpoint, string P256dh, string Auth);

internal sealed record PushNotificationPayload(
    string Title,
    string Body,
    string Url,
    string Tag,
    RepairStatus Estado);
