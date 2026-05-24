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
}

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
        var lucroBruto = receitaTotal - snapshot.CustoPecasCents - snapshot.OpexCents;
        var margemMedia = receitaTotal == 0
            ? 0
            : Math.Round(lucroBruto * 100m / receitaTotal, 2, MidpointRounding.AwayFromZero);
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
            margemMedia,
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
                f.TotalCompradoCents)).ToList());
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
    int LucroBrutoCents,
    decimal MargemMedia,
    int TicketMedioCents,
    int ReparacoesPagasCount,
    IReadOnlyList<TopReparacaoLucrativaDto> TopReparacoesLucrativas,
    IReadOnlyList<TopPecaUsadaDto> TopPecasUsadas,
    IReadOnlyList<TopFornecedorDto> TopFornecedores);

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
