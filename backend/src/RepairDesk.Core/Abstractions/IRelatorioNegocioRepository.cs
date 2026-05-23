namespace RepairDesk.Core.Abstractions;

public interface IRelatorioNegocioRepository
{
    Task<RelatorioNegocioSnapshot> GetAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public sealed record RelatorioNegocioSnapshot(
    int ReceitaReparacoesCents,
    int ReceitaTrabalhosCents,
    int ReceitaVendasCents,
    int ReparacoesPagasCount,
    int CustoPecasCents,
    int OpexCents,
    IReadOnlyList<TopReparacaoLucrativaRow> TopReparacoesLucrativas,
    IReadOnlyList<TopPecaUsadaRow> TopPecasUsadas,
    IReadOnlyList<TopFornecedorComprasRow> TopFornecedores);

public sealed record TopReparacaoLucrativaRow(
    Guid Id,
    int Numero,
    string Equipamento,
    string? ClienteNome,
    int ReceitaCents,
    int CustoPecasCents,
    int LucroCents);

public sealed record TopPecaUsadaRow(
    Guid PartId,
    string Nome,
    string? Sku,
    int Quantidade);

public sealed record TopFornecedorComprasRow(
    string Nome,
    int TotalCompradoCents);
