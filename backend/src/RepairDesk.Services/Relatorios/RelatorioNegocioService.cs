using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Relatorios;

public interface IRelatorioNegocioService
{
    Task<RelatorioNegocioResponse> GetAsync(int ano, int trimestre, CancellationToken ct = default);

    // Sprint 187: análise B2B — taxa de defeito por fornecedor, calculada sobre IMEIs vendidos
    // com fornecedor identificado que voltaram para reparação. `meses` controla quão atrás se
    // olha (default 12). Não inclui vendas onde FornecedorNome ou IMEI estejam vazios.
    Task<TaxaDefeitoFornecedorResponse> GetTaxaDefeitoFornecedorAsync(int meses, CancellationToken ct = default);

    // Sprint 547 (Doc 93 #2): "Análise de Vendas" do painel Moloni — top artigos vendidos
    // (quantidade, receita, margem quando há custo) + top clientes por receita no trimestre.
    Task<AnaliseVendasResponse> GetAnaliseVendasAsync(int ano, int trimestre, CancellationToken ct = default);
}

/// <summary>Sprint 547: resposta da Análise de Vendas (top artigos + top clientes do trimestre).</summary>
public sealed record AnaliseVendasResponse(
    int Ano,
    int Trimestre,
    DateTime PeriodoDe,
    DateTime PeriodoAte,
    IReadOnlyList<TopArtigoRow> TopArtigos,
    IReadOnlyList<TopClienteReceitaRow> TopClientes);

public sealed class RelatorioNegocioService : IRelatorioNegocioService
{
    private readonly IRelatorioNegocioRepository _repo;
    private readonly ITenantContext _tenant;

    public RelatorioNegocioService(IRelatorioNegocioRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<RelatorioNegocioResponse> GetAsync(int ano, int trimestre, CancellationToken ct = default)
    {
        if (_tenant.TenantId is null)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var (fromUtc, toUtc) = Periodo(ano, trimestre);
        var snapshot = await _repo.GetAsync(fromUtc, toUtc, ct);

        var receitaTotal = snapshot.ReceitaReparacoesCents
            + snapshot.ReceitaTrabalhosCents
            + snapshot.ReceitaVendasCents;
        // Sprint 536: lucro BRUTO = receita − custo das peças (COGS). O OpEx (despesas fixas: renda,
        // ferramentas, etc.) entra no resultado OPERACIONAL, NÃO no bruto. Antes subtraía-se o OpEx ao
        // bruto, por isso um serviço lucrativo (ex.: Maria Inês, 98,40€ de margem) aparecia como
        // prejuízo (−1,60€) só porque havia 100€ de OpEx no período. Agora separa-se.
        var lucroBruto = receitaTotal - snapshot.CustoPecasCents;
        var lucroOperacional = lucroBruto - snapshot.OpexCents;
        var margemBruta = receitaTotal == 0
            ? 0
            : Math.Round(lucroBruto * 100m / receitaTotal, 2, MidpointRounding.AwayFromZero);
        var margemOperacional = receitaTotal == 0
            ? 0
            : Math.Round(lucroOperacional * 100m / receitaTotal, 2, MidpointRounding.AwayFromZero);
        var ticketMedio = snapshot.ReparacoesPagasCount == 0
            ? 0
            : (int)Math.Round(receitaTotal / (decimal)snapshot.ReparacoesPagasCount, MidpointRounding.AwayFromZero);

        return new RelatorioNegocioResponse(
            ano,
            trimestre,
            fromUtc,
            toUtc,
            receitaTotal,
            snapshot.ReceitaReparacoesCents,
            snapshot.ReceitaTrabalhosCents,
            snapshot.ReceitaVendasCents,
            snapshot.CustoPecasCents,
            snapshot.OpexCents,
            lucroBruto,
            margemBruta,
            ticketMedio,
            snapshot.ReparacoesPagasCount,
            snapshot.TopReparacoesLucrativas.Select(r => new TopReparacaoLucrativaDto(
                r.Id,
                r.Numero,
                r.Equipamento,
                r.ClienteNome,
                r.ReceitaCents,
                r.CustoPecasCents,
                r.LucroCents)).ToList(),
            snapshot.TopPecasUsadas.Select(p => new TopPecaUsadaDto(
                p.PartId,
                p.Nome,
                p.Sku,
                p.Quantidade)).ToList(),
            snapshot.TopFornecedores.Select(f => new TopFornecedorDto(
                f.Nome,
                f.TotalCompradoCents)).ToList(),
            lucroOperacional,
            margemOperacional);
    }

    public async Task<TaxaDefeitoFornecedorResponse> GetTaxaDefeitoFornecedorAsync(int meses, CancellationToken ct = default)
    {
        if (_tenant.TenantId is null)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        if (meses is < 1 or > 60)
            throw new ValidationException("invalid_lookback", "Janela de análise deve estar entre 1 e 60 meses.");

        var fromUtc = DateTime.UtcNow.AddMonths(-meses);
        var rows = await _repo.GetTaxaDefeitoFornecedorAsync(fromUtc, ct);
        return new TaxaDefeitoFornecedorResponse(
            Meses: meses,
            DesdeUtc: fromUtc,
            Fornecedores: rows.Select(r => new FornecedorDefeitoDto(
                r.Nome,
                r.ItemsVendidos,
                r.ItemsComReparacao,
                r.TaxaDefeitoPct)).ToList());
    }

    public async Task<AnaliseVendasResponse> GetAnaliseVendasAsync(int ano, int trimestre, CancellationToken ct = default)
    {
        if (_tenant.TenantId is null)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var (fromUtc, toUtc) = Periodo(ano, trimestre);
        var artigos = await _repo.GetTopArtigosAsync(fromUtc, toUtc, top: 10, ct);
        var clientes = await _repo.GetTopClientesAsync(fromUtc, toUtc, top: 10, ct);
        return new AnaliseVendasResponse(ano, trimestre, fromUtc, toUtc, artigos, clientes);
    }

    private static (DateTime FromUtc, DateTime ToUtc) Periodo(int ano, int trimestre)
    {
        if (ano is < 2000 or > 2100) throw new ValidationException("invalid_year", "Ano invalido.");
        if (trimestre is < 1 or > 4) throw new ValidationException("invalid_quarter", "Trimestre invalido.");

        var from = new DateTime(ano, (trimestre - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(3));
    }
}

public sealed record RelatorioNegocioResponse(
    int Ano,
    int Trimestre,
    DateTime PeriodoDe,
    DateTime PeriodoAte,
    int ReceitaTotalCents,
    int ReceitaReparacoesCents,
    int ReceitaTrabalhosCents,
    int ReceitaVendasCents,
    int CustoPecasCents,
    int OpexCents,
    // Sprint 536: LucroBrutoCents = receita − peças (COGS), SEM OpEx. MargemMedia = margem BRUTA.
    int LucroBrutoCents,
    decimal MargemMedia,
    int TicketMedioCents,
    int ReparacoesPagasCount,
    IReadOnlyList<TopReparacaoLucrativaDto> TopReparacoesLucrativas,
    IReadOnlyList<TopPecaUsadaDto> TopPecasUsadas,
    IReadOnlyList<TopFornecedorDto> TopFornecedores,
    // Sprint 536: resultado operacional = lucro bruto − OpEx (despesas fixas) + margem respectiva.
    int LucroOperacionalCents = 0,
    decimal MargemOperacionalPct = 0);

public sealed record TopReparacaoLucrativaDto(
    Guid Id,
    int Numero,
    string Equipamento,
    string? ClienteNome,
    int ReceitaCents,
    int CustoPecasCents,
    int LucroCents);

public sealed record TopPecaUsadaDto(
    Guid PartId,
    string Nome,
    string? Sku,
    int Quantidade);

public sealed record TopFornecedorDto(
    string Nome,
    int TotalCompradoCents);

public sealed record TaxaDefeitoFornecedorResponse(
    int Meses,
    DateTime DesdeUtc,
    IReadOnlyList<FornecedorDefeitoDto> Fornecedores);

public sealed record FornecedorDefeitoDto(
    string Nome,
    int ItemsVendidos,
    int ItemsComReparacao,
    decimal TaxaDefeitoPct);
