using RepairDesk.Core.Abstractions;

namespace RepairDesk.Services.Dashboard;

public interface IDashboardKpiHojeService
{
    Task<DashboardKpisHojeResponse> GetAsync(DateTime diaUtc, CancellationToken ct = default);
}

public sealed class DashboardKpiHojeService : IDashboardKpiHojeService
{
    private readonly IDashboardRepository _repo;

    public DashboardKpiHojeService(IDashboardRepository repo)
    {
        _repo = repo;
    }

    public async Task<DashboardKpisHojeResponse> GetAsync(DateTime diaUtc, CancellationToken ct = default)
    {
        var dia = DateTime.SpecifyKind(diaUtc.Date, DateTimeKind.Utc);
        var snapshot = await _repo.GetKpisHojeAsync(dia, ct);

        return new DashboardKpisHojeResponse(
            snapshot.ReparacoesEmCurso,
            snapshot.ValorAReceberCents,
            snapshot.StockCriticoCount,
            snapshot.Receita7d,
            snapshot.ReparacoesEntregues7d,
            snapshot.LucroEstimado7dCents,
            snapshot.TempoMedioReparacaoHoras,
            snapshot.TopReparacoesLucrativas30d.Select(r => new DashboardTopReparacaoLucrativaDto(
                r.Id,
                r.Numero,
                r.Equipamento,
                r.ClienteNome,
                r.ReceitaCents,
                r.CustoPecasCents,
                r.LucroCents)).ToList(),
            snapshot.TopPecasUsadas30d.Select(p => new DashboardTopPecaUsadaDto(
                p.PartId,
                p.Nome,
                p.Sku,
                p.Quantidade)).ToList());
    }
}

public sealed record DashboardKpisHojeResponse(
    int ReparacoesEmCurso,
    int ValorAReceberCents,
    int StockCriticoCount,
    IReadOnlyList<int> Receita7d,
    int ReparacoesEntregues7d,
    int LucroEstimado7dCents,
    double? TempoMedioReparacaoHoras,
    IReadOnlyList<DashboardTopReparacaoLucrativaDto> TopReparacoesLucrativas30d,
    IReadOnlyList<DashboardTopPecaUsadaDto> TopPecasUsadas30d);

public sealed record DashboardTopReparacaoLucrativaDto(
    Guid Id,
    int Numero,
    string Equipamento,
    string? ClienteNome,
    int ReceitaCents,
    int CustoPecasCents,
    int LucroCents);

public sealed record DashboardTopPecaUsadaDto(
    Guid PartId,
    string Nome,
    string? Sku,
    int Quantidade);
