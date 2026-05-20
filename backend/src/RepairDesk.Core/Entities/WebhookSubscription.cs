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

    // Sprint 125: catálogo — loja online usa estes para invalidar cache (read replica local).
    public const string PartsAdicionado = "parts.adicionado";
    public const string PartsAtualizado = "parts.atualizado";
    public const string PartsRemovido = "parts.removido";
    public const string PhonesAdicionado = "phones.adicionado";
    public const string PhonesAtualizado = "phones.atualizado";
    public const string PhonesRemovido = "phones.removido";

    // Sprint 130: alertas operacionais quando stock desce abaixo do mínimo. Dispara só na
    // transição (above→below) para evitar spam. Loja online pode esconder produtos esgotados;
    // Bruno recebe lembrete para reencomendar.
    public const string PartsStockBaixo = "parts.stock-baixo";
    public const string PhonesStockBaixo = "phones.stock-baixo";

    public static readonly IReadOnlyList<string> All = new[]
    {
        GarantiaEmitida, GarantiaAnulada, GarantiaExpirada,
        VendaCriada, VendaPaga, VendaCancelada,
        ReparacaoConcluida,
        PartsAdicionado, PartsAtualizado, PartsRemovido,
        PhonesAdicionado, PhonesAtualizado, PhonesRemovido,
        PartsStockBaixo, PhonesStockBaixo,
    };
}
