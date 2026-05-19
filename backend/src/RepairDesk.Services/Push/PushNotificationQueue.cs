using System.Threading.Channels;

namespace RepairDesk.Services.Push;

public interface IPushNotificationQueue
{
    ValueTask EnqueueStatusChangedAsync(RepairStatusChangedPushJob job, CancellationToken ct = default);
    ValueTask<RepairStatusChangedPushJob> DequeueAsync(CancellationToken ct = default);
}

public class PushNotificationQueue : IPushNotificationQueue
{
    private readonly Channel<RepairStatusChangedPushJob> _channel =
        Channel.CreateUnbounded<RepairStatusChangedPushJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueStatusChangedAsync(RepairStatusChangedPushJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public ValueTask<RepairStatusChangedPushJob> DequeueAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAsync(ct);
}
