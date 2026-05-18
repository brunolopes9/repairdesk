using RepairDesk.Core.Enums;
using RepairDesk.Services.Audit;

namespace RepairDesk.Services.Clientes;

public sealed record ClientePortableExportDto(
    DateTime ExportedAt,
    string FormatVersion,
    ClienteExportDto Cliente,
    IReadOnlyList<ReparacaoExportDto> Reparacoes,
    IReadOnlyList<TrabalhoExportDto> Trabalhos,
    IReadOnlyList<DespesaExportDto> Despesas,
    IReadOnlyList<FotoExportDto> Fotos,
    IReadOnlyList<GarantiaExportDto> Garantias,
    IReadOnlyList<AvaliacaoExportDto> Avaliacoes,
    IReadOnlyList<PartMovimentoExportDto> PartMovimentos,
    IReadOnlyList<AuditEntryDto> AuditEntries);

public sealed record ClienteExportDto(Guid Id, string Nome, string? Telefone, string? Email, string? Nif, string? Notas, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record ReparacaoExportDto(Guid Id, int Numero, string Equipamento, string? Imei, string Avaria, string? Diagnostico, RepairStatus Estado, DateTime EstadoSince, DateTime CreatedAt, DateTime? EntregueEm, int? OrcamentoCents, bool OrcamentoAprovado, int? PrecoFinalCents, int CustoPecasCents, decimal HorasGastas, string? Notas, PaymentStatus EstadoPagamento, string? PublicSlug, IReadOnlyList<EstadoLogExportDto> Timeline);
public sealed record EstadoLogExportDto(Guid Id, RepairStatus? EstadoFrom, RepairStatus EstadoTo, DateTime MudouEm, Guid? UserId, string? Notas);
public sealed record TrabalhoExportDto(Guid Id, int Numero, string Titulo, string? Descricao, JobCategory Categoria, TrabalhoStatus Status, DateTime? DataInicio, DateTime? DataConclusao, int? OrcamentoCents, int? PrecoFinalCents, decimal HorasGastas, string? Notas, PaymentStatus EstadoPagamento, DateTime CreatedAt);
public sealed record DespesaExportDto(Guid Id, string Descricao, DespesaCategoria Categoria, int ValorCents, DateTime Data, string? Fornecedor, string? NumeroEncomenda, string? Notas, Guid? TrabalhoId, Guid? ReparacaoId, DateTime CreatedAt);
public sealed record FotoExportDto(Guid Id, Guid ReparacaoId, string FileName, string ContentType, long Size, FotoTipo Tipo, int Ordem, string? Legenda, bool VisivelNoPortal, string SignedUrl, DateTimeOffset SignedUrlExpiresAt, DateTime CreatedAt);
public sealed record GarantiaExportDto(Guid Id, Guid ReparacaoId, string Slug, DateTime DataInicio, DateTime DataFim, int DiasGarantia, string? Cobertura, string? Exclusoes, bool Anulada, string? MotivoAnulacao);
public sealed record AvaliacaoExportDto(Guid Id, Guid ReparacaoId, int Score, string? Comentario, bool PublicarTestemunho, bool PedidoGoogleReview, DateTime CreatedAt);
public sealed record PartMovimentoExportDto(Guid Id, Guid PartId, string? PartNome, string? PartSku, int Quantidade, int StockAntes, int StockDepois, PartMovimentoMotivo Motivo, Guid? ReparacaoId, string? Notas, DateTime CreatedAt);

public sealed record HardDeleteClienteRequest(string Confirm, string? Motivo);
public sealed record HardDeleteClienteResponse(Guid ClienteId, string Nome, DateTime DeletedAt, int Reparacoes, int Trabalhos, int Despesas, int Fotos);
