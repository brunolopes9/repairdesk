using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.EquipmentFields;

namespace RepairDesk.Services.Reparacoes;

/// <summary>Sprint 343: payload para PUT /api/reparacoes/{id}/assign.</summary>
public sealed record AssignReparacaoRequest(Guid? UserId);

/// <summary>Sprint 346: payload para PUT /api/reparacoes/{id}/tags.</summary>
public sealed record SetReparacaoTagsRequest(Guid[]? TagIds);

/// <summary>Sprint 346: tag resumida (nome + cor) para embed em ReparacaoDto.</summary>
public sealed record TagSummaryDto(Guid Id, string Nome, string CorHex);

public sealed record CreateReparacaoRequest(
    Guid ClienteId,
    string Equipamento,
    string Avaria,
    string? Imei,
    int? OrcamentoCents,
    string? Notas,
    RepairStatus? EstadoInicial = null,
    Guid? EquipmentFieldTemplateId = null,
    IReadOnlyList<SetEquipmentFieldValueRequest>? Fields = null);

public sealed record UpdateReparacaoRequest(
    string Equipamento,
    string Avaria,
    string? Imei,
    string? Diagnostico,
    int? OrcamentoCents,
    bool OrcamentoAprovado,
    int? PrecoFinalCents,
    int CustoPecasCents,
    decimal HorasGastas,
    string? Notas,
    PaymentStatus EstadoPagamento,
    Guid? ClienteId = null,
    Guid? EquipmentFieldTemplateId = null,
    IReadOnlyList<SetEquipmentFieldValueRequest>? Fields = null);

public sealed record ChangeEstadoRequest(RepairStatus Estado, string? Notas);

public sealed record ClienteResumo(Guid Id, string Nome, string Telefone, string? Nif = null);

public sealed record EstadoLogDto(
    Guid Id,
    RepairStatus? EstadoFrom,
    RepairStatus EstadoTo,
    DateTime MudouEm,
    string? Notas);

public sealed record ReparacaoDto(
    Guid Id,
    int Numero,
    ClienteResumo Cliente,
    string Equipamento,
    string Avaria,
    string? Imei,
    string? Diagnostico,
    RepairStatus Estado,
    DateTime EstadoSince,
    DateTime RecebidoEm,
    DateTime? EntregueEm,
    int? OrcamentoCents,
    bool OrcamentoAprovado,
    int? PrecoFinalCents,
    int CustoPecasCents,
    decimal HorasGastas,
    int LucroCents,
    int CustoDespesasCents,
    string? Notas,
    PaymentStatus EstadoPagamento,
    string? PublicSlug,
    BillingProvider InvoiceProvider,
    string? InvoiceExternalId,
    string? InvoicePdfUrl,
    string? InvoiceNumber,
    DateTime? InvoiceEmittedAt,
    string? EstimateExternalId,
    string? EstimateNumber,
    string? EstimatePdfUrl,
    DateTime? EstimateEmittedAt,
    Guid? EquipmentFieldTemplateId,
    string? EquipmentFieldTemplateNome,
    IReadOnlyList<EquipmentFieldValueDto> Fields,
    bool PrecisaConfirmacaoPagamento = false,
    bool PrecisaConfirmacaoGarantia = false,
    /// <summary>Sprint 343: técnico atribuído à reparação (null = não atribuída).</summary>
    Guid? AssignedToUserId = null,
    string? AssignedToDisplayName = null,
    /// <summary>Sprint 346: tags categóricas atribuídas (Urgente, Em garantia, etc).</summary>
    IReadOnlyList<TagSummaryDto>? Tags = null);

public sealed record ReparacaoDetalhadaDto(
    ReparacaoDto Reparacao,
    IReadOnlyList<EstadoLogDto> Timeline,
    /// <summary>Sprint 87: venda anterior cujo IMEI bate (se aplicável) — para fluxo "reparação em garantia".</summary>
    ReparacaoVendaOrigemDto? VendaOrigem);

public sealed record ReparacaoVendaOrigemDto(
    Guid VendaId,
    int VendaNumero,
    DateTime VendaData,
    string? GarantiaSlug,
    bool GarantiaActiva,
    int DiasRestantesGarantia,
    int DiasEntreVendaEReparacao,
    string? FornecedorNome,
    int Condicao,
    DateTime? GarantiaFornecedorAteAo);

public sealed record ReparacaoHistoricoItem(
    Guid Id,
    int Numero,
    string Equipamento,
    string? Imei,
    ClienteResumo Cliente,
    RepairStatus Estado,
    DateTime RecebidoEm,
    DateTime? EntregueEm,
    int? PrecoFinalCents,
    string? Diagnostico);

public sealed record ReparacaoHistoricoResponse(
    string Imei,
    bool LuhnValido,
    int Total,
    IReadOnlyList<ReparacaoHistoricoItem> Items);

public sealed record ImportReparacoesRequest(string Csv);

public sealed record ImportReparacaoError(int Linha, string Campo, string Mensagem, string? ValorOriginal);

public sealed record ImportReparacoesResponse(
    int TotalLinhas,
    int Criadas,
    int ClientesCriados,
    int ClientesReutilizados,
    int ComErro,
    IReadOnlyList<ImportReparacaoError> Erros);
