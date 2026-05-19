using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Endpoint HTTP do tenant que recebe POSTs quando eventos do RepairDesk acontecem.
/// Cada delivery é assinada com HMAC-SHA256 do <see cref="Secret"/> para que o receptor
/// possa verificar autenticidade.
/// </summary>
public class WebhookSubscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Nome legível (ex: "Loja online — eventos garantia").</summary>
    public required string Name { get; set; }
    /// <summary>URL HTTPS para onde o RepairDesk vai fazer POST. Validado no save.</summary>
    public required string Url { get; set; }
    /// <summary>Secret usado em HMAC-SHA256(payload) → header X-RepairDesk-Signature.</summary>
    public required string Secret { get; set; }
    /// <summary>CSV de event types subscritos (ex: "garantia.emitida,venda.cancelada").</summary>
    public required string Events { get; set; }
    public bool Active { get; set; } = true;
    public DateTime? LastDeliveryAt { get; set; }
    public int FailureCount { get; set; }
    public DateTime? DisabledAt { get; set; }
}

/// <summary>Constantes dos event types publicados pelo RepairDesk.</summary>
public static class WebhookEvents
{
    public const string GarantiaEmitida = "garantia.emitida";
    public const string GarantiaAnulada = "garantia.anulada";
    public const string GarantiaExpirada = "garantia.expirada";
    public const string VendaCriada = "venda.criada";
    public const string VendaPaga = "venda.paga";
    public const string VendaCancelada = "venda.cancelada";
    public const string ReparacaoConcluida = "reparacao.concluida";

    public static readonly IReadOnlyList<string> All = new[]
    {
        GarantiaEmitida, GarantiaAnulada, GarantiaExpirada,
        VendaCriada, VendaPaga, VendaCancelada,
        ReparacaoConcluida,
    };
}
