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

    /// <summary>
    /// Sprint 547 (Doc 93 #2): artigos mais vendidos no período — linhas de VENDAS pagas (mesma
    /// janela da receita de vendas do snapshot), agregadas por descrição. Margem só quando todas
    /// as linhas do artigo têm custo (Part) — senão null ("sem custo registado").
    /// </summary>
    Task<IReadOnlyList<TopArtigoRow>> GetTopArtigosAsync(DateTime fromUtc, DateTime toUtc, int top, CancellationToken ct = default);

    /// <summary>
    /// Sprint 547 (Doc 93 #2): clientes com mais receita no período — soma das 3 fontes com as
    /// MESMAS condições do snapshot (reparações entregues+pagas, trabalhos concluídos+pagos,
    /// vendas pagas). Sem-cliente (consumidor final) fica de fora.
    /// </summary>
    Task<IReadOnlyList<TopClienteReceitaRow>> GetTopClientesAsync(DateTime fromUtc, DateTime toUtc, int top, CancellationToken ct = default);
}

/// <summary>Sprint 547: linha do top de artigos vendidos.</summary>
public sealed record TopArtigoRow(string Descricao, int Quantidade, long ReceitaCents, long? MargemCents);

/// <summary>Sprint 547: linha do top de clientes por receita.</summary>
public sealed record TopClienteReceitaRow(Guid ClienteId, string Nome, long ReceitaCents, int Documentos);

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
