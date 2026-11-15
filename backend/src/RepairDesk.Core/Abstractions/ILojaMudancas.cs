namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Sprint 674: aviso de que algo que a loja do site mostra mudou (produto, foto, artigo, stock, categoria).
/// O gerador do site fica à espera em <c>/api/external/loja/aguardar</c> e é acordado no instante — sem perguntar a cada minuto.
/// </summary>
public interface ILojaMudancas
{
    /// <summary>Nº que sobe a cada mudança (só em memória; o que conta para o site é a versão do catálogo).</summary>
    long Atual { get; }
    void Sinalizar();
    /// <summary>Espera até haver uma mudança depois de <paramref name="desde"/> ou até passar <paramref name="maximo"/>.</summary>
    Task EsperarAsync(long desde, TimeSpan maximo, CancellationToken ct);
}
