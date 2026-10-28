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
    bool IsCogs = false,
    bool IsRecorrente = false,
    int? PeriodicidadeMeses = null,
    // Sprint 525: compra intra-UE em autoliquidação — IVA não dedutível em PT.
    bool ReverseCharge = false,
    // Sprint 573: IVA da fatura e % dedutível (apuramento trimestral).
    int? IvaCents = null,
    int DeducaoPercent = 100);

public sealed record UpdateDespesaRequest(
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    bool IsCogs = false,
    bool IsRecorrente = false,
    int? PeriodicidadeMeses = null,
    int? IvaCents = null,
    int DeducaoPercent = 100,
    bool ReverseCharge = false);

public sealed record DespesaDto(
    Guid Id,
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    DateTime CreatedAt,
    // Sprint 176/177: COGS flag — peça consumida em reparação (não OpEx).
    bool IsCogs,
    bool IsRecorrente,
    int? PeriodicidadeMeses,
    int? IvaCents = null,
    int DeducaoPercent = 100,
    bool ReverseCharge = false);


