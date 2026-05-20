using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Chave de API para integrações externas (loja online, importadores, automações).
/// Diferente de JWT de utilizador: scope é o tenant inteiro, sem expiração natural,
/// revogável manualmente. Key armazenada como hash SHA256 — plain só é mostrada na criação.
/// </summary>
public class ServiceApiKey : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Nome legível (ex: "Loja online produção", "Importador CSV mensal").</summary>
    public required string Name { get; set; }
    /// <summary>Prefixo visível para identificação na UI (ex: "rd_live_a1b2c3"). NÃO sensível.</summary>
    public required string KeyPrefix { get; set; }
    /// <summary>SHA256 hex do plain key (lookup-friendly, não-reversível).</summary>
    public required string KeyHash { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    /// <summary>
    /// CSV de scopes (ex: "read,write"). Null = wildcard (acesso total — backwards-compat com keys
    /// criadas antes do Sprint 111). Validar com <see cref="ServiceApiKeyScopes"/>.
    /// </summary>
    public string? Scopes { get; set; }
}

/// <summary>Scopes disponíveis para ServiceApiKey. Granularidade intencionalmente baixa (Sprint 111).</summary>
public static class ServiceApiKeyScopes
{
    /// <summary>Lê dados (orders, parts, clientes, garantias). Endpoints GET do External.</summary>
    public const string Read = "read";
    /// <summary>Cria e modifica (checkout, cancel order). Endpoints POST do External.</summary>
    public const string Write = "write";
    /// <summary>Sprint 147: submete faturas de fornecedor (n8n IMAP → ingest). Não dá acesso a leituras.</summary>
    public const string Ingest = "ingest";

    public static readonly IReadOnlyList<string> All = new[] { Read, Write, Ingest };

    /// <summary>Parsing tolerante: split, trim, lowercase, distinct. Devolve null se input null/vazio.</summary>
    public static string[]? Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToArray();
    }
}
