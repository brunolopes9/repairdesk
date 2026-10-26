using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Services.Moloni;

/// <summary>Referências do Moloni ON guardadas por tenant (JSON em <c>Tenant.MoloniConfigJson</c>).</summary>
public sealed class MoloniConfig
{
    public int SerieServico { get; set; }
    public int SerieReparacao { get; set; }
    public int SerieProduto { get; set; }
    public int ClienteConsumidorFinalId { get; set; }
    public int UnidadeId { get; set; }
    public int CategoriaId { get; set; }
    public int? ProdutoServicoId { get; set; }
    public int? ProdutoArtigoId { get; set; }
    /// <summary>Taxa de IVA em % (ex.: "23") → taxId.</summary>
    public Dictionary<string, int> Impostos { get; set; } = new();
    /// <summary>Nome do método de pagamento (minúsculas) → paymentMethodId.</summary>
    public Dictionary<string, int> MetodosPagamento { get; set; } = new();
}

public sealed record MoloniSerieDto(int Id, string Nome, bool Predefinida);

public sealed record MoloniEstadoDto(
    bool Configurado,
    int? CompanyId,
    IReadOnlyList<MoloniSerieDto> Series,
    int? SerieServico,
    int? SerieReparacao,
    int? SerieProduto);

public sealed record MoloniConfigurarRequest(string? ApiKey, int? SerieServico = null, int? SerieReparacao = null, int? SerieProduto = null);

public enum MoloniDocumento
{
    /// <summary>Fatura: fica por pagar até se registar o recibo.</summary>
    Fatura,
    /// <summary>Fatura-recibo: paga na hora.</summary>
    FaturaRecibo,
}

public sealed record MoloniEmitirRequest(
    MoloniDocumento Tipo,
    int? SerieId = null,
    PaymentMethod? PaymentMethod = null,
    int VencimentoDias = 0,
    /// <summary>Obrigatório quando há linhas a 0% (ex.: "M40" autoliquidação).</summary>
    string? MotivoIsencao = null,
    string? Notas = null);

public sealed record MoloniReciboRequest(PaymentMethod? PaymentMethod = null);

public sealed record MoloniAnularRequest(string Motivo);

public sealed record MoloniOrcamentoRequest(int? SerieId = null, int ValidadeDias = 30, string? MotivoIsencao = null);

public interface IMoloniService
{
    Task<bool> ConfiguradoAsync(CancellationToken ct = default);
    Task<MoloniEstadoDto> EstadoAsync(CancellationToken ct = default);
    Task<MoloniEstadoDto> ConfigurarAsync(MoloniConfigurarRequest req, CancellationToken ct = default);
    Task RemoverAsync(CancellationToken ct = default);
    Task<VendaDto> EmitirAsync(Guid vendaId, MoloniEmitirRequest req, CancellationToken ct = default);
    Task<VendaDto> ReciboAsync(Guid vendaId, MoloniReciboRequest req, CancellationToken ct = default);
    Task<VendaDto> AnularAsync(Guid vendaId, MoloniAnularRequest req, CancellationToken ct = default);
    Task<VendaDto> OrcamentoAsync(Guid vendaId, MoloniOrcamentoRequest req, CancellationToken ct = default);
    /// <summary>PDF de um documento da venda, pedido ao Moloni: "fatura", "recibo" ou "orcamento".</summary>
    Task<(byte[] Pdf, string Nome)> PdfAsync(Guid vendaId, string documento, CancellationToken ct = default);
    /// <summary>Cria/atualiza o cliente no Moloni ON (sem efeito se a ligação não estiver configurada).</summary>
    Task SincronizarClienteAsync(Guid clienteId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 567: o Mender emite os documentos fiscais no Moloni ON (fatura, fatura-recibo, recibo, orçamento,
/// anulação). Regras de segurança:
/// <list type="bullet">
/// <item>Cada documento nasce em rascunho; só é finalizado (e comunicado à AT) se o total do Moloni bater com o da venda.
/// Se não bater, o rascunho é apagado e nada fica emitido.</item>
/// <item>Linhas a 0% exigem motivo de isenção explícito — nunca se assume.</item>
/// <item>Anular segue a ordem que o Moloni exige: primeiro o recibo, depois a fatura.</item>
/// <item>Consumidor final = cliente predefinido do Moloni (nunca se cria).</item>
/// </list>
/// </summary>
public class MoloniService : IMoloniService
{
    private const decimal ToleranciaTotal = 0.02m;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMoloniClient _client;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secrets;
    private readonly IVendaRepository _vendas;
    private readonly IVendaService _vendaService;
    private readonly IVendaFaturaService _faturas;
    private readonly IClienteRepository _clientes;
    private readonly IArtigoRepository _artigos;
    private readonly IAuditLogger _audit;
    private readonly ILogger<MoloniService> _logger;

    public MoloniService(IMoloniClient client, ITenantRepository tenants, ITenantContext tenant, ISecretProtector secrets,
        IVendaRepository vendas, IVendaService vendaService, IVendaFaturaService faturas, IClienteRepository clientes,
        IArtigoRepository artigos, IAuditLogger audit, ILogger<MoloniService> logger)
    {
        _client = client;
        _tenants = tenants;
        _tenant = tenant;
        _secrets = secrets;
        _vendas = vendas;
        _vendaService = vendaService;
        _faturas = faturas;
        _clientes = clientes;
        _artigos = artigos;
        _audit = audit;
        _logger = logger;
    }

    // ---------- configuração ----------

    public async Task<bool> ConfiguradoAsync(CancellationToken ct = default)
        => await TenantAsync(ct) is { MoloniApiKeyCipherText: not null, MoloniCompanyId: not null, MoloniConfigJson: not null };

    public async Task<MoloniEstadoDto> EstadoAsync(CancellationToken ct = default)
    {
        var t = await TenantAsync(ct);
        if (t?.MoloniApiKeyCipherText is null || t.MoloniCompanyId is not { } companyId || t.MoloniConfigJson is null)
            return new MoloniEstadoDto(false, null, [], null, null, null);
        var cfg = Config(t);
        var series = await SeriesAsync(_secrets.Unprotect(t.MoloniApiKeyCipherText), companyId, ct);
        return new MoloniEstadoDto(true, companyId, series, cfg.SerieServico, cfg.SerieReparacao, cfg.SerieProduto);
    }

    public async Task<MoloniEstadoDto> ConfigurarAsync(MoloniConfigurarRequest req, CancellationToken ct = default)
    {
        var t = await TenantAsync(ct) ?? throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var key = req.ApiKey?.Trim();
        if (string.IsNullOrEmpty(key))
            key = t.MoloniApiKeyCipherText is { } c ? _secrets.Unprotect(c)
                : throw new ValidationException("chave_obrigatoria", "Cola a chave da API do Moloni ON (começa por \"apik:\").");
        if (!key.StartsWith("apik:", StringComparison.Ordinal))
            throw new ValidationException("chave_invalida", "A chave da API do Moloni ON começa por \"apik:\".");

        var me = await _client.ExecutarAsync(key, "{ me { errors { field msg } data { userCompanies { companyId } } } }", null, "me", ct);
        var empresas = me?["userCompanies"]?.AsArray().Select(c => c!["companyId"]!.GetValue<int>()).ToList() ?? [];
        if (empresas.Count == 0) throw new ValidationException("sem_empresa", "Esta chave não tem acesso a nenhuma empresa no Moloni ON.");
        var companyId = t.MoloniCompanyId is { } atual && empresas.Contains(atual) ? atual : empresas[0];

        var series = await SeriesAsync(key, companyId, ct); // também confirma que o módulo de API está ativo
        var cfg = t.MoloniConfigJson is null ? new MoloniConfig() : Config(t);
        int Serie(int? pedido, int atual, Func<MoloniSerieDto?> omissao)
        {
            if (pedido is { } p)
                return series.Any(s => s.Id == p) ? p : throw new ValidationException("serie_invalida", "Essa série não existe no Moloni ON.");
            return series.Any(s => s.Id == atual) ? atual : omissao()?.Id ?? series[0].Id;
        }
        MoloniSerieDto? PorNome(string nome) => series.FirstOrDefault(s => s.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));
        var predefinida = series.FirstOrDefault(s => s.Predefinida) ?? series[0];
        cfg.SerieServico = Serie(req.SerieServico, cfg.SerieServico, () => predefinida);
        cfg.SerieReparacao = Serie(req.SerieReparacao, cfg.SerieReparacao, () => PorNome("REPARACAO") ?? predefinida);
        cfg.SerieProduto = Serie(req.SerieProduto, cfg.SerieProduto, () => PorNome("REPARACAO") ?? predefinida);

        var impostos = await _client.ExecutarAsync(key, "query($c:Int!){ taxes(companyId:$c) { errors { field msg } data { taxId value type } } }", new { c = companyId }, "taxes", ct);
        cfg.Impostos = impostos!.AsArray().Where(x => x!["type"]?.GetValue<int>() == 1)
            .GroupBy(x => ((int)x!["value"]!.GetValue<decimal>()).ToString(CultureInfo.InvariantCulture))
            .ToDictionary(g => g.Key, g => g.First()!["taxId"]!.GetValue<int>());
        var metodos = await _client.ExecutarAsync(key, "query($c:Int!){ paymentMethods(companyId:$c) { errors { field msg } data { paymentMethodId name } } }", new { c = companyId }, "paymentMethods", ct);
        cfg.MetodosPagamento = metodos!.AsArray().GroupBy(x => Normalizar(x!["name"]!.GetValue<string>()))
            .ToDictionary(g => g.Key, g => g.First()!["paymentMethodId"]!.GetValue<int>());
        var unidades = await _client.ExecutarAsync(key, "query($c:Int!){ measurementUnits(companyId:$c) { errors { field msg } data { measurementUnitId abbreviation } } }", new { c = companyId }, "measurementUnits", ct);
        cfg.UnidadeId = unidades!.AsArray().FirstOrDefault(u => u!["abbreviation"]?.GetValue<string>() == "un")?["measurementUnitId"]?.GetValue<int>()
            ?? unidades!.AsArray()[0]!["measurementUnitId"]!.GetValue<int>();
        var categorias = await _client.ExecutarAsync(key, "query($c:Int!){ productCategories(companyId:$c) { errors { field msg } data { productCategoryId } } }", new { c = companyId }, "productCategories", ct);
        cfg.CategoriaId = categorias!.AsArray()[0]!["productCategoryId"]!.GetValue<int>();
        var consumidor = await _client.ExecutarAsync(key,
            "query($c:Int!){ customers(companyId:$c, options:{ filter:[{field:isDefault, comparison:eq, value:\"1\"}] }) { errors { field msg } data { customerId } } }",
            new { c = companyId }, "customers", ct);
        cfg.ClienteConsumidorFinalId = consumidor!.AsArray().FirstOrDefault()?["customerId"]?.GetValue<int>()
            ?? throw new MoloniException("não encontrei o cliente \"Consumidor final\" (predefinido).");

        t.MoloniApiKeyCipherText = _secrets.Protect(key);
        t.MoloniCompanyId = companyId;
        t.MoloniConfigJson = JsonSerializer.Serialize(cfg, Json);
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Tenant), t.Id, new { operation = "moloni_configurar", companyId }, ct: ct);
        return new MoloniEstadoDto(true, companyId, series, cfg.SerieServico, cfg.SerieReparacao, cfg.SerieProduto);
    }

    public async Task RemoverAsync(CancellationToken ct = default)
    {
        var t = await TenantAsync(ct) ?? throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        t.MoloniApiKeyCipherText = null;
        t.MoloniCompanyId = null;
        t.MoloniConfigJson = null;
        await _tenants.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Tenant), t.Id, new { operation = "moloni_remover" }, ct: ct);
    }

    // ---------- documentos ----------

    public async Task<VendaDto> EmitirAsync(Guid vendaId, MoloniEmitirRequest req, CancellationToken ct = default)
    {
        var (t, key, companyId, cfg) = await ContextoAsync(ct);
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (venda.Estado != VendaEstado.Entregue)
            throw new ValidationException("venda_nao_entregue", "Marca a venda como entregue antes de emitir a fatura (ou emite um orçamento).");
        if (venda.InvoiceNumber is not null || venda.MoloniDocumentoId is not null)
            throw new ConflictException("ja_tem_fatura", $"Esta venda já tem a fatura {venda.InvoiceNumber}. Anula-a primeiro se for para emitir outra.");
        if (venda.Items.Count == 0) throw new ValidationException("venda_sem_linhas", "A venda não tem linhas.");
        if (req.VencimentoDias is < 0 or > 365) throw new ValidationException("vencimento_invalido", "Prazo de vencimento entre 0 e 365 dias.");

        var serie = req.SerieId ?? SeriePorTipo(cfg, venda.Tipo);
        var customerId = await ClienteMoloniAsync(key, companyId, cfg, venda.Cliente, ct);
        var produtos = await LinhasAsync(key, companyId, cfg, t, venda, req.MotivoIsencao, ct);
        var agora = DateTime.UtcNow;
        var totalVenda = venda.TotalCents / 100m;
        var fatura = req.Tipo == MoloniDocumento.Fatura;
        var apiCode = fatura ? "invoice" : "invoiceReceipt";
        var metodo = fatura ? (int?)null : await MetodoPagamentoAsync(key, companyId, cfg, t, req.PaymentMethod ?? venda.PaymentMethod, ct);

        var dados = new Dictionary<string, object?>
        {
            ["documentSetId"] = serie,
            ["customerId"] = customerId,
            ["date"] = agora.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            ["expirationDate"] = agora.AddDays(fatura ? req.VencimentoDias : 0).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["status"] = 0,
            ["notes"] = Limpar(req.Notas, 1000),
            ["ourReference"] = $"Mender venda #{venda.Numero}",
            ["products"] = produtos,
        };
        if (!fatura) dados["payments"] = new[] { Pagamento(metodo!.Value, totalVenda, agora) };

        var (documentId, totalMoloni) = await CriarRascunhoAsync(key, companyId, apiCode, fatura ? "InvoiceInsert" : "InvoiceReceiptInsert", dados, ct);
        await GarantirTotalAsync(key, companyId, apiCode, documentId, totalMoloni, totalVenda, ct);

        var final = new Dictionary<string, object?> { ["documentId"] = documentId, ["status"] = 1 };
        if (!fatura) final["payments"] = new[] { Pagamento(metodo!.Value, totalMoloni, agora) };
        var (numero, serieNome) = await FinalizarAsync(key, companyId, apiCode, fatura ? "InvoiceUpdate" : "InvoiceReceiptUpdate", final, ct);

        venda.MoloniDocumentoTipo = apiCode;
        venda.MoloniDocumentoId = documentId;
        if (!fatura && req.PaymentMethod is { } pm) venda.PaymentMethod = pm;
        await _vendas.SaveAsync(ct);
        var invoiceNumber = $"{(fatura ? "FT" : "FR")} {serieNome}/{numero}";
        await _vendaService.RegistarFaturaAsync(venda.Id, new RegistarFaturaRequest(invoiceNumber, agora), ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Venda), venda.Id,
            new { operation = "moloni_emitir", documento = apiCode, invoiceNumber, documentId, totalMoloni }, ct: ct);

        await GuardarPdfAsync(key, companyId, apiCode, documentId, venda.Id, ct);
        return await _vendaService.GetAsync(venda.Id, ct);
    }

    public async Task<VendaDto> ReciboAsync(Guid vendaId, MoloniReciboRequest req, CancellationToken ct = default)
    {
        var (t, key, companyId, cfg) = await ContextoAsync(ct);
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (venda.MoloniDocumentoTipo != "invoice" || venda.MoloniDocumentoId is not { } faturaId)
            throw new ValidationException("sem_fatura_por_pagar", "Esta venda não tem uma fatura (por pagar) emitida pelo Mender.");
        if (venda.MoloniReciboId is not null)
            throw new ConflictException("ja_paga", $"Esta fatura já tem o recibo {venda.MoloniReciboNumero}.");

        var inv = await _client.ExecutarAsync(key,
            "query($c:Int!,$d:Int!){ invoice(companyId:$c, documentId:$d) { errors { field msg } data { totalValue documentSetId customerId remainingReconciledValue nullified } } }",
            new { c = companyId, d = faturaId }, "invoice", ct);
        if (inv?["nullified"]?.GetValue<bool>() == true) throw new ConflictException("fatura_anulada", "A fatura está anulada no Moloni ON.");
        var valor = inv?["remainingReconciledValue"] is JsonValue rv && rv.TryGetValue<decimal>(out var resto) && resto > 0 ? resto : inv!["totalValue"]!.GetValue<decimal>();
        var metodo = await MetodoPagamentoAsync(key, companyId, cfg, t, req.PaymentMethod ?? venda.PaymentMethod, ct);
        var agora = DateTime.UtcNow;

        var recibo = await _client.ExecutarAsync(key,
            "mutation($c:Int!,$d:ReceiptInsert!){ receiptCreate(companyId:$c, data:$d) { errors { field msg } data { documentId number documentSetName totalValue } } }",
            new
            {
                c = companyId,
                d = new
                {
                    documentSetId = inv!["documentSetId"]!.GetValue<int>(),
                    customerId = inv["customerId"]!.GetValue<int>(),
                    date = agora.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                    totalValue = valor,
                    relatedWith = new[] { new { relatedDocumentId = faturaId, value = valor } },
                    payments = new[] { Pagamento(metodo, valor, agora) },
                    status = 1,
                },
            }, "receiptCreate", ct);

        venda.MoloniReciboId = recibo!["documentId"]!.GetValue<int>();
        venda.MoloniReciboNumero = $"RC {recibo["documentSetName"]!.GetValue<string>()}/{recibo["number"]!.GetValue<int>()}";
        if (req.PaymentMethod is { } pm) venda.PaymentMethod = pm;
        await _vendas.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Venda), venda.Id,
            new { operation = "moloni_recibo", venda.MoloniReciboNumero, valor }, ct: ct);
        return await _vendaService.GetAsync(venda.Id, ct);
    }

    public async Task<VendaDto> AnularAsync(Guid vendaId, MoloniAnularRequest req, CancellationToken ct = default)
    {
        var (_, key, companyId, _) = await ContextoAsync(ct);
        var motivo = Limpar(req.Motivo, 200);
        if (motivo is null || motivo.Length < 5)
            throw new ValidationException("motivo_obrigatorio", "Escreve o motivo da anulação (fica registado na AT).");
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (venda.MoloniDocumentoId is not { } documentId || venda.MoloniDocumentoTipo is not { } apiCode)
            throw new ValidationException("sem_documento_moloni", "Esta venda não tem documento emitido pelo Mender no Moloni ON.");

        var anulados = new List<string>();
        // O Moloni não anula uma fatura usada num recibo — o recibo vai primeiro.
        if (venda.MoloniReciboId is { } reciboId)
        {
            await NullifyAsync(key, companyId, "receipt", reciboId, motivo, ct);
            anulados.Add(venda.MoloniReciboNumero ?? "recibo");
            venda.MoloniReciboId = null;
            venda.MoloniReciboNumero = null;
            await _vendas.SaveAsync(ct);
        }
        await NullifyAsync(key, companyId, apiCode, documentId, motivo, ct);
        anulados.Add(venda.InvoiceNumber ?? apiCode);

        venda.MoloniDocumentoId = null;
        venda.MoloniDocumentoTipo = null;
        venda.FaturaPdfPath = null;
        venda.FaturaPdfSha256 = null;
        await _vendas.SaveAsync(ct);
        await _vendaService.RegistarFaturaAsync(venda.Id, new RegistarFaturaRequest(null, null), ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Venda), venda.Id, new { operation = "moloni_anular", anulados, motivo }, ct: ct);
        return await _vendaService.GetAsync(venda.Id, ct);
    }

    public async Task<VendaDto> OrcamentoAsync(Guid vendaId, MoloniOrcamentoRequest req, CancellationToken ct = default)
    {
        var (t, key, companyId, cfg) = await ContextoAsync(ct);
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (venda.Estado == VendaEstado.Cancelada) throw new ValidationException("venda_cancelada", "A venda está cancelada.");
        if (venda.Items.Count == 0) throw new ValidationException("venda_sem_linhas", "A venda não tem linhas.");
        if (req.ValidadeDias is < 1 or > 365) throw new ValidationException("validade_invalida", "Validade do orçamento entre 1 e 365 dias.");

        var customerId = await ClienteMoloniAsync(key, companyId, cfg, venda.Cliente, ct);
        var produtos = await LinhasAsync(key, companyId, cfg, t, venda, req.MotivoIsencao, ct);
        var agora = DateTime.UtcNow;
        var dados = new Dictionary<string, object?>
        {
            ["documentSetId"] = req.SerieId ?? SeriePorTipo(cfg, venda.Tipo),
            ["customerId"] = customerId,
            ["date"] = agora.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            ["expirationDate"] = agora.AddDays(req.ValidadeDias).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["status"] = 0,
            ["ourReference"] = $"Mender venda #{venda.Numero}",
            ["products"] = produtos,
        };
        var (documentId, totalMoloni) = await CriarRascunhoAsync(key, companyId, "estimate", "EstimateInsert", dados, ct);
        await GarantirTotalAsync(key, companyId, "estimate", documentId, totalMoloni, venda.TotalCents / 100m, ct);
        var (numero, serieNome) = await FinalizarAsync(key, companyId, "estimate", "EstimateUpdate",
            new Dictionary<string, object?> { ["documentId"] = documentId, ["status"] = 1 }, ct);

        venda.MoloniOrcamentoId = documentId;
        venda.MoloniOrcamentoNumero = $"OR {serieNome}/{numero}";
        await _vendas.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Venda), venda.Id, new { operation = "moloni_orcamento", venda.MoloniOrcamentoNumero }, ct: ct);
        return await _vendaService.GetAsync(venda.Id, ct);
    }

    public async Task<(byte[] Pdf, string Nome)> PdfAsync(Guid vendaId, string documento, CancellationToken ct = default)
    {
        var (_, key, companyId, _) = await ContextoAsync(ct);
        var venda = await _vendas.FindByIdAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        var (apiCode, id, nome) = documento switch
        {
            "fatura" => (venda.MoloniDocumentoTipo, venda.MoloniDocumentoId, venda.InvoiceNumber),
            "recibo" => ("receipt", venda.MoloniReciboId, venda.MoloniReciboNumero),
            "orcamento" => ("estimate", venda.MoloniOrcamentoId, venda.MoloniOrcamentoNumero),
            _ => throw new ValidationException("documento_invalido", "Documento: fatura, recibo ou orcamento."),
        };
        if (apiCode is null || id is not { } documentId) throw new NotFoundException("DocumentoMoloni", vendaId);
        var pdf = await ObterPdfAsync(key, companyId, apiCode, documentId, ct);
        return (pdf, (nome ?? documento).Replace('/', '-') + ".pdf");
    }

    public async Task SincronizarClienteAsync(Guid clienteId, CancellationToken ct = default)
    {
        var t = await TenantAsync(ct);
        if (t?.MoloniApiKeyCipherText is null || t.MoloniCompanyId is not { } companyId || t.MoloniConfigJson is null) return;
        var cliente = await _clientes.FindByIdAsync(clienteId, ct);
        if (cliente is null) return;
        var key = _secrets.Unprotect(t.MoloniApiKeyCipherText);
        var cfg = Config(t);
        if (cliente.MoloniCustomerId is { } id)
        {
            var dados = DadosCliente(cliente);
            dados["customerId"] = id;
            await _client.ExecutarAsync(key, "mutation($c:Int!,$d:CustomerUpdate!){ customerUpdate(companyId:$c, data:$d) { errors { field msg } data { customerId } } }",
                new { c = companyId, d = dados }, "customerUpdate", ct);
        }
        else
        {
            await ClienteMoloniAsync(key, companyId, cfg, cliente, ct);
            await _clientes.SaveAsync(ct);
        }
    }

    // ---------- peças ----------

    private async Task<Tenant?> TenantAsync(CancellationToken ct)
        => _tenant.TenantId is { } id ? await _tenants.FindByIdAsync(id, ct) : null;

    private async Task<(Tenant T, string Key, int CompanyId, MoloniConfig Cfg)> ContextoAsync(CancellationToken ct)
    {
        var t = await TenantAsync(ct);
        if (t?.MoloniApiKeyCipherText is null || t.MoloniCompanyId is not { } companyId || t.MoloniConfigJson is null)
            throw new ValidationException("moloni_nao_configurado", "Liga primeiro o Moloni ON em Definições → Moloni ON.");
        return (t, _secrets.Unprotect(t.MoloniApiKeyCipherText), companyId, Config(t));
    }

    private static MoloniConfig Config(Tenant t) => JsonSerializer.Deserialize<MoloniConfig>(t.MoloniConfigJson!, Json) ?? new MoloniConfig();

    private async Task GuardarConfigAsync(Tenant t, MoloniConfig cfg, CancellationToken ct)
    {
        t.MoloniConfigJson = JsonSerializer.Serialize(cfg, Json);
        await _tenants.SaveAsync(ct);
    }

    private async Task<IReadOnlyList<MoloniSerieDto>> SeriesAsync(string key, int companyId, CancellationToken ct)
    {
        var d = await _client.ExecutarAsync(key,
            "query($c:Int!){ documentSetsForDocument(companyId:$c, documentTypeId:1) { errors { field msg } data { documentSetId name isDefault } } }",
            new { c = companyId }, "documentSetsForDocument", ct);
        var series = d?.AsArray().Select(s => new MoloniSerieDto(s!["documentSetId"]!.GetValue<int>(), s["name"]!.GetValue<string>(),
            s["isDefault"]?.GetValue<bool>() ?? false)).ToList() ?? [];
        if (series.Count == 0) throw new MoloniException("não há séries de faturas configuradas.");
        return series;
    }

    private static int SeriePorTipo(MoloniConfig cfg, VendaTipo tipo) => tipo switch
    {
        VendaTipo.Servico => cfg.SerieServico,
        VendaTipo.Reparacao => cfg.SerieReparacao,
        _ => cfg.SerieProduto,
    };

    /// <summary>Consumidor final → cliente predefinido; senão o ligado, o do mesmo NIF, ou um novo.</summary>
    private async Task<int> ClienteMoloniAsync(string key, int companyId, MoloniConfig cfg, Cliente? cliente, CancellationToken ct)
    {
        if (cliente is null) return cfg.ClienteConsumidorFinalId;
        if (cliente.MoloniCustomerId is { } ligado) return ligado;

        var nif = NifLimpo(cliente.Nif);
        if (nif is not null)
        {
            var encontrados = await _client.ExecutarAsync(key,
                "query($c:Int!,$v:String!){ customers(companyId:$c, options:{ filter:[{field:vat, comparison:eq, value:$v}] }) { errors { field msg } data { customerId } } }",
                new { c = companyId, v = nif.Value.Vat }, "customers", ct);
            if (encontrados?.AsArray().FirstOrDefault()?["customerId"]?.GetValue<int>() is { } existente)
            {
                cliente.MoloniCustomerId = existente;
                return existente;
            }
        }

        var numero = (await _client.ExecutarAsync(key, "query($c:Int!){ customerNextNumber(companyId:$c) { errors { field msg } data } }",
            new { c = companyId }, "customerNextNumber", ct))?.ToString() ?? throw new MoloniException("não consegui o próximo nº de cliente.");
        var dados = DadosCliente(cliente);
        dados["number"] = numero;
        dados["countryId"] = nif is { Pais: not "PT" } ? await PaisAsync(key, nif.Value.Pais, ct) : 1;
        dados["languageId"] = 1;
        var criado = await _client.ExecutarAsync(key,
            "mutation($c:Int!,$d:CustomerInsert!){ customerCreate(companyId:$c, data:$d) { errors { field msg } data { customerId } } }",
            new { c = companyId, d = dados }, "customerCreate", ct);
        var novoId = criado!["customerId"]!.GetValue<int>();
        cliente.MoloniCustomerId = novoId;
        return novoId;
    }

    private static Dictionary<string, object?> DadosCliente(Cliente c)
    {
        var d = new Dictionary<string, object?> { ["name"] = c.Nome };
        if (NifLimpo(c.Nif) is { } nif) d["vat"] = nif.Vat;
        if (!string.IsNullOrWhiteSpace(c.Morada)) d["address"] = c.Morada;
        if (!string.IsNullOrWhiteSpace(c.Localidade)) d["city"] = c.Localidade;
        if (c.CodigoPostal is { } cp && Regex.IsMatch(cp.Trim(), @"^\d{4}-\d{3}$")) d["zipCode"] = cp.Trim();
        if (!string.IsNullOrWhiteSpace(c.Email)) d["email"] = c.Email;
        if (!string.IsNullOrWhiteSpace(c.Telefone)) d["phone"] = c.Telefone;
        return d;
    }

    /// <summary>"PT263758141" / "263758141" → (PT, 263758141); "CZ699005540" → (CZ, CZ699005540) — como o Moloni guarda.</summary>
    private static (string Pais, string Vat)? NifLimpo(string? nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return null;
        var n = Regex.Replace(nif.ToUpperInvariant(), @"[\s.-]", string.Empty);
        if (Regex.IsMatch(n, @"^PT\d{9}$")) return ("PT", n[2..]);
        if (Regex.IsMatch(n, @"^\d{9}$")) return ("PT", n);
        if (Regex.IsMatch(n, @"^[A-Z]{2}[A-Z0-9]+$")) return (n[..2], n);
        return ("XX", n);
    }

    private async Task<int> PaisAsync(string key, string iso, CancellationToken ct)
    {
        var paises = await _client.ExecutarAsync(key, "{ countries { errors { field msg } data { countryId iso3166_1 } } }", null, "countries", ct);
        return paises?.AsArray().FirstOrDefault(p => string.Equals(p!["iso3166_1"]?.GetValue<string>(), iso, StringComparison.OrdinalIgnoreCase))?["countryId"]?.GetValue<int>()
            ?? throw new ValidationException("pais_desconhecido", $"Não sei o país do NIF (prefixo {iso}). Corrige o NIF do cliente.");
    }

    /// <summary>Linhas da venda → produtos Moloni (preço SEM IVA, IVA da linha ou motivo de isenção).</summary>
    private async Task<List<Dictionary<string, object?>>> LinhasAsync(string key, int companyId, MoloniConfig cfg, Tenant t, Venda venda,
        string? motivoIsencao, CancellationToken ct)
    {
        var motivo = Limpar(motivoIsencao, 10)?.ToUpperInvariant();
        var linhas = new List<Dictionary<string, object?>>();
        var ordem = 0;
        foreach (var item in venda.Items.OrderBy(i => i.CreatedAt))
        {
            var taxa = item.IvaRate;
            var fator = 1m + taxa / 100m;
            var bruto = item.Quantidade * item.PrecoUnitarioCents;
            var linha = new Dictionary<string, object?>
            {
                ["productId"] = await ProdutoAsync(key, companyId, cfg, t, item, ct),
                ["ordering"] = ++ordem,
                ["name"] = item.Descricao.Length > 200 ? item.Descricao[..200] : item.Descricao,
                ["qty"] = item.Quantidade,
                ["price"] = Math.Round(item.PrecoUnitarioCents / 100m / fator, 4, MidpointRounding.AwayFromZero),
            };
            if (item.DescontoCents > 0 && bruto > 0)
                linha["discount"] = Math.Round(item.DescontoCents * 100m / bruto, 4, MidpointRounding.AwayFromZero);
            if (taxa > 0)
            {
                var chave = ((int)taxa).ToString(CultureInfo.InvariantCulture);
                if (!cfg.Impostos.TryGetValue(chave, out var taxId))
                    throw new ValidationException("taxa_sem_imposto", $"O Moloni ON não tem o IVA {chave}% configurado.");
                linha["taxes"] = new[] { new { taxId, value = taxa, ordering = 1, cumulative = false } };
            }
            else
            {
                if (motivo is null)
                    throw new ValidationException("motivo_isencao_obrigatorio", $"\"{item.Descricao}\" está a 0% — escolhe o motivo de isenção (ex.: M40 autoliquidação).");
                linha["taxes"] = Array.Empty<object>();
                linha["exemptionReason"] = motivo;
            }
            linhas.Add(linha);
        }
        return linhas;
    }

    /// <summary>Artigo da loja → o seu artigo no Moloni (criado à 1.ª vez); resto → artigo genérico (serviço ou produto).</summary>
    private async Task<int> ProdutoAsync(string key, int companyId, MoloniConfig cfg, Tenant t, VendaItem item, CancellationToken ct)
    {
        if (item.CompraLinha?.ArtigoId is { } artigoId && await _artigos.FindByIdAsync(artigoId, ct) is { } artigo)
        {
            if (artigo.MoloniProductId is { } p) return p;
            artigo.MoloniProductId = await GarantirProdutoAsync(key, companyId, cfg, artigo.Sku, artigo.Nome, 1,
                (artigo.PrecoVendaCents ?? item.PrecoUnitarioCents) / 100m / 1.23m, ct);
            await _artigos.SaveAsync(ct);
            return artigo.MoloniProductId.Value;
        }
        if (item.CompraLinhaId is not null)
        {
            if (cfg.ProdutoArtigoId is { } a) return a;
            cfg.ProdutoArtigoId = await GarantirProdutoAsync(key, companyId, cfg, "MENDER-ARTIGO", "Artigo (Mender)", 1, 0m, ct);
            await GuardarConfigAsync(t, cfg, ct);
            return cfg.ProdutoArtigoId.Value;
        }
        if (cfg.ProdutoServicoId is { } s) return s;
        cfg.ProdutoServicoId = await GarantirProdutoAsync(key, companyId, cfg, "MENDER-SERVICO", "Serviço (Mender)", 2, 0m, ct);
        await GuardarConfigAsync(t, cfg, ct);
        return cfg.ProdutoServicoId.Value;
    }

    private async Task<int> GarantirProdutoAsync(string key, int companyId, MoloniConfig cfg, string referencia, string nome, int tipo, decimal preco, CancellationToken ct)
    {
        var existente = await _client.ExecutarAsync(key,
            "query($c:Int!,$r:String!){ products(companyId:$c, options:{ search:{field:reference, value:$r} }) { errors { field msg } data { productId reference } } }",
            new { c = companyId, r = referencia }, "products", ct);
        if (existente?.AsArray().FirstOrDefault(p => string.Equals(p!["reference"]?.GetValue<string>(), referencia, StringComparison.OrdinalIgnoreCase))
                ?["productId"]?.GetValue<int>() is { } id)
            return id;
        var taxId = cfg.Impostos.GetValueOrDefault("23");
        var criado = await _client.ExecutarAsync(key,
            "mutation($c:Int!,$d:ProductInsert!){ productCreate(companyId:$c, data:$d) { errors { field msg } data { productId } } }",
            new
            {
                c = companyId,
                d = new
                {
                    name = nome.Length > 200 ? nome[..200] : nome,
                    reference = referencia,
                    type = tipo,
                    productCategoryId = cfg.CategoriaId,
                    measurementUnitId = cfg.UnidadeId,
                    price = Math.Round(preco, 4),
                    taxes = taxId > 0 ? new[] { new { taxId, value = 23m, ordering = 1, cumulative = false } } : null,
                },
            }, "productCreate", ct);
        return criado!["productId"]!.GetValue<int>();
    }

    private async Task<int> MetodoPagamentoAsync(string key, int companyId, MoloniConfig cfg, Tenant t, PaymentMethod metodo, CancellationToken ct)
    {
        var nome = metodo switch
        {
            PaymentMethod.Dinheiro => "Dinheiro",
            PaymentMethod.Cartao => "Cartão",
            PaymentMethod.TransferenciaBancaria => "Transferência bancária",
            PaymentMethod.MBWay => "MB Way",
            PaymentMethod.Multibanco => "Multibanco",
            _ => "Outro",
        };
        if (cfg.MetodosPagamento.TryGetValue(Normalizar(nome), out var id)) return id;
        var criado = await _client.ExecutarAsync(key,
            "mutation($c:Int!,$d:PaymentMethodInsert!){ paymentMethodCreate(companyId:$c, data:$d) { errors { field msg } data { paymentMethodId } } }",
            new { c = companyId, d = new { name = nome } }, "paymentMethodCreate", ct);
        id = criado!["paymentMethodId"]!.GetValue<int>();
        cfg.MetodosPagamento[Normalizar(nome)] = id;
        await GuardarConfigAsync(t, cfg, ct);
        return id;
    }

    private static object Pagamento(int metodo, decimal valor, DateTime data)
        => new { paymentMethodId = metodo, value = valor, date = data.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) };

    private async Task<(int DocumentId, decimal Total)> CriarRascunhoAsync(string key, int companyId, string apiCode, string tipoInput,
        Dictionary<string, object?> dados, CancellationToken ct)
    {
        var d = await _client.ExecutarAsync(key,
            $"mutation($c:Int!,$d:{tipoInput}!){{ {apiCode}Create(companyId:$c, data:$d) {{ errors {{ field msg }} data {{ documentId totalValue }} }} }}",
            new { c = companyId, d = dados.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value) }, apiCode + "Create", ct);
        return (d!["documentId"]!.GetValue<int>(), d["totalValue"]!.GetValue<decimal>());
    }

    /// <summary>Se o Moloni calcular outro total, apaga o rascunho — nada é comunicado à AT.</summary>
    private async Task GarantirTotalAsync(string key, int companyId, string apiCode, int documentId, decimal totalMoloni, decimal totalVenda, CancellationToken ct)
    {
        if (Math.Abs(totalMoloni - totalVenda) <= ToleranciaTotal) return;
        await ApagarRascunhoAsync(key, companyId, apiCode, documentId, ct);
        throw new ValidationException("total_diferente",
            $"O Moloni ON calculou {totalMoloni:0.00} € e a venda tem {totalVenda:0.00} €. Não emiti nada (apaguei o rascunho) — revê os preços da venda.");
    }

    private async Task<(int Numero, string Serie)> FinalizarAsync(string key, int companyId, string apiCode, string tipoInput,
        Dictionary<string, object?> dados, CancellationToken ct)
    {
        try
        {
            var d = await _client.ExecutarAsync(key,
                $"mutation($c:Int!,$d:{tipoInput}!){{ {apiCode}Update(companyId:$c, data:$d) {{ errors {{ field msg }} data {{ documentId number documentSetName status }} }} }}",
                new { c = companyId, d = dados }, apiCode + "Update", ct);
            if (d?["status"]?.GetValue<int>() != 1 || d["number"] is not JsonValue nv || !nv.TryGetValue<int>(out var numero))
                throw new MoloniException("o documento não ficou finalizado.");
            return (numero, d["documentSetName"]!.GetValue<string>());
        }
        catch
        {
            await ApagarRascunhoAsync(key, companyId, apiCode, (int)dados["documentId"]!, ct);
            throw;
        }
    }

    private async Task ApagarRascunhoAsync(string key, int companyId, string apiCode, int documentId, CancellationToken ct)
    {
        try
        {
            await _client.ExecutarEscalarAsync(key,
                $"mutation($c:Int!,$d:[Int!]!){{ {apiCode}Delete(companyId:$c, documentId:$d) {{ status deletedCount errors {{ field msg }} }} }}",
                new { c = companyId, d = new[] { documentId } }, apiCode + "Delete", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Não consegui apagar o rascunho {ApiCode} {DocumentId} no Moloni ON", apiCode, documentId);
        }
    }

    private async Task NullifyAsync(string key, int companyId, string apiCode, int documentId, string motivo, CancellationToken ct)
        => await _client.ExecutarAsync(key,
            $"mutation($c:Int!,$d:Int!,$m:String){{ {apiCode}Nullify(companyId:$c, documentId:$d, nullifiedReason:$m) {{ errors {{ field msg }} data {{ documentId nullified }} }} }}",
            new { c = companyId, d = documentId, m = motivo }, apiCode + "Nullify", ct);

    private async Task GuardarPdfAsync(string key, int companyId, string apiCode, int documentId, Guid vendaId, CancellationToken ct)
    {
        try
        {
            var pdf = await ObterPdfAsync(key, companyId, apiCode, documentId, ct);
            await _faturas.AnexarPdfAsync(vendaId, pdf, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O documento já está emitido; o PDF pode ser pedido mais tarde ("PDF da fatura").
            _logger.LogWarning(ex, "PDF do {ApiCode} {DocumentId} ainda não disponível", apiCode, documentId);
        }
    }

    /// <summary>Gera o PDF no Moloni, espera pelo token (a geração é assíncrona) e descarrega.</summary>
    private async Task<byte[]> ObterPdfAsync(string key, int companyId, string apiCode, int documentId, CancellationToken ct)
    {
        await _client.ExecutarEscalarAsync(key, $"mutation($c:Int!,$d:Int!){{ {apiCode}GetPDF(companyId:$c, documentId:$d) }}",
            new { c = companyId, d = documentId }, apiCode + "GetPDF", ct);
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            if (tentativa > 0) await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
            try
            {
                var tok = await _client.ExecutarAsync(key,
                    $"query($d:Int!){{ {apiCode}GetPDFToken(documentId:$d) {{ errors {{ field msg }} data {{ token path filename }} }} }}",
                    new { d = documentId }, apiCode + "GetPDFToken", ct);
                if (tok?["token"]?.GetValue<string>() is { } token && tok["path"]?.GetValue<string>() is { } path)
                    return await _client.DescarregarAsync(path, token, ct);
            }
            catch (MoloniException) when (tentativa < 9)
            {
                // ainda a gerar
            }
        }
        throw new MoloniException("o PDF ainda não está pronto — tenta daqui a pouco.");
    }

    private static string Normalizar(string s) => s.Trim().ToLowerInvariant();

    private static string? Limpar(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
