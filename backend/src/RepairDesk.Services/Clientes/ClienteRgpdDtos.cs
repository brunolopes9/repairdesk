using RepairDesk.Core.Enums;
using RepairDesk.Services.Audit;

namespace RepairDesk.Services.Clientes;

/// <summary>Portabilidade RGPD (art. 20.º): tudo o que a loja tem sobre o cliente, em JSON.</summary>
public sealed record ClientePortableExportDto(
    DateTime ExportedAt,
    string FormatVersion,
    ClienteExportDto Cliente,
    IReadOnlyList<VendaExportDto> Vendas,
    IReadOnlyList<FotoExportDto> Fotos,
    IReadOnlyList<ComunicacaoExportDto> Comunicacoes,
    IReadOnlyList<GarantiaExportDto> Garantias,
    IReadOnlyList<AvaliacaoExportDto> Avaliacoes,
    IReadOnlyList<AuditEntryDto> AuditEntries);

public sealed record ClienteExportDto(Guid Id, string Nome, string? Telefone, string? Email, string? Nif, string? Notas, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record EstadoLogExportDto(Guid Id, VendaEstado? EstadoFrom, VendaEstado EstadoTo, DateTime MudouEm);
public sealed record FotoExportDto(Guid Id, Guid VendaId, string FileName, string ContentType, long Size, FotoTipo Tipo, int Ordem, string? Legenda, bool VisivelNoPortal, string SignedUrl, DateTimeOffset SignedUrlExpiresAt, DateTime CreatedAt);
public sealed record ComunicacaoExportDto(Guid Id, Guid VendaId, ComunicacaoTipo Tipo, ComunicacaoDirecao Direcao, string Texto, DateTime CreatedAt);
public sealed record GarantiaExportDto(Guid Id, Guid? VendaId, GarantiaSourceType SourceType, string Slug, DateTime DataInicio, DateTime DataFim, int DiasGarantia, string? Cobertura, string? Exclusoes, bool Anulada, string? MotivoAnulacao);
public sealed record AvaliacaoExportDto(Guid Id, Guid VendaId, int Score, string? Comentario, bool PublicarTestemunho, bool PedidoGoogleReview, DateTime CreatedAt);
public sealed record VendaExportDto(
    Guid Id, int Numero, VendaTipo Tipo, VendaEstado Estado, DateTime Data, DateTime CreatedAt,
    string? Equipamento, string? Problema, int TotalCents, int IvaCents,
    PaymentMethod PaymentMethod, string? InvoiceNumber, DateTime? InvoiceEmittedAt, string? Notas,
    IReadOnlyList<VendaItemExportDto> Items,
    IReadOnlyList<EstadoLogExportDto> Timeline);
public sealed record VendaItemExportDto(
    Guid Id, string Descricao, int Quantidade, int PrecoUnitarioCents, int DescontoCents, decimal IvaRate, int TotalCents, string? Imei);

public sealed record HardDeleteClienteRequest(string Confirm, string? Motivo);
public sealed record HardDeleteClienteResponse(Guid ClienteId, string Nome, DateTime DeletedAt, int Reparacoes, int Fotos, int Vendas);
