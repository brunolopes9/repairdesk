using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;

namespace RepairDesk.Services.Vendas;

public sealed record CreateVendaItemRequest(
    Guid? PartId,
    string? Descricao,
    int Quantidade,
    int PrecoUnitarioCents,
    int DescontoCents,
    decimal IvaRate,
    string? Imei = null,
    string? Imei2 = null);

public sealed record CreateVendaRequest(
    Guid? ClienteId,
    IReadOnlyList<CreateVendaItemRequest> Items,
    string? Notas,
    VendaOrigem? Origem = null);

public sealed record MarcarVendaPagaRequest(
    PaymentMethod PaymentMethod,
    bool EmitirFatura = false);

public sealed record VendaClienteResumo(Guid Id, string Nome, string Telefone);

public sealed record VendaItemDto(
    Guid Id,
    Guid? PartId,
    string? PartSku,
    string Descricao,
    int Quantidade,
    int PrecoUnitarioCents,
    int DescontoCents,
    decimal IvaRate,
    int TotalCents,
    int IvaCents,
    string? Imei,
    string? Imei2);

public sealed record VendaDto(
    Guid Id,
    int Numero,
    DateTime Data,
    VendaClienteResumo? Cliente,
    int TotalCents,
    int IvaCents,
    PaymentMethod PaymentMethod,
    VendaStatus Status,
    BillingProvider InvoiceProvider,
    string? InvoiceExternalId,
    string? InvoicePdfUrl,
    string? InvoiceNumber,
    DateTime? InvoiceEmittedAt,
    string? Notas,
    IReadOnlyList<VendaItemDto> Items,
    VendaOrigem Origem);

public sealed record EmitVendaFaturaResponse(VendaDto Venda, InvoiceDto? Invoice);

public sealed record VendaImeiLookupDto(
    Guid VendaId,
    int Numero,
    DateTime Data,
    string Descricao,
    string? ClienteNome);

public sealed record VendaReparacaoRelacionadaDto(
    Guid ReparacaoId,
    int ReparacaoNumero,
    DateTime RecebidoEm,
    string Equipamento,
    string Imei,
    int Estado,
    int DiasDesdeAVenda,
    int? OrcamentoCents);
