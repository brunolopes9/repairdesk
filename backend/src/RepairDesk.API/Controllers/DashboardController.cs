using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Dashboard;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    private readonly IDashboardKpiHojeService _kpisHoje;

    public DashboardController(IDashboardService service, IDashboardKpiHojeService kpisHoje)
    {
        _service = service;
        _kpisHoje = kpisHoje;
    }

    [HttpGet("kpis-hoje")]
    public Task<DashboardKpisHojeResponse> GetKpisHoje([FromQuery] DateOnly? dia, CancellationToken ct)
    {
        var target = dia ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var diaUtc = target.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return _kpisHoje.GetAsync(diaUtc, ct);
    }

    [HttpGet]
    public Task<DashboardResponse> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (from is null || to is null)
            return _service.GetCurrentMonthAsync(ct);
        return _service.GetRangeAsync(from.Value, to.Value, ct);
    }

    [HttpGet("financeiro")]
    public Task<FinanceiroResponse> GetFinanceiro([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (from is null || to is null)
            return _service.GetFinanceiroCurrentMonthAsync(ct);
        return _service.GetFinanceiroRangeAsync(from.Value, to.Value, ct);
    }

    [HttpGet("alertas")]
    public Task<AlertasResponse> GetAlertas(CancellationToken ct) => _service.GetAlertasAsync(ct);

    /// <summary>
    /// Sprint 467 (Doc 90 Tier 2 #6 extensão): Devices com GarantiaFabricanteUntil
    /// entre hoje e hoje+N dias (default 30). Permite ao Bruno contactar o cliente
    /// para oferecer "garantia loja" antes do fabricante acabar — oportunidade de
    /// cross-sell. Ignora arquivados.
    /// </summary>
    [HttpGet("devices-garantia-a-expirar")]
    public async Task<DevicesGarantiaAExpirarResponse> GetDevicesGarantiaAExpirar(
        [FromQuery] int days = 30,
        [FromQuery] int limit = 30,
        [FromServices] AppDbContext db = null!,
        CancellationToken ct = default)
    {
        var janela = Math.Clamp(days, 1, 365);
        var pageSize = Math.Clamp(limit, 1, 100);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = hoje.AddDays(janela);

        var baseQ = db.Devices
            .AsNoTracking()
            .Where(d => !d.Arquivado
                && d.GarantiaFabricanteUntil != null
                && d.GarantiaFabricanteUntil >= hoje
                && d.GarantiaFabricanteUntil <= cutoff);

        var total = await baseQ.CountAsync(ct);
        var items = await baseQ
            .OrderBy(d => d.GarantiaFabricanteUntil)
            .Take(pageSize)
            .Select(d => new DeviceGarantiaItem(
                d.Id,
                d.ClienteId,
                d.Cliente!.Nome,
                d.Tipo,
                d.Marca,
                d.Modelo,
                d.Apelido,
                d.Imei,
                d.GarantiaFabricanteUntil!.Value))
            .ToListAsync(ct);

        return new DevicesGarantiaAExpirarResponse(items, total, janela);
    }

    /// <summary>
    /// Sprint 460 (Doc 91 follow-up): reparações em estado comunicável há > N horas sem
    /// comunicação Outbound desde a mudança. Mesma lógica do cron S458 — mas devolve a
    /// lista ao Dashboard em vez de só um push. Bruno clica num item → vai ao detalhe →
    /// usa CTA "Avisar X" (S456/S457) → fecha o loop.
    /// </summary>
    [HttpGet("avisos-pendentes")]
    public async Task<AvisosPendentesResponse> GetAvisosPendentes(
        [FromQuery] int horas = 8,
        [FromQuery] int limit = 20,
        [FromServices] AppDbContext db = null!,
        CancellationToken ct = default)
    {
        var horasLimite = Math.Clamp(horas, 1, 168);
        var pageSize = Math.Clamp(limit, 1, 50);
        var cutoff = DateTime.UtcNow.AddHours(-horasLimite);
        var estadosComunicaveis = new[] { RepairStatus.Diagnostico, RepairStatus.AguardaPeca, RepairStatus.Pronto };

        var baseQ = db.Reparacoes
            .AsNoTracking()
            .Where(r => estadosComunicaveis.Contains(r.Estado) && r.EstadoSince < cutoff)
            .Where(r => !db.ReparacaoComunicacoes
                .Where(c => c.ReparacaoId == r.Id && c.Direcao == ComunicacaoDirecao.Outbound)
                .Any(c => c.CreatedAt >= r.EstadoSince));

        var total = await baseQ.CountAsync(ct);
        var now = DateTime.UtcNow;
        var items = await baseQ
            .OrderBy(r => r.EstadoSince) // mais antigas primeiro — pior caso fica visível
            .Take(pageSize)
            .Select(r => new AvisoPendenteItem(
                r.Id,
                r.Numero,
                (int)r.Estado,
                r.Equipamento,
                r.Cliente!.Nome,
                r.Cliente!.Telefone,
                r.EstadoSince,
                (int)(now - r.EstadoSince).TotalHours))
            .ToListAsync(ct);

        return new AvisosPendentesResponse(items, total, horasLimite);
    }

    /// <summary>
    /// Sprint 483 (Doc 91): reparações cuja última mensagem no portal foi do cliente
    /// (Inbound PortalCliente) sem resposta Outbound posterior. Fecha o loop S480/S482 —
    /// o cliente escreveu e está à espera. Bruno clica → detalhe → "Responder no portal".
    /// </summary>
    [HttpGet("mensagens-por-responder")]
    public async Task<MensagensPorResponderResponse> GetMensagensPorResponder(
        [FromQuery] int limit = 20,
        [FromServices] AppDbContext db = null!,
        CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(limit, 1, 50);

        // Reparações com pelo menos uma mensagem Inbound do portal, sem nenhuma resposta
        // Outbound do portal igual ou posterior à última Inbound (i.e. cliente falou por último).
        var baseQ = db.Reparacoes
            .AsNoTracking()
            .Where(r => db.ReparacaoComunicacoes.Any(c =>
                c.ReparacaoId == r.Id
                && c.Tipo == ComunicacaoTipo.PortalCliente
                && c.Direcao == ComunicacaoDirecao.Inbound))
            .Where(r => !db.ReparacaoComunicacoes.Any(o =>
                o.ReparacaoId == r.Id
                && o.Tipo == ComunicacaoTipo.PortalCliente
                && o.Direcao == ComunicacaoDirecao.Outbound
                && o.CreatedAt >= db.ReparacaoComunicacoes
                    .Where(i => i.ReparacaoId == r.Id
                        && i.Tipo == ComunicacaoTipo.PortalCliente
                        && i.Direcao == ComunicacaoDirecao.Inbound)
                    .Max(i => i.CreatedAt)));

        var total = await baseQ.CountAsync(ct);
        var now = DateTime.UtcNow;
        var items = await baseQ
            .Select(r => new
            {
                r.Id,
                r.Numero,
                Estado = (int)r.Estado,
                r.Equipamento,
                ClienteNome = r.Cliente!.Nome,
                Ultima = db.ReparacaoComunicacoes
                    .Where(i => i.ReparacaoId == r.Id
                        && i.Tipo == ComunicacaoTipo.PortalCliente
                        && i.Direcao == ComunicacaoDirecao.Inbound)
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => new { i.Texto, i.CreatedAt })
                    .First(),
            })
            .OrderBy(x => x.Ultima.CreatedAt) // mais antigas à espera primeiro
            .Take(pageSize)
            .ToListAsync(ct);

        var result = items
            .Select(x => new MensagemPorResponderItem(
                x.Id,
                x.Numero,
                x.Estado,
                x.Equipamento,
                x.ClienteNome,
                x.Ultima.Texto.Length > 120 ? x.Ultima.Texto[..120] + "…" : x.Ultima.Texto,
                x.Ultima.CreatedAt,
                (int)(now - x.Ultima.CreatedAt).TotalHours))
            .ToList();

        return new MensagensPorResponderResponse(result, total);
    }

    [HttpGet("avaliacoes")]
    public Task<AvaliacoesDashboardResponse> GetAvaliacoes(CancellationToken ct) => _service.GetAvaliacoesAsync(ct);

    [HttpGet("tendencia")]
    public Task<TendenciaResponse> GetTendencia([FromQuery] int meses = 6, CancellationToken ct = default)
        => _service.GetTendenciaAsync(meses, ct);

    /// <summary>Sprint 429 (Doc 88 IDEIAS 1): cash flow diário para gráfico Dashboard. Default 30 dias, max 90.</summary>
    [HttpGet("cashflow")]
    public Task<CashflowResponse> GetCashflow([FromQuery] int days = 30, CancellationToken ct = default)
        => _service.GetCashflowAsync(days, ct);

    [HttpGet("top-reparacoes")]
    public Task<TopReparacoesResponse> GetTopReparacoes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 5,
        CancellationToken ct = default)
    {
        if (from is null || to is null)
            return _service.GetTopReparacoesCurrentMonthAsync(limit, ct);
        return _service.GetTopReparacoesAsync(from.Value, to.Value, limit, ct);
    }

    [HttpGet("garantias-resumo")]
    public Task<GarantiasResumoResponse> GetGarantiasResumo(
        [FromQuery] int dias = 30,
        [FromQuery] int limit = 8,
        CancellationToken ct = default)
        => _service.GetGarantiasResumoAsync(dias, limit, ct);

    /// <summary>
    /// Reparações cujo IMEI bate uma venda anterior do mesmo tenant — indicador de
    /// qualidade pós-venda. Útil para Direito de Regresso art. 21º DL 84/2021.
    /// </summary>
    [HttpGet("reparacoes-em-garantia")]
    public Task<ReparacoesEmGarantiaResponse> GetReparacoesEmGarantia(
        [FromQuery] int dias = 90,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
        => _service.GetReparacoesEmGarantiaAsync(dias, limit, ct);

    /// <summary>Export CSV das reparações em garantia interna — para enviar ao fornecedor (Molano).</summary>
    [HttpGet("reparacoes-em-garantia/export.csv")]
    public async Task<IActionResult> ExportReparacoesEmGarantia(
        [FromQuery] int dias = 90,
        CancellationToken ct = default)
    {
        var bytes = await _service.ExportReparacoesEmGarantiaCsvAsync(dias, ct);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return File(bytes, "text/csv; charset=utf-8", $"reparacoes_em_garantia_{stamp}.csv");
    }
}
