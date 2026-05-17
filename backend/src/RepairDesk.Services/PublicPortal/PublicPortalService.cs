using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.PublicPortal;

public interface IPublicPortalService
{
    Task<PublicRepairDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<PublicRepairDto> AprovarOrcamentoAsync(string slug, bool aceitar, CancellationToken ct = default);
    Task<PublicGarantiaDto> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default);
    Task<AvaliacaoSubmittedDto> SubmeterAvaliacaoAsync(string repairSlug, int score, string? comentario, bool publicarTestemunho, CancellationToken ct = default);
}

public class PublicPortalService : IPublicPortalService
{
    private readonly IReparacaoRepository _repo;
    private readonly ITenantRepository _tenants;
    private readonly IDiagnosticoRepository _diagnostico;
    private readonly IGarantiaRepository _garantias;
    private readonly IAvaliacaoRepository _avaliacoes;
    private readonly IReparacaoFotoRepository _fotos;

    public PublicPortalService(
        IReparacaoRepository repo,
        ITenantRepository tenants,
        IDiagnosticoRepository diagnostico,
        IGarantiaRepository garantias,
        IAvaliacaoRepository avaliacoes,
        IReparacaoFotoRepository fotos)
    {
        _repo = repo;
        _tenants = tenants;
        _diagnostico = diagnostico;
        _garantias = garantias;
        _avaliacoes = avaliacoes;
        _fotos = fotos;
    }

    public async Task<PublicGarantiaDto> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Garantia", slug);

        var g = await _garantias.FindBySlugAsync(slug, ct)
            ?? throw new NotFoundException("Garantia", slug);

        var tenant = await _tenants.FindByIdAsync(g.TenantId, ct);
        var agora = DateTime.UtcNow;
        var diasRestantes = (int)Math.Max(0, (g.DataFim - agora).TotalDays);
        var activa = !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;

        return new PublicGarantiaDto(
            Slug: g.Slug,
            EquipamentoPublico: g.Reparacao?.Equipamento ?? "Equipamento",
            Loja: tenant?.LegalName ?? tenant?.Name ?? "Oficina",
            LogoUrl: tenant?.LogoUrl,
            DataInicio: g.DataInicio,
            DataFim: g.DataFim,
            DiasGarantia: g.DiasGarantia,
            Activa: activa,
            Anulada: g.Anulada,
            DiasRestantes: diasRestantes,
            Cobertura: g.Cobertura,
            Exclusoes: g.Exclusoes);
    }

    public async Task<AvaliacaoSubmittedDto> SubmeterAvaliacaoAsync(string repairSlug, int score, string? comentario, bool publicarTestemunho, CancellationToken ct = default)
    {
        if (score < 1 || score > 5)
            throw new ValidationException("score_invalido", "Score deve ser entre 1 e 5.");
        if (string.IsNullOrWhiteSpace(repairSlug))
            throw new NotFoundException("Reparacao", repairSlug);

        var rep = await _repo.FindByPublicSlugWithTimelineAsync(repairSlug, ct)
            ?? throw new NotFoundException("Reparacao", repairSlug);

        if (rep.Estado != RepairStatus.Entregue)
            throw new ConflictException("nao_entregue", "Esta reparação ainda não foi entregue.");

        var existente = await _avaliacoes.FindByReparacaoAsync(rep.Id, ct);
        if (existente is not null)
            throw new ConflictException("ja_avaliado", "Esta reparação já foi avaliada.");

        var tenant = await _tenants.FindByIdAsync(rep.TenantId, ct);
        var dirigirGoogle = score >= 4 && !string.IsNullOrWhiteSpace(tenant?.GoogleReviewUrl);

        var avaliacao = new Avaliacao
        {
            TenantId = rep.TenantId,
            ReparacaoId = rep.Id,
            Score = score,
            Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(),
            PublicarTestemunho = publicarTestemunho,
            PedidoGoogleReview = dirigirGoogle,
        };
        await _avaliacoes.AddAsync(avaliacao, ct);
        await _avaliacoes.SaveAsync(ct);

        return new AvaliacaoSubmittedDto(score, avaliacao.Comentario, dirigirGoogle ? tenant!.GoogleReviewUrl : null);
    }

    public async Task<PublicRepairDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Reparacao", slug);

        var rep = await _repo.FindByPublicSlugWithTimelineAsync(slug, ct)
            ?? throw new NotFoundException("Reparacao", slug);

        // Compliance: não revelar reparações > 2 anos
        if (rep.RecebidoEm() < DateTime.UtcNow.AddYears(-2))
            throw new NotFoundException("Reparacao", slug);

        var tenant = await _tenants.FindByIdAsync(rep.TenantId, ct);
        var diag = await _diagnostico.FindExecucaoByReparacaoAsync(rep.Id, ct);
        var garantia = await _garantias.FindByReparacaoAsync(rep.Id, ct);
        var avaliacao = await _avaliacoes.FindByReparacaoAsync(rep.Id, ct);
        var fotos = await _fotos.ListPublicByReparacaoIdAsync(rep.Id, ct);
        return ToDto(rep, tenant, diag, garantia, avaliacao is not null, fotos);
    }

    public async Task<PublicRepairDto> AprovarOrcamentoAsync(string slug, bool aceitar, CancellationToken ct = default)
    {
        var rep = await _repo.FindByPublicSlugWithTimelineAsync(slug, ct)
            ?? throw new NotFoundException("Reparacao", slug);

        if (rep.OrcamentoCents is null)
            throw new ConflictException("sem_orcamento", "Esta reparação não tem orçamento para aprovar.");
        if (rep.OrcamentoAprovado)
            throw new ConflictException("ja_aprovado", "Este orçamento já foi aprovado anteriormente.");

        rep.OrcamentoAprovado = aceitar;
        if (!aceitar)
        {
            rep.Estado = Core.Enums.RepairStatus.Cancelado;
            rep.EstadoSince = DateTime.UtcNow;
        }
        await _repo.SaveAsync(ct);

        var tenant = await _tenants.FindByIdAsync(rep.TenantId, ct);
        var diag = await _diagnostico.FindExecucaoByReparacaoAsync(rep.Id, ct);
        var garantia = await _garantias.FindByReparacaoAsync(rep.Id, ct);
        var avaliacao = await _avaliacoes.FindByReparacaoAsync(rep.Id, ct);
        var fotos = await _fotos.ListPublicByReparacaoIdAsync(rep.Id, ct);
        return ToDto(rep, tenant, diag, garantia, avaliacao is not null, fotos);
    }

    private static PublicRepairDto ToDto(Reparacao rep, Tenant? tenant, DiagnosticoExecucao? diag, Garantia? garantia, bool jaAvaliado, IReadOnlyList<ReparacaoFoto> fotos)
    {
        var primeiroNome = rep.Cliente?.Nome?.Split(' ').FirstOrDefault() ?? "Cliente";
        var loja = new PublicLoja(
            tenant?.LegalName ?? tenant?.Name ?? "Oficina",
            tenant?.Phone,
            tenant?.Email,
            tenant?.Website,
            tenant?.LogoUrl);

        var timeline = rep.Timeline
            .OrderBy(t => t.MudouEm)
            .Select(t => new PublicTimelineEntry(PublicEstadoMapper.From(t.EstadoTo), t.MudouEm))
            .ToList();

        // Diagnóstico só exposto se está completado (caso contrário pode confundir cliente)
        int? healthScore = null;
        var destaques = new List<string>();
        if (diag is not null && diag.CompletadoEm.HasValue)
        {
            healthScore = diag.Score;
            destaques = diag.Items
                .Where(i => i.Resultado == DiagnosticoResultado.Avaria || i.Resultado == DiagnosticoResultado.Marginal)
                .OrderBy(i => i.Ordem)
                .Select(i => i.Resultado == DiagnosticoResultado.Marginal ? $"⚠️ {i.Label}" : $"❌ {i.Label}")
                .Take(8)
                .ToList();
        }

        return new PublicRepairDto(
            Slug: rep.PublicSlug!,
            EquipamentoPublico: rep.Equipamento,
            AvariaPublica: rep.Avaria,
            Diagnostico: rep.Diagnostico,
            Estado: PublicEstadoMapper.From(rep.Estado),
            EstadoSince: rep.EstadoSince,
            RecebidoEm: rep.RecebidoEm(),
            EntregueEm: rep.EntregueEm,
            OrcamentoCents: rep.OrcamentoCents,
            OrcamentoAprovado: rep.OrcamentoAprovado,
            TemPrecoFinal: rep.PrecoFinalCents.HasValue,
            PrecoFinalCents: rep.PrecoFinalCents,
            Loja: loja,
            ClientePrimeiroNome: primeiroNome,
            Timeline: timeline,
            HealthScore: healthScore,
            DiagnosticoDestaques: destaques,
            GarantiaSlug: garantia?.Slug,
            JaAvaliado: jaAvaliado,
            Fotos: fotos.Select(f => new PublicFotoDto(f.Id, (int)f.Tipo, f.Legenda, f.CreatedAt)).ToList());
    }
}

internal static class ReparacaoExtensions
{
    public static DateTime RecebidoEm(this Reparacao r) =>
        r.Timeline.OrderBy(t => t.MudouEm).FirstOrDefault()?.MudouEm ?? r.CreatedAt;
}
