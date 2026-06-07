namespace RepairDesk.Core.Enums;

public enum BillingDocumentType
{
    FaturaSimplificada = 0,
    Fatura = 1,
    // Sprint 519: Fatura-Recibo (FR) — fatura + recibo num só documento (pagamento imediato).
    // Não fica "em dívida" como a Fatura pura. Ideal para reparação/balcão pago ao levantar.
    FaturaRecibo = 2,
}
