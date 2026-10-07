using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface IEntregaPdfService
{
    Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 451 / Doc 94 Fase 4c: "Recibo de entrega" de uma reparação (Venda tipo Reparação) — par do
/// comprovativo de entrada. Linhas = linhas da venda; inclui o link da garantia (se emitida) e a
/// assinatura do cliente na entrega. Não é documento fiscal (a fatura é emitida no Moloni).
/// </summary>
public sealed class EntregaPdfService : IEntregaPdfService
{
    private readonly IVendaRepository _vendas;
    private readonly ITenantRepository _tenants;
    private readonly IGarantiaRepository _garantias;
    private readonly IVendaAssinaturaRepository _assinaturas;
    private readonly IConfiguration _config;

    public EntregaPdfService(
        IVendaRepository vendas,
        ITenantRepository tenants,
        IGarantiaRepository garantias,
        IVendaAssinaturaRepository assinaturas,
        IConfiguration config)
    {
        _vendas = vendas;
        _tenants = tenants;
        _garantias = garantias;
        _assinaturas = assinaturas;
        _config = config;
    }

    public async Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default)
    {
        var v = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (v.Tipo != VendaTipo.Reparacao)
            throw new ValidationException("nao_e_reparacao", "O recibo de entrega só existe para reparações.");
        var cliente = v.Cliente ?? throw new ValidationException("cliente_obrigatorio", "A reparação não tem cliente.");
        var tenant = await _tenants.FindByIdAsync(v.TenantId, ct);
        var assinatura = await _assinaturas.FindAsync(v.Id, AssinaturaTipo.Entrega, ct);
        var garantia = await _garantias.FindByVendaAsync(v.Id, ct);

        var linhas = v.Items
            .OrderBy(i => i.CreatedAt)
            .Select(i => new OrcamentoLinha(i.Quantidade > 1 ? $"{i.Descricao} (×{i.Quantidade})" : i.Descricao, i.TotalCents))
            .ToList();

        var data = new EntregaEquipamentoData(
            Numero: $"R-{v.Numero:D5}",
            EntregueEm: v.Estado == VendaEstado.Entregue ? v.Data : DateTime.UtcNow,
            Emissor: PdfEmissor.From(tenant),
            Cliente: new OrcamentoCliente(cliente.Nome, cliente.Telefone, cliente.Email, cliente.Nif),
            Equipamento: v.Equipamento ?? "Equipamento",
            Imei: null,
            Tipo: null,
            Diagnostico: null,
            ResumoIntervencao: v.Problema ?? "Reparação",
            Linhas: linhas,
            TotalPagoCents: v.TotalCents,
            DiasGarantia: garantia?.DiasGarantia ?? tenant?.GarantiaDiasDefault ?? 90,
            GarantiaCobertura: garantia?.Cobertura ?? tenant?.GarantiaCoberturaDefault,
            EntreguePor: null,
            GarantiaUrl: GarantiaUrl(garantia?.Slug),
            PortalUrl: EntradaPdfService.PortalUrl(_config, v.PublicSlug),
            AssinaturaPng: assinatura?.PngBytes,
            AssinaturaEm: assinatura?.AssinadaEm);

        return (EntregaPdfRenderer.Render(data), $"Entrega_R-{v.Numero:D5}.pdf");
    }

    private string? GarantiaUrl(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var baseUrl = _config["Frontend:PortalBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl}/g/{slug}";
    }
}
