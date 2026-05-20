using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.EquipmentFields;

namespace RepairDesk.Services.Documents;

public interface IOrcamentoPdfService
{
    Task<(byte[] Pdf, string Filename)> ForReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<(byte[] Pdf, string Filename)> ForTrabalhoAsync(Guid trabalhoId, CancellationToken ct = default);
}

public class OrcamentoPdfService : IOrcamentoPdfService
{
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITrabalhoRepository _trabalhos;
    private readonly IClienteRepository _clientes;
    private readonly IDespesaRepository _despesas;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IEquipmentFieldService _equipmentFields;
    private readonly IConfiguration _config;

    public OrcamentoPdfService(
        IReparacaoRepository reparacoes,
        ITrabalhoRepository trabalhos,
        IClienteRepository clientes,
        IDespesaRepository despesas,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IEquipmentFieldService equipmentFields,
        IConfiguration config)
    {
        _reparacoes = reparacoes;
        _trabalhos = trabalhos;
        _clientes = clientes;
        _despesas = despesas;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _equipmentFields = equipmentFields;
        _config = config;
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

        // Linhas a partir das despesas linked (peças) — se houver
        var totalDespesas = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        var linhas = new List<OrcamentoLinha>();
        // Mão-de-obra calculada como (preço − peças)
        var precoTotal = rep.PrecoFinalCents ?? rep.OrcamentoCents ?? 0;
        if (totalDespesas > 0)
        {
            linhas.Add(new OrcamentoLinha("Peças e material", totalDespesas));
            var maoDeObra = Math.Max(0, precoTotal - totalDespesas);
            if (maoDeObra > 0) linhas.Add(new OrcamentoLinha("Mão-de-obra", maoDeObra));
        }
        else if (precoTotal > 0)
        {
            // Sprint 112: garante 1 linha descritiva mesmo sem peças imputadas
            // (em vez de "orçamento global sem detalhe").
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

    public async Task<(byte[] Pdf, string Filename)> ForTrabalhoAsync(Guid trabalhoId, CancellationToken ct = default)
    {
        var t = await _trabalhos.FindByIdAsync(trabalhoId, ct)
            ?? throw new NotFoundException("Trabalho", trabalhoId);
        Cliente? cliente = null;
        if (t.ClienteId is not null)
            cliente = await _clientes.FindByIdAsync(t.ClienteId.Value, ct);

        var emissor = await BuildEmissorAsync(ct);
        var totalDespesas = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        var precoTotal = t.PrecoFinalCents ?? t.OrcamentoCents ?? 0;
        var linhas = new List<OrcamentoLinha>();
        if (totalDespesas > 0)
        {
            linhas.Add(new OrcamentoLinha("Material e despesas", totalDespesas));
            var maoDeObra = Math.Max(0, precoTotal - totalDespesas);
            if (maoDeObra > 0) linhas.Add(new OrcamentoLinha("Serviço / mão-de-obra", maoDeObra));
        }

        var data = new OrcamentoData(
            Numero: $"T-{t.Numero:D5}",
            Tipo: "Trabalho",
            Data: DateTime.UtcNow,
            ValidoAte: DateTime.UtcNow.AddDays(30),
            Emissor: emissor,
            Cliente: cliente is not null
                ? new OrcamentoCliente(cliente.Nome, cliente.Telefone, cliente.Email, cliente.Nif)
                : new OrcamentoCliente("(cliente a definir)", null, null, null),
            Titulo: t.Titulo,
            Descricao: t.Descricao,
            Linhas: linhas,
            TotalCents: precoTotal,
            Observacoes: t.Notas);

        var pdf = OrcamentoPdfRenderer.Render(data);
        return (pdf, $"Orcamento_T-{t.Numero:D5}.pdf");
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
