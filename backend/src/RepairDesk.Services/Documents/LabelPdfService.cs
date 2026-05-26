using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface ILabelPdfService
{
    Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 347 (Doc 83 Pillar 4): produz PDF de etiqueta 62×29mm para uma reparação.
/// QR aponta para portal cliente (publicSlug) quando existe, senão para número interno.
/// </summary>
public class LabelPdfService : ILabelPdfService
{
    private readonly IReparacaoRepository _reparacoes;
    private readonly IClienteRepository _clientes;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _config;

    public LabelPdfService(
        IReparacaoRepository reparacoes,
        IClienteRepository clientes,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IConfiguration config)
    {
        _reparacoes = reparacoes;
        _clientes = clientes;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _config = config;
    }

    public async Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);
        var cliente = await _clientes.FindByIdAsync(rep.ClienteId, ct)
            ?? throw new NotFoundException("Cliente", rep.ClienteId);

        string? tenantNome = null;
        if (_tenantContext.TenantId is { } tenantId)
        {
            var tenant = await _tenants.FindByIdAsync(tenantId, ct);
            tenantNome = tenant?.LegalName ?? tenant?.Name;
        }

        var qrPayload = BuildQrPayload(rep.PublicSlug, rep.Numero);

        var data = new LabelPdfData(
            Numero: $"#{rep.Numero:D5}",
            ClienteNome: cliente.Nome,
            ClienteTelefone: cliente.Telefone,
            Equipamento: rep.Equipamento,
            Imei: rep.Imei,
            QrPayload: qrPayload,
            TenantNome: tenantNome);

        var pdf = LabelPdfRenderer.Render(data);
        var filename = $"etiqueta-{rep.Numero:D5}.pdf";
        return (pdf, filename);
    }

    private string BuildQrPayload(string? slug, int numero)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var baseUrl = _config["Frontend:PortalBaseUrl"]?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(baseUrl)) return $"{baseUrl}/r/{slug}";
        }
        return $"REPDESK:REP:{numero:D5}";
    }
}
