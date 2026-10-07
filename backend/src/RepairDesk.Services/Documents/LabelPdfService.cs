using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface ILabelPdfService
{
    Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 347 / Doc 94: produz PDF de etiqueta 62×29mm para uma reparação (Venda tipo Reparação).
/// QR aponta para portal cliente (publicSlug) quando existe, senão para número interno.
/// </summary>
public class LabelPdfService : ILabelPdfService
{
    private readonly IVendaRepository _vendas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _config;

    public LabelPdfService(
        IVendaRepository vendas,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IConfiguration config)
    {
        _vendas = vendas;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _config = config;
    }

    public async Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default)
    {
        var rep = await _vendas.FindByIdAsync(vendaId, ct)
            ?? throw new NotFoundException("Venda", vendaId);

        string? tenantNome = null;
        if (_tenantContext.TenantId is { } tenantId)
        {
            var tenant = await _tenants.FindByIdAsync(tenantId, ct);
            tenantNome = tenant?.LegalName ?? tenant?.Name;
        }

        var qrPayload = BuildQrPayload(rep.PublicSlug, rep.Numero);

        var data = new LabelPdfData(
            Numero: $"#{rep.Numero:D5}",
            ClienteNome: rep.Cliente?.Nome ?? "Consumidor final",
            ClienteTelefone: rep.Cliente?.Telefone,
            Equipamento: rep.Equipamento ?? rep.Problema ?? "Equipamento",
            Imei: null,
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
