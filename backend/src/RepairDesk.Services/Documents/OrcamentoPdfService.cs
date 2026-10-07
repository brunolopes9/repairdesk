using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

public interface IOrcamentoPdfService
{
    /// <summary>Doc 94 Fase 4: orçamento de uma Venda (reparação, serviço ou produto) para enviar ao cliente.</summary>
    Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default);
}

public class OrcamentoPdfService : IOrcamentoPdfService
{
    private readonly IVendaRepository _vendas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;

    public OrcamentoPdfService(IVendaRepository vendas, ITenantRepository tenants, ITenantContext tenantContext)
    {
        _vendas = vendas;
        _tenants = tenants;
        _tenantContext = tenantContext;
    }

    public async Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default)
    {
        var v = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        var emissor = await BuildEmissorAsync(ct);
        var linhas = v.Items.OrderBy(i => i.CreatedAt)
            .Select(i => new OrcamentoLinha(i.Quantidade > 1 ? $"{i.Quantidade} × {i.Descricao}" : i.Descricao, i.TotalCents))
            .ToList();

        var data = new OrcamentoData(
            Numero: $"V-{v.Numero:D5}",
            Tipo: v.Tipo switch { Core.Enums.VendaTipo.Reparacao => "Reparação", Core.Enums.VendaTipo.Servico => "Serviço", _ => "Venda" },
            Data: DateTime.UtcNow,
            ValidoAte: DateTime.UtcNow.AddDays(30),
            Emissor: emissor,
            Cliente: v.Cliente is not null
                ? new OrcamentoCliente(v.Cliente.Nome, v.Cliente.Telefone, v.Cliente.Email, v.Cliente.Nif)
                : new OrcamentoCliente("(cliente a definir)", null, null, null),
            Titulo: v.Equipamento ?? v.Problema ?? "Orçamento",
            Descricao: v.Equipamento is not null ? v.Problema : null,
            Linhas: linhas,
            TotalCents: v.TotalCents,
            Observacoes: v.Notas);

        var pdf = OrcamentoPdfRenderer.Render(data);
        return (pdf, $"Orcamento_V-{v.Numero:D5}.pdf");
    }

    private async Task<OrcamentoEmissor> BuildEmissorAsync(CancellationToken ct)
    {
        var tenant = _tenantContext.TenantId is not null
            ? await _tenants.FindByIdAsync(_tenantContext.TenantId.Value, ct)
            : null;
        return new OrcamentoEmissor(
            Nome: tenant?.LegalName ?? tenant?.Name ?? "LopesTech",
            Nif: tenant?.Nif,
            Morada: tenant?.Address,
            CodigoPostal: tenant?.PostalCode,
            Localidade: tenant?.Locality,
            Telefone: tenant?.Phone,
            Email: tenant?.Email,
            Website: tenant?.Website,
            Iban: tenant?.Iban,
            CaePrincipal: tenant?.CaePrincipal,
            CaeSecundarios: tenant?.CaeSecundarios,
            LogoUrl: tenant?.LogoUrl,
            PrimaryColor: tenant?.PrimaryColor,
            TermosCondicoes: tenant?.TermosCondicoes);
    }
}
