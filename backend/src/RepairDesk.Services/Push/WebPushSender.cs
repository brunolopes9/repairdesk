using WebPush;

namespace RepairDesk.Services.Push;

public interface IWebPushSender
{
    Task SendAsync(WebPushTarget target, string payload, VapidKeys keys, CancellationToken ct = default);
}

public class WebPushSender : IWebPushSender
{
    public async Task SendAsync(WebPushTarget target, string payload, VapidKeys keys, CancellationToken ct = default)
    {
        var subscription = new PushSubscription(target.Endpoint, target.P256dh, target.Auth);
        var vapid = new VapidDetails(keys.Subject, keys.PublicKey, keys.PrivateKey);
        using var client = new WebPushClient();
        await client.SendNotificationAsync(subscription, payload, vapid, ct);
    }
}
