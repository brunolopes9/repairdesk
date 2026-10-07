using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface IEntradaPdfService
{
    Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 450 / Doc 94 Fase 4c: "Comprovativo de entrada de equipamento" de uma reparação (Venda
/// tipo Reparação). Junta loja + cliente + equipamento + avaria + link do portal + assinatura do
/// cliente e renderiza via <see cref="EntradaPdfRenderer"/>.
/// </summary>
public sealed class EntradaPdfService : IEntradaPdfService
{
    private readonly IVendaRepository _vendas;
    private readonly ITenantRepository _tenants;
    private readonly IVendaAssinaturaRepository _assinaturas;
    private readonly IConfiguration _config;

    public EntradaPdfService(
        IVendaRepository vendas,
        ITenantRepository tenants,
        IVendaAssinaturaRepository assinaturas,
        IConfiguration config)
    {
        _vendas = vendas;
        _tenants = tenants;
        _assinaturas = assinaturas;
        _config = config;
    }

    public async Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default)
    {
        var v = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (v.Tipo != VendaTipo.Reparacao)
            throw new ValidationException("nao_e_reparacao", "O comprovativo de entrada só existe para reparações.");
        var cliente = v.Cliente ?? throw new ValidationException("cliente_obrigatorio", "A reparação não tem cliente.");
        var tenant = await _tenants.FindByIdAsync(v.TenantId, ct);
        var assinatura = await _assinaturas.FindAsync(v.Id, AssinaturaTipo.Entrada, ct);

        var data = new EntradaEquipamentoData(
            Numero: $"R-{v.Numero:D5}",
            RecebidoEm: v.CreatedAt,
            Emissor: PdfEmissor.From(tenant),
            Cliente: new OrcamentoCliente(cliente.Nome, cliente.Telefone, cliente.Email, cliente.Nif),
            Equipamento: v.Equipamento ?? "Equipamento",
            Imei: null,
            Avaria: v.Problema ?? "—",
            EstadoFisico: null,
            Tipo: null,
            RecebidoPor: null,
            CamposEquipamento: null,
            TermosLoja: tenant?.TermosCondicoes,
            PortalUrl: PortalUrl(_config, v.PublicSlug),
            AssinaturaPng: assinatura?.PngBytes,
            AssinaturaEm: assinatura?.AssinadaEm);

        return (EntradaPdfRenderer.Render(data), $"Entrada_R-{v.Numero:D5}.pdf");
    }

    internal static string? PortalUrl(IConfiguration config, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var baseUrl = config["Frontend:PortalBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl}/r/{slug}";
    }
}

/// <summary>Dados da loja para cabeçalho dos PDFs operacionais (sem IBAN/CAE — não são documentos fiscais).</summary>
internal static class PdfEmissor
{
    public static OrcamentoEmissor From(Core.Entities.Tenant? tenant) => new(
        Nome: tenant?.LegalName ?? tenant?.Name ?? "Loja",
        Nif: tenant?.Nif,
        Morada: tenant?.Address,
        CodigoPostal: tenant?.PostalCode,
        Localidade: tenant?.Locality,
        Telefone: tenant?.Phone,
        Email: tenant?.Email,
        Website: tenant?.Website,
        Iban: null,
        CaePrincipal: null,
        CaeSecundarios: null,
        LogoUrl: tenant?.LogoUrl,
        PrimaryColor: tenant?.PrimaryColor,
        TermosCondicoes: tenant?.TermosCondicoes);
}
