using RepairDesk.Core.Abstractions;

namespace RepairDesk.Services.Loja;

/// <summary>Sprint 674: singleton — cada mudança liberta quem está à espera (uma TaskCompletionSource por «geração»).</summary>
public sealed class LojaMudancas : ILojaMudancas
{
    private readonly object _lock = new();
    private long _atual;
    private TaskCompletionSource _sinal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public long Atual { get { lock (_lock) return _atual; } }

    public void Sinalizar()
    {
        TaskCompletionSource anterior;
        lock (_lock)
        {
            _atual++;
            anterior = _sinal;
            _sinal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        anterior.TrySetResult();
    }

    public async Task EsperarAsync(long desde, TimeSpan maximo, CancellationToken ct)
    {
        Task espera;
        lock (_lock)
        {
            if (_atual != desde) return;
            espera = _sinal.Task;
        }
        try { await espera.WaitAsync(maximo, ct); }
        catch (TimeoutException) { }
    }
}
