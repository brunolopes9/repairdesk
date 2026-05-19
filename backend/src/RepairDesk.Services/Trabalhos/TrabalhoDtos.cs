using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Trabalhos;

public sealed record CreateTrabalhoRequest(
    Guid? ClienteId,
    string Titulo,
    string? Descricao,
    JobCategory Categoria,
    int? OrcamentoCents,
    string? Notas);

public sealed record UpdateTrabalhoRequest(
    Guid? ClienteId,
    string Titulo,
    string? Descricao,
    JobCategory Categoria,
    TrabalhoStatus Status,
    DateTime? DataInicio,
    DateTime? DataConclusao,
    int? OrcamentoCents,
    int? PrecoFinalCents,
    decimal HorasGastas,
    string? Notas,
    PaymentStatus EstadoPagamento);

public sealed record ClienteResumo(Guid Id, string Nome, string Telefone);

public sealed record TrabalhoDto(
    Guid Id,
    int Numero,
    ClienteResumo? Cliente,
    string Titulo,
    string? Descricao,
    JobCategory Categoria,
    TrabalhoStatus Status,
    DateTime CreatedAt,
    DateTime? DataInicio,
    DateTime? DataConclusao,
    int? OrcamentoCents,
    int? PrecoFinalCents,
    decimal HorasGastas,
    string? Notas,
    PaymentStatus EstadoPagamento,
    int CustoDespesasCents,
    int LucroCents,
    BillingProvider InvoiceProvider,
    string? InvoiceExternalId,
    string? InvoicePdfUrl,
    string? InvoiceNumber,
    DateTime? InvoiceEmittedAt,
    string? EstimateExternalId,
    string? EstimateNumber,
    string? EstimatePdfUrl,
    DateTime? EstimateEmittedAt);
