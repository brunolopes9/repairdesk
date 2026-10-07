using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.EquipmentFields;

namespace RepairDesk.Services.Documents;

public interface IOrcamentoPdfService
{
    Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    /// <summary>Doc 94 Fase 4: orçamento de uma Venda (reparação, serviço ou produto) para enviar ao cliente.</summary>
    Task<(byte[] Pdf, string Filename)> ForVendaAsync(Guid vendaId, CancellationToken ct = default);
}

public class OrcamentoPdfService : IOrcamentoPdfService
{
    private readonly IReparacaoRepository _reparacoes;
    private readonly IVendaRepository _vendas;
    private readonly IClienteRepository _clientes;
    private readonly IDespesaRepository _despesas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IEquipmentFieldService _equipmentFields;
    private readonly IConfiguration _config;
    private readonly IPartRepository _parts;

    public OrcamentoPdfService(
        IReparacaoRepository reparacoes,
        IVendaRepository vendas,
        IClienteRepository clientes,
        IDespesaRepository despesas,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IEquipmentFieldService equipmentFields,
        IConfiguration config,
        IPartRepository parts)
    {
        _reparacoes = reparacoes;
        _vendas = vendas;
        _clientes = clientes;
        _despesas = despesas;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _equipmentFields = equipmentFields;
        _config = config;
        _parts = parts;
    }

    private string? BuildPortalUrl(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var baseUrl = _config["Frontend:PortalBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        return $"{baseUrl}/r/{slug}";
    }

    public async Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);
        var cliente = await _clientes.FindByIdAsync(rep.ClienteId, ct)
            ?? throw new NotFoundException("Cliente", rep.ClienteId);
        var emissor = await BuildEmissorAsync(ct);
        var camposEquipamento = await _equipmentFields.GetValuesAsync(rep.Id, visibleInPortalOnly: true, ct);

        // Sprint 137: linhas a partir de PartMovimentos (peças do stock) + Despesas (compras
        // específicas ao fornecedor) + mão-de-obra residual. Bruno tipicamente usa Stock
        // (Sprint 134/135 flow) e ignora Despesas — antes, o PDF mostrava 1 linha sintética.
        var totalDespesas = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        var movimentos = await _parts.MovimentosAsync(partId: null, reparacaoId: rep.Id, ct);

        var linhas = new List<OrcamentoLinha>();
        var pecasSubtotal = 0;
        var precoTotal = rep.PrecoFinalCents ?? rep.OrcamentoCents ?? 0;

        // 1) Peças do stock: 1 linha por Part (líquido das devoluções)
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

        // 2) Despesas (compras específicas) — agregadas; granularidade fina pode vir num sprint futuro
        if (totalDespesas > 0)
        {
            linhas.Add(new OrcamentoLinha(linhas.Count == 0 ? "Peças e material" : "Material adicional", totalDespesas));
            pecasSubtotal += totalDespesas;
        }

        // 3) Mão-de-obra residual (positiva apenas)
        if (linhas.Count > 0)
        {
            var maoDeObra = precoTotal - pecasSubtotal;
            if (maoDeObra > 0)
                linhas.Add(new OrcamentoLinha($"Mão-de-obra · {rep.Equipamento}".Trim(), maoDeObra));
            // Se mão-de-obra negativa (peças > preço), não mostra linha negativa — apenas as peças
            // ficam visíveis e o total no rodapé. Bruno deve rever o preço final.
        }
        else if (precoTotal > 0)
        {
            // Sem peças nem despesas: fallback à linha sintética antiga (Sprint 112).
            var desc = string.IsNullOrWhiteSpace(rep.Avaria)
                ? $"Reparação {rep.Equipamento}".Trim()
                : $"Reparação {rep.Equipamento} — {rep.Avaria}".Trim();
            linhas.Add(new OrcamentoLinha(desc, precoTotal));
        }

        var data = new OrcamentoData(
            Numero: $"R-{rep.Numero:D5}",
            Tipo: "Reparação",
            Data: DateTime.UtcNow,
            ValidoAte: DateTime.UtcNow.AddDays(15),
            Emissor: emissor,
            Cliente: new OrcamentoCliente(cliente.Nome, cliente.Telefone, cliente.Email, cliente.Nif),
            Titulo: rep.Equipamento,
            Descricao: rep.Avaria + (string.IsNullOrWhiteSpace(rep.Diagnostico) ? "" : $"\n\nDiagnóstico: {rep.Diagnostico}"),
            Linhas: linhas,
            TotalCents: precoTotal,
            Observacoes: rep.Notas,
            CamposEquipamento: camposEquipamento
                .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => new OrcamentoCampoEquipamento(c.Label, c.Value!))
                .ToList(),
            PortalUrl: BuildPortalUrl(rep.PublicSlug));

        var pdf = OrcamentoPdfRenderer.Render(data);
        return (pdf, $"Orcamento_R-{rep.Numero:D5}.pdf");
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
