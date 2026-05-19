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

public sealed record VapidKeys(string PublicKey, string PrivateKey, string Subject);

public sealed record WebPushTarget(string Endpoint, string P256dh, string Auth);

internal sealed record PushNotificationPayload(
    string Title,
    string Body,
    string Url,
    string Tag,
    RepairStatus Estado);
