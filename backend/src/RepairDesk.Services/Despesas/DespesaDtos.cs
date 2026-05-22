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
    bool IsCogs = false);

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
    bool IsCogs = false);

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
    bool IsCogs);
