using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.EquipmentFields;

namespace RepairDesk.Services.Documents;

public interface IEntradaPdfService
{
    Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 450 (Doc 91 ponto 4): "Comprovativo de entrada de equipamento".
/// Junta dados loja + cliente + equipamento + avaria + portal URL e renderiza
/// via <see cref="EntradaPdfRenderer"/>. Endpoint expõe em /api/reparacoes/{id}/entrada.pdf.
/// </summary>
public sealed class EntradaPdfService : IEntradaPdfService
{
    private readonly IReparacaoRepository _reparacoes;
    private readonly IClienteRepository _clientes;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IEquipmentFieldService _equipmentFields;
    private readonly IConfiguration _config;

    public EntradaPdfService(
        IReparacaoRepository reparacoes,
        IClienteRepository clientes,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IEquipmentFieldService equipmentFields,
        IConfiguration config)
    {
        _reparacoes = reparacoes;
        _clientes = clientes;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _equipmentFields = equipmentFields;
        _config = config;
    }

    public async Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);
        var cliente = await _clientes.FindByIdAsync(rep.ClienteId, ct)
            ?? throw new NotFoundException("Cliente", rep.ClienteId);

        var tenant = _tenantContext.TenantId is not null
            ? await _tenants.FindByIdAsync(_tenantContext.TenantId.Value, ct)
            : null;

        var emissor = new OrcamentoEmissor(
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

        var clienteData = new OrcamentoCliente(cliente.Nome, cliente.Telefone, cliente.Email, cliente.Nif);

        // Sprint 359: campos de equipamento (cor, password, etc) marcados para portal/visíveis
        // ao cliente também fazem sentido aqui — é a fotografia do equipamento na entrada.
        var campos = await _equipmentFields.GetValuesAsync(rep.Id, visibleInPortalOnly: true, ct);
        var camposEquipamento = campos
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => new OrcamentoCampoEquipamento(c.Label, c.Value!))
            .ToList();

        var data = new EntradaEquipamentoData(
            Numero: $"R-{rep.Numero:D5}",
            // Reparacao não tem campo dedicado de "RecebidoEm" — usa CreatedAt (BaseEntity)
            // que representa a data/hora em que a reparação foi criada no sistema, o que
            // na prática coincide com a entrada do equipamento.
            RecebidoEm: rep.CreatedAt,
            Emissor: emissor,
            Cliente: clienteData,
            Equipamento: rep.Equipamento,
            Imei: rep.Imei,
            Avaria: rep.Avaria,
            // Sprint 450: estado físico não tem campo dedicado ainda — usa Diagnostico
            // (na entrada é tipicamente "telemovel apresenta-se com ecra rachado" etc).
            EstadoFisico: rep.Diagnostico,
            RecebidoPor: null, // futuro: capturar User.DisplayName no Create
            CamposEquipamento: camposEquipamento.Count > 0 ? camposEquipamento : null,
            TermosLoja: tenant?.TermosCondicoes,
            PortalUrl: BuildPortalUrl(rep.PublicSlug));

        var pdf = EntradaPdfRenderer.Render(data);
        return (pdf, $"Entrada_R-{rep.Numero:D5}.pdf");
    }

    private string? BuildPortalUrl(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var baseUrl = _config["Frontend:PortalBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        return $"{baseUrl}/r/{slug}";
    }
}
