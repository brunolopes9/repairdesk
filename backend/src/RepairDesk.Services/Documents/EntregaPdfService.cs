using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface IEntregaPdfService
{
    Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 451 (Doc 91 ponto 4): "Recibo de entrega" — par do S450.
/// Constrói linhas (peças+mão-de-obra) usando o mesmo padrão do OrcamentoPdfService
/// e renderiza via <see cref="EntregaPdfRenderer"/>. Inclui slug de garantia
/// (se existe) para QR direto ao portal de reclamação.
/// </summary>
public sealed class EntregaPdfService : IEntregaPdfService
{
    private readonly IReparacaoRepository _reparacoes;
    private readonly IClienteRepository _clientes;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IDespesaRepository _despesas;
    private readonly IPartRepository _parts;
    private readonly IGarantiaRepository _garantias;
    private readonly IReparacaoAssinaturaRepository _assinaturas;
    private readonly IConfiguration _config;

    public EntregaPdfService(
        IReparacaoRepository reparacoes,
        IClienteRepository clientes,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IDespesaRepository despesas,
        IPartRepository parts,
        IGarantiaRepository garantias,
        IReparacaoAssinaturaRepository assinaturas,
        IConfiguration config)
    {
        _reparacoes = reparacoes;
        _clientes = clientes;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _despesas = despesas;
        _parts = parts;
        _garantias = garantias;
        _assinaturas = assinaturas;
        _config = config;
    }

    public async Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);
        var cliente = await _clientes.FindByIdAsync(rep.ClienteId, ct)
            ?? throw new NotFoundException("Cliente", rep.ClienteId);

        var assinatura = await _assinaturas.FindAsync(rep.Id, Core.Enums.AssinaturaTipo.Entrega, ct);

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

        // Mesma lógica de linhas que OrcamentoPdfService.ForReparacaoAsync.
        var totalDespesas = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        var movimentos = await _parts.MovimentosAsync(partId: null, reparacaoId: rep.Id, ct);
        var precoTotal = rep.PrecoFinalCents ?? rep.OrcamentoCents ?? 0;
        var linhas = new List<OrcamentoLinha>();
        var pecasSubtotal = 0;

        var pecasPorPart = movimentos
            .GroupBy(m => m.PartId)
            .Select(g => new
            {
                Nome = g.First().Part?.Nome ?? "Peça",
                NetQty = -g.Sum(m => m.Quantidade),
                UnitCost = g.First().Part?.CustoUnitarioCents ?? 0,
            })
            .Where(p => p.NetQty > 0 && p.UnitCost > 0)
            .ToList();
        foreach (var p in pecasPorPart)
        {
            var lineTotal = p.NetQty * p.UnitCost;
            pecasSubtotal += lineTotal;
            var label = p.NetQty > 1 ? $"{p.Nome} (×{p.NetQty})" : p.Nome;
            linhas.Add(new OrcamentoLinha(label, lineTotal));
        }
        if (totalDespesas > 0)
        {
            linhas.Add(new OrcamentoLinha(linhas.Count == 0 ? "Peças e material" : "Material adicional", totalDespesas));
            pecasSubtotal += totalDespesas;
        }
        if (linhas.Count > 0)
        {
            var maoDeObra = precoTotal - pecasSubtotal;
            if (maoDeObra > 0)
                linhas.Add(new OrcamentoLinha($"Mão-de-obra · {rep.Equipamento}".Trim(), maoDeObra));
        }
        else if (precoTotal > 0)
        {
            var desc = string.IsNullOrWhiteSpace(rep.Avaria)
                ? $"Reparação {rep.Equipamento}".Trim()
                : $"Reparação {rep.Equipamento} — {rep.Avaria}".Trim();
            linhas.Add(new OrcamentoLinha(desc, precoTotal));
        }

        // Garantia: dias do tenant (S127 default). Se já existe Garantia para esta
        // reparação, usa esse Slug para o QR; senão omite.
        var garantia = await _garantias.FindByReparacaoAsync(rep.Id, ct);
        var diasGarantia = tenant?.GarantiaDiasDefault ?? 90;
        var garantiaUrl = garantia is not null ? BuildGarantiaUrl(garantia.Slug) : null;

        var data = new EntregaEquipamentoData(
            Numero: $"R-{rep.Numero:D5}",
            EntregueEm: rep.EntregueEm ?? DateTime.UtcNow,
            Emissor: emissor,
            Cliente: clienteData,
            Equipamento: rep.Equipamento,
            Imei: rep.Imei,
            // Sprint 491: tipo/categoria (S475) com label pt-PT partilhado (DeviceCategoryLabels).
            Tipo: DeviceCategoryLabels.PtLabel(rep.Categoria),
            Diagnostico: rep.Diagnostico,
            ResumoIntervencao: rep.Notas ?? rep.Avaria,
            Linhas: linhas,
            TotalPagoCents: precoTotal,
            DiasGarantia: diasGarantia,
            GarantiaCobertura: tenant?.GarantiaCoberturaDefault,
            EntreguePor: null,
            GarantiaUrl: garantiaUrl,
            PortalUrl: BuildPortalUrl(rep.PublicSlug),
            // Sprint 551: assinatura digital recolhida no balcão na entrega, se existir.
            AssinaturaPng: assinatura?.PngBytes,
            AssinaturaEm: assinatura?.AssinadaEm);

        var pdf = EntregaPdfRenderer.Render(data);
        return (pdf, $"Entrega_R-{rep.Numero:D5}.pdf");
    }

    private string? BuildPortalUrl(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var baseUrl = _config["Frontend:PortalBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl}/r/{slug}";
    }

    private string? BuildGarantiaUrl(string slug)
    {
        var baseUrl = _config["Frontend:PortalBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl}/g/{slug}";
    }
}
