namespace RepairDesk.Services.Payments.Ifthenpay;

/// <summary>
/// Sprint 303 Fase B: configuração IFTHENPAY. Lida do <c>IConfiguration</c> em runtime
/// (env vars). Sem chaves preenchidas, o provider não é registado (Mock fica activo).
///
/// IFTHENPAY: gateway PT para Multibanco refs + MBWay push. Conta em ifthenpay.com,
/// chaves específicas por método (mbWayKey, mbKey). AntiPhishingKey valida origem
/// do callback (não é HMAC clássico — é shared secret no query string).
/// </summary>
public sealed record IfthenpayOptions
{
    /// <summary>Chave MBWay (formato 8 chars). Distinta da chave MB.</summary>
    public string? MBWayKey { get; init; }

    /// <summary>Chave Multibanco para gerar referências dinâmicas.</summary>
    public string? MultibancoKey { get; init; }

    /// <summary>
    /// Chave anti-phishing partilhada — IFTHENPAY envia-a no callback URL.
    /// Validamos para evitar callbacks forjados.
    /// </summary>
    public string? AntiPhishingKey { get; init; }

    /// <summary>Base URL da API. Sandbox vs produção.</summary>
    public string BaseUrl { get; init; } = "https://api.ifthenpay.com";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(MBWayKey)
        || !string.IsNullOrWhiteSpace(MultibancoKey);
}
