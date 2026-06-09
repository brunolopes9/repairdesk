using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Despesas;

public sealed record CreateDespesaRequest(
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime? Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    Guid? TrabalhoId,
    Guid? ReparacaoId,
    bool IsCogs = false,
    bool IsRecorrente = false,
    int? PeriodicidadeMeses = null,
    // Sprint 525: compra intra-UE em autoliquidação — IVA não dedutível em PT.
    bool ReverseCharge = false);

public sealed record UpdateDespesaRequest(
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    Guid? TrabalhoId,
    Guid? ReparacaoId,
    bool IsCogs = false,
    bool IsRecorrente = false,
    int? PeriodicidadeMeses = null);

public sealed record DespesaDto(
    Guid Id,
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    Guid? TrabalhoId,
    Guid? ReparacaoId,
    DateTime CreatedAt,
    // Sprint 176/177: COGS flag — peça consumida em reparação (não OpEx).
    bool IsCogs,
    bool IsRecorrente,
    int? PeriodicidadeMeses);

// Sprint 540: converter uma despesa de Peças/Material (limbo: comprada como despesa, invisível
// no Stock) numa Part real com movimento de entrada. Resolve o registo Samsung A15 (161144) e
// qualquer compra de inventário que tenha sido aprovada como despesa por engano.
public sealed record ConvertDespesaToStockRequest(
    int Quantidade = 1,
    string? Sku = null,
    string? Nome = null,
    PartCategoria Categoria = PartCategoria.Outro,
    string? Marca = null,
    string? Modelo = null,
    string? LocalArmazenamento = null);

public sealed record ConvertDespesaToStockResult(
    Guid PartId,
    string Nome,
    int Quantidade,
    int CustoUnitarioCents);
