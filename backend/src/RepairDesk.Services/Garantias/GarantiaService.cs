using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Audit;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Garantias;

public interface IGarantiaService
{
    Task<GarantiaAdminDto?> GetByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<GarantiaAdminDto?> GetByVendaAsync(Guid vendaId, CancellationToken ct = default);
    Task<GarantiaAdminDto> AnularAsync(Guid id, string motivo, CancellationToken ct = default);
    Task<(byte[] Pdf, string Filename)> RenderPdfAsync(Guid id, string portalBaseUrl, CancellationToken ct = default);
}

public sealed record GarantiaAdminDto(
    Guid Id,
    string Slug,
    GarantiaSourceType SourceType,
    Guid? ReparacaoId,
    Guid? VendaId,
    DateTime DataInicio,
    DateTime DataFim,
    int DiasGarantia,
    int DiasRestantes,
    bool Activa,
    bool Anulada,
    string? MotivoAnulacao,
    string? Cobertura,
    string? Exclusoes);

public sealed record AnularGarantiaRequest(string Motivo);

public class GarantiaService : IGarantiaService
{
    private readonly IGarantiaRepository _repo;
    private readonly IAuditLogger _audit;
    private readonly ITenantContext _tenant;
    private readonly ITenantRepository _tenants;

    public GarantiaService(IGarantiaRepository repo, IAuditLogger audit, ITenantContext tenant, ITenantRepository tenants)
    {
        _repo = repo;
        _audit = audit;
        _tenant = tenant;
        _tenants = tenants;
    }

    public async Task<(byte[] Pdf, string Filename)> RenderPdfAsync(Guid id, string portalBaseUrl, CancellationToken ct = default)
    {
        var g = await _repo.FindByIdWithSourceAsync(id, ct) ?? throw new NotFoundException("Garantia", id);
        var tenant = _tenant.TenantId is { } tid ? await _tenants.FindByIdAsync(tid, ct) : null;
        var data = BuildPdfData(g, tenant, portalBaseUrl);
        var pdf = GarantiaPdfRenderer.Render(data);
        return (pdf, $"garantia-{g.Slug}.pdf");
    }

    private static GarantiaPdfData BuildPdfData(Garantia g, Tenant? tenant, string portalBaseUrl)
    {
        var emissor = new GarantiaPdfEmissor(
            tenant?.LegalName ?? tenant?.Name ?? "Oficina",
            tenant?.Nif,
            tenant?.Address,
            tenant?.PostalCode,
            tenant?.Locality,
            tenant?.Phone,
            tenant?.Email,
            tenant?.Website,
            tenant?.PrimaryColor);

        string equipamento;
        string origemLabel;
        string docRef;
        string? clienteNome;
        string? clienteNif;
        var artigos = new List<GarantiaPdfArtigo>();

        if (g.SourceType == GarantiaSourceType.Venda && g.Venda is not null)
        {
            equipamento = g.Venda.Items.FirstOrDefault()?.Descricao ?? "Artigos vendidos";
            origemLabel = "Garantia de venda (DL 84/2021)";
            docRef = $"Venda #{g.Venda.Numero:D5}";
            clienteNome = g.Venda.Cliente?.Nome;
            clienteNif = g.Venda.Cliente?.Nif;
            // Sprint 93: PDF entregue ao cliente que comprou — IMEI completo, sem mascarar.
            // Mascaramento aplica-se apenas ao portal público /g/{slug} (anti-fraude por URL).
            artigos.AddRange(g.Venda.Items.Select(i =>
                new GarantiaPdfArtigo(
                    i.Descricao,
                    i.Quantidade,
                    string.IsNullOrEmpty(i.Imei) ? null : i.Imei)));
        }
        else
        {
            equipamento = g.Reparacao?.Equipamento ?? "Equipamento reparado";
            origemLabel = "Garantia de reparação";
            docRef = g.Reparacao is not null ? $"Reparação #{g.Reparacao.Numero:D5}" : "Reparação";
            clienteNome = g.Reparacao?.Cliente?.Nome;
            clienteNif = g.Reparacao?.Cliente?.Nif;
        }

        var portalUrl = $"{portalBaseUrl.TrimEnd('/')}/g/{g.Slug}";

        return new GarantiaPdfData(
            emissor, g.Slug, portalUrl, origemLabel, docRef,
            g.DataInicio, g.DataFim, g.DiasGarantia,
            equipamento, clienteNome, clienteNif, artigos,
            g.Cobertura ?? "Conformidade do bem com o descrito na fatura.",
            g.Exclusoes ?? "Danos por uso indevido, líquidos, quedas, abertura/desmontagem do equipamento.",
            g.Anulada, g.MotivoAnulacao);
    }

    public async Task<GarantiaAdminDto?> GetByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var g = await _repo.FindByReparacaoAsync(reparacaoId, ct);
        return g is null ? null : ToDto(g);
    }

    public async Task<GarantiaAdminDto?> GetByVendaAsync(Guid vendaId, CancellationToken ct = default)
    {
        var g = await _repo.FindByVendaAsync(vendaId, ct);
        return g is null ? null : ToDto(g);
    }

    public async Task<GarantiaAdminDto> AnularAsync(Guid id, string motivo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ValidationException("motivo_obrigatorio", "Motivo da anulação é obrigatório.");
        if (motivo.Length > 500)
            throw new ValidationException("motivo_demasiado_longo", "Motivo até 500 caracteres.");

        var g = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Garantia", id);
        if (g.Anulada)
            throw new ConflictException("ja_anulada", "Garantia já está anulada.");

        g.Anulada = true;
        g.MotivoAnulacao = motivo.Trim();
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(
            AuditAction.Update,
            "Garantia",
            g.Id,
            new { acao = "anulada", motivo = g.MotivoAnulacao, slug = g.Slug },
            _tenant.TenantId,
            ct: ct);

        return ToDto(g);
    }

    private static GarantiaAdminDto ToDto(Garantia g)
    {
        var agora = DateTime.UtcNow;
        var diasRestantes = (int)Math.Max(0, (g.DataFim - agora).TotalDays);
        var activa = !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;
        return new GarantiaAdminDto(
            g.Id, g.Slug, g.SourceType, g.ReparacaoId, g.VendaId,
            g.DataInicio, g.DataFim, g.DiasGarantia, diasRestantes,
            activa, g.Anulada, g.MotivoAnulacao, g.Cobertura, g.Exclusoes);
    }
}
