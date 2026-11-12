namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Sprint 638: envio por partes (multipart S3/R2) para vídeos grandes do telemóvel. Cada parte sobe
/// direto do browser com URL pré-assinado; se a rede cair só se repete a parte que falhou, e deixa de
/// haver o limite de 5 GB de um PUT simples. Só o storage que o suporta (R2) implementa isto.
/// </summary>
public interface IMultipartUploadStorage
{
    /// <summary>Abre o envio e devolve o UploadId.</summary>
    Task<string> StartMultipartUploadAsync(string key, string contentType, CancellationToken ct = default);

    /// <summary>URL para PUT de uma parte (1..10000).</summary>
    string CreatePartUploadUrl(string key, string uploadId, int partNumber, TimeSpan validFor);

    /// <summary>Junta as partes no objeto final.</summary>
    Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyList<MultipartPart> parts, CancellationToken ct = default);

    /// <summary>Cancela e liberta as partes já enviadas (best-effort).</summary>
    Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken ct = default);
}

public sealed record MultipartPart(int PartNumber, string ETag);
