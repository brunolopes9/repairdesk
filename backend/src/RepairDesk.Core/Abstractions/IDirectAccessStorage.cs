namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Sprint 598 (Doc 95 §6): acesso direto ao storage por URLs pré-assinados. Os vídeos do
/// telemóvel (centenas de MB a GB) sobem do browser direto para o R2 sem passar pela API,
/// e são vistos/descarregados da mesma forma (com suporte a range requests para o player).
/// Só existe quando o storage o suporta (R2); em dev local a API faz de proxy.
/// </summary>
public interface IDirectAccessStorage
{
    /// <summary>URL para PUT do binário com o Content-Type indicado.</summary>
    string CreateUploadUrl(string key, string contentType, TimeSpan validFor);

    /// <summary>URL de leitura temporária (máx. 7 dias no R2).</summary>
    string CreateDownloadUrl(string key, TimeSpan validFor, string? downloadFileName = null);

    /// <summary>Tamanho do objeto, ou null se não existir.</summary>
    Task<long?> GetSizeAsync(string key, CancellationToken ct = default);

    /// <summary>Garante a regra CORS que deixa o browser fazer PUT/GET nestas origens.</summary>
    Task EnsureBrowserCorsAsync(IReadOnlyCollection<string> origins, CancellationToken ct = default);
}
