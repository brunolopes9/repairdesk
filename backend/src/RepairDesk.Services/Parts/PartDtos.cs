using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Parts;

public sealed record CreatePartRequest(
    string? Sku,
    string Nome,
    PartCategoria Categoria,
    string? Marca,
    string? Modelo,
    Guid? PriceTableEntryId,
    int QtdStock,
    int QtdMinima,
    int CustoUnitarioCents,
    string? Fornecedor,
    string? LocalArmazenamento,
    string? Notas);

public sealed record UpdatePartRequest(
    string? Sku,
    string Nome,
    PartCategoria Categoria,
    string? Marca,
    string? Modelo,
    Guid? PriceTableEntryId,
    int QtdStock,
    int QtdMinima,
    int CustoUnitarioCents,
    string? Fornecedor,
    string? LocalArmazenamento,
    string? Notas,
    bool Activo);

public sealed record CreatePartMovimentoRequest(
    int Quantidade,
    PartMovimentoMotivo Motivo,
    Guid? ReparacaoId,
    string? Notas);

public sealed record PartDto(
    Guid Id,
    string? Sku,
    string Nome,
    PartCategoria Categoria,
    string? Marca,
    string? Modelo,
    Guid? PriceTableEntryId,
    int QtdStock,
    int QtdMinima,
    int CustoUnitarioCents,
    int ValorTotalStockCents,
    string? Fornecedor,
    string? LocalArmazenamento,
    string? Notas,
    bool Activo,
    bool StockBaixo,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PartMovimentoDto(
    Guid Id,
    Guid PartId,
    string PartNome,
    string? PartSku,
    int Quantidade,
    int StockAntes,
    int StockDepois,
    PartMovimentoMotivo Motivo,
    Guid? ReparacaoId,
    string? Notas,
    DateTime CreatedAt);

public sealed record ImportPartsRequest(string Csv);

public sealed record ImportPartsResponse(
    int TotalLinhas,
    int Criadas,
    int Ignoradas,
    int ComErro,
    IReadOnlyList<PartDto> PecasCriadas,
    IReadOnlyList<ImportError> Erros);
