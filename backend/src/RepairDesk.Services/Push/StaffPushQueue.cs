using System.Threading.Channels;

namespace RepairDesk.Services.Push;

/// <summary>
/// Sprint 366: fila in-process para notificações de staff. O endpoint público que cria
/// o pedido online enfileira aqui e responde já ao cliente — o envio do push corre num
/// worker em background, sem bloquear o pedido nem falhá-lo se o push falhar.
/// </summary>
public interface IStaffPushQueue
{
    ValueTask EnqueueAsync(StaffPushJob job, CancellationToken ct = default);
    ValueTask<StaffPushJob> DequeueAsync(CancellationToken ct = default);
}

public class StaffPushQueue : IStaffPushQueue
{
    private readonly Channel<StaffPushJob> _channel =
        Channel.CreateUnbounded<StaffPushJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(StaffPushJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public ValueTask<StaffPushJob> DequeueAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAsync(ct);
}
