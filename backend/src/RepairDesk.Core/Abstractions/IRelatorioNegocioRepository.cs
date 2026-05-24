namespace RepairDesk.Core.Abstractions;

public interface IRelatorioNegocioRepository
{
    Task<RelatorioNegocioSnapshot> GetAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    // Sprint 187: análise B2B — para cada fornecedor, % de IMEIs vendidos que voltaram em reparação.
    // VendaItems com IMEI != null e DataVenda >= fromUtc cruzados com Reparacoes pelo mesmo IMEI
    // criadas DEPOIS da venda. Não filtra por garantia activa (uma volta ao fim de 18 meses ainda
    // é um defeito relevante para o B2B mesmo se já fora da garantia legal).
    Task<IReadOnlyList<FornecedorDefeitoRow>> GetTaxaDefeitoFornecedorAsync(
        DateTime fromUtc,
        CancellationToken ct = default);
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

public sealed record FornecedorDefeitoRow(
    string Nome,
    int ItemsVendidos,
    int ItemsComReparacao,
    decimal TaxaDefeitoPct);
