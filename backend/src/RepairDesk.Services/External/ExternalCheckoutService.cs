using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Services.External;

/// <summary>
/// Endpoint atómico para integrações externas (loja online, kiosks, importadores) que
/// querem fechar uma venda inteira num único POST: cria/encontra cliente, cria venda
/// e marca paga (decrementa stock, emite garantia, opcionalmente emite fatura).
/// </summary>
public interface IExternalCheckoutService
{
    Task<ExternalCheckoutResponse> CheckoutAsync(ExternalCheckoutRequest req, CancellationToken ct = default);
    Task<ExternalOrderStatusResponse> GetOrderAsync(Guid vendaId, CancellationToken ct = default);
    Task<ExternalOrderStatusResponse> CancelOrderAsync(Guid vendaId, string? motivo, CancellationToken ct = default);
    Task<Clientes.PagedResult<ExternalPartDto>> ListPartsAsync(string? search, PartCategoria? categoria, int page, int pageSize, bool? lojaOnline = null, bool lowStockOnly = false, CancellationToken ct = default);
    Task<ExternalClienteHistoricoResponse?> GetHistoricoByNifAsync(string nif, CancellationToken ct = default);
    Task<ExternalGarantiaDetalhe?> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default);
    /// <summary>Sprint 122: catálogo de Products (telemóveis revendidos) para a loja online.</summary>
    Task<Clientes.PagedResult<ExternalProductDto>> ListProductsAsync(
        string? search, string? brand, int page, int pageSize,
        bool lowStockOnly = false,
        // Sprint 154: filtro incremental para reconciliação cron da loja.
        DateTime? updatedAfter = null,
        CancellationToken ct = default);
    Task<ExternalProductDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default);
}

/// <summary>
/// Sprint 122: produto da loja online — telemóvel revendido, com SEO/images/attributes.
/// Distinto de ExternalPart (peça técnica). Loja faz cron sync via /api/external/products.
/// </summary>
public sealed record ExternalProductDto(
    Guid Id,
    string Sku,
    string Slug,
    string Brand,
    string Model,
    string? Storage,
    string? Color,
    /// <summary>Sprint 122: nome do enum interno (ToString) — ex: "GradeA", "Premium".</summary>
    string Grading,
    /// <summary>Sprint 146: canonical estável para sync com a loja (A+/A/B+/B/C/OpenBox).</summary>
    string GradingCanonical,
    /// <summary>Sprint 146: label PT user-friendly ("Como novo", "Excelente", etc).</summary>
    string GradingLabel,
    /// <summary>Sprint 197: 2D classification — origem (new/used/refurbished). Schema.org itemCondition compatible.</summary>
    string Origin,
    /// <summary>Sprint 197: origem PT label ("Novo", "Usado original", "Recondicionado").</summary>
    string OriginLabel,
    /// <summary>Sprint 197: grade canonical (sealed/A++/A+/A/B+/B/C+/C). Display only — NÃO usar em URLs.</summary>
    string Grade,
    /// <summary>Sprint 202: URL-safe slug (a-plus-plus/a-plus/a/b-plus/b/c-plus/c/sealed).
    /// Usar em URLs, filtros query string, breadcrumbs, Google Shopping feeds.</summary>
    string GradeSlug,
    /// <summary>Sprint 197: grade PT label ("A++ · Como novo", etc).</summary>
    string GradeLabel,
    /// <summary>Sprint 197: label combinado ("Novo (selado)", "Usado original A++", "Recondicionado B").</summary>
    string ConditionCombined,
    /// <summary>Sprint 307: grade raw como veio do fornecedor (B+/C+/AB/A Premium). Loja preserva
    /// para mostrar exactamente o que o fornecedor classificou. Null para produtos pre-Sprint 305.</summary>
    string? SupplierGrade,
    string SupplyType,
    int PriceCents,
    int StockQuantity,
    /// <summary>Sprint 196: 'on-demand' (dropship — loja mostra 'por encomenda · entrega 3-5d')
    /// ou 'exact' (stock próprio — loja mostra quantidade real). Evita '999 disponível' que parece scam.</summary>
    string StockDisplayMode,
    string? DescriptionMarkdown,
    string? AttributesJson,
    string? SeoTitle,
    string? SeoDescription,
    /// <summary>Sprint 205: flag explícita open-box (loja mostra badge laranja + página /loja/open-box). NÃO usar Grade A++ para inferir.</summary>
    bool IsOpenBox,
    /// <summary>Sprint 204: saúde da bateria 0-100% (null = não aplicável). Loja usa para filtro 4-bucket.</summary>
    int? BatteryHealthPercent,
    /// <summary>Sprint 204: 'never_opened' | 'original_parts' | 'repaired' | null. Loja usa para selo trust + filtros.</summary>
    string? TechnicalState,
    /// <summary>Sprint 204: notas técnicas free-form ('Ecrã substituído · Bateria nova premium'). Loja mostra na PDP.</summary>
    string? TechnicalNotes,
    string? SupplierName,
    /// <summary>Sprint 122 (back-compat): array de URLs originais. Lojas novas devem usar Images com sizes/blur.</summary>
    IReadOnlyList<string> ImageUrls,
    /// <summary>Sprint 189: imagens com pipeline SEO (sizes WebP + blur LQIP + alt + dimensões).
    /// Quando Sizes != null: produto tem versões optimizadas. Quando null: imagem legacy só com Url original.</summary>
    IReadOnlyList<ExternalProductImageDto> Images,
    DateTime UpdatedAt,
    /// <summary>Sprint 359: preço da bateria nova deste modelo (cêntimos). Null = modelo sem
    /// upgrade. A elegibilidade é decidida no shop (condition≠new && !isOpenBox && bateria&lt;95%).</summary>
    int? BatteryUpgradePriceCents = null,
    /// <summary>Sprint 359: id do modelo-template, para o shop agrupar variantes do mesmo modelo
    /// numa página única com seletor (cor/capacidade/grade). Null = produto independente.</summary>
    Guid? ModelTemplateId = null,
    /// <summary>Sprint 359: série de marketing herdada do modelo (ex: "iPhone 15"). Null se sem modelo.</summary>
    string? Series = null);

/// <summary>Sprint 189: imagem rica com sizes para &lt;picture&gt; srcset + LQIP placeholder.</summary>
public sealed record ExternalProductImageDto(
    string Url,
    string? Alt,
    ExternalProductImageSizes? Sizes,
    string? BlurDataUrl,
    int? Width,
    int? Height);

public sealed record ExternalProductImageSizes(
    string Webp480w,
    string Webp1024w,
    string Webp2048w);

/// <summary>Resposta do health check — usada por integradores para clock skew e confirmação do tenant.</summary>
public sealed record ExternalHealthResponse(
    string Status,
    DateTimeOffset ServerTime,
    string ApiVersion,
    Guid? TenantId);

/// <summary>Detalhe da garantia para integrações externas — espelha PublicGarantiaDto sem mascaramento.</summary>
public sealed record ExternalGarantiaDetalhe(
    string Slug,
    string Origem,
    DateTime DataInicio,
    DateTime DataFim,
    int DiasGarantia,
    int DiasRestantes,
    bool Activa,
    bool Anulada,
    string? MotivoAnulacao,
    string Equipamento,
    string? Cobertura,
    string? Exclusoes,
    string? DocumentoReferencia,
    string? NumeroFatura);

/// <summary>
/// Histórico agregado de um cliente — vendas, reparações e garantias activas.
/// Pensado para loja online mostrar "Os meus pedidos" sem replicar BD.
/// Null se NIF não corresponde a cliente do tenant.
/// </summary>
public sealed record ExternalClienteHistoricoResponse(
    Guid ClienteId,
    string Nome,
    string? Email,
    string? Telefone,
    IReadOnlyList<ExternalVendaResumo> Vendas,
    IReadOnlyList<ExternalReparacaoResumo> Reparacoes,
    IReadOnlyList<ExternalGarantiaResumo> GarantiasActivas);

public sealed record ExternalVendaResumo(
    Guid Id,
    int Numero,
    DateTime Data,
    int TotalCents,
    string Status,
    string Origem,
    string? FaturaNumero,
    string? FaturaPdfUrl,
    /// <summary>Sprint 133: slug da garantia digital desta venda (`/g/{slug}`). Null se anulada ou ainda não emitida.</summary>
    string? GarantiaSlug = null,
    /// <summary>Sprint 133: garantia activa (não expirada nem anulada). Permite à loja decidir se mostra "Ver garantia" ou "Garantia expirada".</summary>
    bool GarantiaActiva = false);

public sealed record ExternalReparacaoResumo(
    Guid Id,
    int Numero,
    DateTime RecebidoEm,
    string Equipamento,
    int Estado,
    /// <summary>Slug público /p/{slug} para acompanhamento.</summary>
    string? PublicSlug,
    /// <summary>Sprint 133: garantia da reparação (60d/90d/etc).</summary>
    string? GarantiaSlug = null,
    bool GarantiaActiva = false);

public sealed record ExternalGarantiaResumo(
    string Slug,
    string Origem,           // "Reparacao" ou "Venda"
    DateTime DataFim,
    int DiasRestantes,
    string? Equipamento);

/// <summary>
/// Versão pública do catálogo — NÃO expõe custo, fornecedor, local armazenamento, notas internas.
/// Para uso por integrações externas (loja online) que precisam de listar acessórios disponíveis.
/// </summary>
public sealed record ExternalPartDto(
    Guid Id,
    string? Sku,
    string Nome,
    PartCategoria Categoria,
    string? Marca,
    string? Modelo,
    /// <summary>Sprint 121: true se Bruno marcou esta peça para aparecer na loja online.</summary>
    bool MostrarLojaOnline,
    /// <summary>Stock disponível atualmente.</summary>
    int QtdStock,
    bool Activo);

public sealed record ExternalOrderStatusResponse(
    Guid VendaId,
    int VendaNumero,
    DateTime Data,
    Guid? ClienteId,
    int TotalCents,
    int IvaCents,
    /// <summary>"Pendente" / "Paga" / "Cancelada".</summary>
    string Status,
    /// <summary>"Balcao" / "Online" / "Importacao".</summary>
    string Origem,
    string? FaturaNumero,
    string? FaturaPdfUrl,
    DateTime? FaturaEmitidaEm,
    string? GarantiaSlug,
    bool GarantiaActiva,
    bool GarantiaAnulada,
    DateTime? CanceladaEm);

public sealed record CancelOrderRequest(string? Motivo);

public sealed record ExternalCheckoutRequest(
    /// <summary>Dados do cliente final. NIF é opcional mas recomendado para emissão de fatura PT.</summary>
    ExternalCheckoutCliente Cliente,
    IReadOnlyList<CreateVendaItemRequest> Items,
    PaymentMethod PaymentMethod,
    /// <summary>Quando true, emite fatura via provider de billing (Moloni/InvoiceXpress) após marcar paga.</summary>
    bool EmitirFatura = true,
    string? Notas = null,
    /// <summary>Default Online — para integrações externas. Override apenas se for outro canal.</summary>
    VendaOrigem? Origem = null);

public sealed record ExternalCheckoutCliente(
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif,
    string? Notas);

public sealed record ExternalCheckoutResponse(
    Guid VendaId,
    int VendaNumero,
    Guid ClienteId,
    bool ClienteCreated,
    int TotalCents,
    int IvaCents,
    string? FaturaNumero,
    string? FaturaPdfUrl,
    /// <summary>Slug da garantia digital (URL pública: /g/{slug}).</summary>
    string? GarantiaSlug,
    /// <summary>
    /// Sprint 127: garantia efectiva emitida (em dias) — Max das condições dos items.
    /// Igual a <c>Items.Max(i =&gt; i.GarantiaDias)</c>; exposto para conveniência.
    /// </summary>
    int? GarantiaDiasEfectivo,
    /// <summary>
    /// Sprint 127: items com prazo de garantia individual calculado a partir da
    /// condicao + defaults do tenant. A loja usa estes para mostrar prazo correcto
    /// por produto em /conta/garantias.
    /// </summary>
    IReadOnlyList<ExternalCheckoutItemSummary> Items);

public sealed record ExternalCheckoutItemSummary(
    string Descricao,
    int Quantidade,
    /// <summary>"Novo" | "OpenBox" | "Recondicionado" | "Usado" | "NaoAplicavel".</summary>
    string Condicao,
    /// <summary>Período de garantia em dias para este item (calculado pelo tenant settings).</summary>
    int GarantiaDias);

public class ExternalCheckoutService : IExternalCheckoutService
{
    private readonly IClienteService _clientes;
    private readonly IVendaService _vendas;
    private readonly IGarantiaRepository _garantias;
    private readonly IAuditLogger _audit;
    private readonly ITenantContext _tenant;
    private readonly IPartRepository _parts;
    private readonly IClienteRepository _clienteRepo;
    private readonly IVendaRepository _vendaRepo;
    private readonly IReparacaoRepository _reparacaoRepo;
    private readonly IProductRepository _products;
    private readonly ITenantRepository _tenants;

    public ExternalCheckoutService(
        IClienteService clientes,
        IVendaService vendas,
        IGarantiaRepository garantias,
        IAuditLogger audit,
        ITenantContext tenant,
        IPartRepository parts,
        IClienteRepository clienteRepo,
        IVendaRepository vendaRepo,
        IReparacaoRepository reparacaoRepo,
        IProductRepository products,
        ITenantRepository tenants)
    {
        _clientes = clientes;
        _vendas = vendas;
        _garantias = garantias;
        _audit = audit;
        _tenant = tenant;
        _parts = parts;
        _clienteRepo = clienteRepo;
        _vendaRepo = vendaRepo;
        _reparacaoRepo = reparacaoRepo;
        _products = products;
        _tenants = tenants;
    }

    public async Task<Clientes.PagedResult<ExternalProductDto>> ListProductsAsync(
        string? search, string? brand, int page, int pageSize,
        bool lowStockOnly = false,
        DateTime? updatedAfter = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        // Externo: sempre MostrarLojaOnline=true + Active. Não revela CustoUnitarioCents nem FornecedorId.
        var (items, total) = await _products.SearchAsync(
            search: search,
            brand: brand,
            lojaOnline: true,
            fornecedorId: null,
            ativo: true,
            mostrarLojaOnline: true,
            sort: null,
            includeInactive: false,
            page: page,
            pageSize: pageSize,
            ct: ct);
        // Sprint 132: filter in-memory (lowStock + Sprint 154 updatedAfter). Catálogo refurbished
        // tipicamente <500 items. Promover ao SQL se Bruno chegar a milhares.
        var query = items.AsEnumerable();
        if (lowStockOnly) query = query.Where(p => p.StockMinima > 0 && p.StockQuantity <= p.StockMinima);
        if (updatedAfter is { } cutoff) query = query.Where(p => (p.UpdatedAt ?? p.CreatedAt) > cutoff);
        var dtos = query.Select(ToExternalProductDto).ToList();
        return new Clientes.PagedResult<ExternalProductDto>(dtos, page, pageSize, total);
    }

    public async Task<ExternalProductDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 200) return null;
        var p = await _products.FindBySlugAsync(slug, ct);
        if (p is null || !p.Active || !p.MostrarLojaOnline) return null;
        return ToExternalProductDto(p);
    }

    /// <summary>Sprint 359: imagens efectivas — as da unidade se existirem, senão as de marketing do modelo.</summary>
    private static List<ExternalProductImageDto> ResolveImages(RepairDesk.Core.Entities.Product p)
    {
        if (p.Images.Count > 0)
            return p.Images.OrderBy(i => i.Ordem).Select(i => new ExternalProductImageDto(
                i.Url, i.Alt,
                i.Url480w is not null && i.Url1024w is not null && i.Url2048w is not null
                    ? new ExternalProductImageSizes(i.Url480w, i.Url1024w, i.Url2048w) : null,
                i.BlurDataUrl, i.Width, i.Height)).ToList();
        var mt = p.ModelTemplate;
        if (mt is null || mt.Images.Count == 0) return new List<ExternalProductImageDto>();
        return mt.Images.OrderBy(i => i.Ordem).Select(i => new ExternalProductImageDto(
            i.Url, i.Alt,
            i.Url480w is not null && i.Url1024w is not null && i.Url2048w is not null
                ? new ExternalProductImageSizes(i.Url480w, i.Url1024w, i.Url2048w) : null,
            i.BlurDataUrl, i.Width, i.Height)).ToList();
    }

    private static ExternalProductDto ToExternalProductDto(RepairDesk.Core.Entities.Product p) => new(
        p.Id, p.Sku, p.Slug, p.Brand, p.Model, p.Storage, p.Color,
        Grading: p.Grading.ToString(),
        GradingCanonical: RepairDesk.Services.Products.ProductGradingMapper.ToCanonical(p.Grading),
        GradingLabel: RepairDesk.Services.Products.ProductGradingMapper.ToLabelPt(p.Grading),
        // Sprint 197: 2D origin+grade — loja deve usar isto em vez do grading legacy.
        Origin: RepairDesk.Services.Products.ProductGradingMapper.OriginCanonical(p.Origin),
        OriginLabel: RepairDesk.Services.Products.ProductGradingMapper.OriginLabelPt(p.Origin),
        Grade: RepairDesk.Services.Products.ProductGradingMapper.GradeCanonical(p.Grade),
        GradeSlug: RepairDesk.Services.Products.ProductGradingMapper.GradeSlug(p.Grade),
        GradeLabel: RepairDesk.Services.Products.ProductGradingMapper.GradeLabelPt(p.Grade),
        ConditionCombined: RepairDesk.Services.Products.ProductGradingMapper.ComposedLabelPt(p.Origin, p.Grade),
        SupplierGrade: p.SupplierGrade,
        SupplyType: p.SupplyType.ToString(),
        PriceCents: p.PriceCents, StockQuantity: p.StockQuantity,
        StockDisplayMode: p.SupplyType == ProductSupplyType.Dropship ? "on-demand" : "exact",
        // Sprint 359: herança override-com-fallback — usa o valor da unidade se existir,
        // senão herda do modelo-template.
        DescriptionMarkdown: !string.IsNullOrWhiteSpace(p.DescriptionMarkdown)
            ? p.DescriptionMarkdown
            : p.ModelTemplate?.DescriptionMarkdown,
        AttributesJson: !string.IsNullOrWhiteSpace(p.AttributesJson)
            ? p.AttributesJson
            : p.ModelTemplate?.SpecsJson,
        SeoTitle: p.SeoTitle, SeoDescription: p.SeoDescription,
        IsOpenBox: p.IsOpenBox,
        BatteryHealthPercent: p.BatteryHealthPercent,
        TechnicalState: p.TechnicalState == RepairDesk.Core.Enums.ProductTechnicalState.Unknown
            ? null
            : p.TechnicalState switch
            {
                RepairDesk.Core.Enums.ProductTechnicalState.NeverOpened => "never_opened",
                RepairDesk.Core.Enums.ProductTechnicalState.OriginalParts => "original_parts",
                RepairDesk.Core.Enums.ProductTechnicalState.Repaired => "repaired",
                _ => null,
            },
        TechnicalNotes: p.TechnicalNotes,
        SupplierName: p.Fornecedor?.Name,
        // Sprint 359: imagens herdadas do modelo quando a unidade não tem próprias.
        ImageUrls: ResolveImages(p).Select(i => i.Url).ToList(),
        Images: ResolveImages(p),
        UpdatedAt: p.UpdatedAt ?? p.CreatedAt,
        // Sprint 359: campos herdados do modelo-template (back-compat: defaults null).
        BatteryUpgradePriceCents: p.ModelTemplate?.BatteryUpgradePriceCents,
        ModelTemplateId: p.ModelId,
        Series: p.ModelTemplate?.Series);

    public async Task<ExternalClienteHistoricoResponse?> GetHistoricoByNifAsync(string nif, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nif)) return null;
        var cliente = await _clienteRepo.FindByNifAsync(nif.Trim(), ct);
        if (cliente is null) return null;

        var (vendas, _) = await _vendaRepo.SearchAsync(null, null, cliente.Id, 1, 100, ct);
        var (reparacoes, _) = await _reparacaoRepo.SearchAsync(null, null, cliente.Id, 1, 100, ct);

        var agora = DateTime.UtcNow;
        var garantiasActivas = new List<ExternalGarantiaResumo>();

        // Sprint 133: 1 pass por venda — busca garantia uma vez, popula resumo + lista activas.
        var vendaResumos = new List<ExternalVendaResumo>(vendas.Count);
        foreach (var v in vendas)
        {
            var g = await _garantias.FindByVendaAsync(v.Id, ct);
            var activa = g is not null && !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;
            vendaResumos.Add(new ExternalVendaResumo(
                v.Id, v.Numero, v.Data, v.TotalCents,
                v.Status.ToString(), v.Origem.ToString(),
                v.InvoiceNumber, v.InvoicePdfUrl,
                g?.Slug, activa));
            if (activa)
            {
                garantiasActivas.Add(new ExternalGarantiaResumo(
                    g!.Slug, "Venda", g.DataFim,
                    (int)Math.Max(0, (g.DataFim - agora).TotalDays),
                    v.Items.FirstOrDefault()?.Descricao));
            }
        }

        var reparacaoResumos = new List<ExternalReparacaoResumo>(reparacoes.Count);
        foreach (var r in reparacoes)
        {
            var g = await _garantias.FindByReparacaoAsync(r.Id, ct);
            var activa = g is not null && !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;
            reparacaoResumos.Add(new ExternalReparacaoResumo(
                r.Id, r.Numero, r.CreatedAt, r.Equipamento, (int)r.Estado, r.PublicSlug,
                g?.Slug, activa));
            if (activa)
            {
                garantiasActivas.Add(new ExternalGarantiaResumo(
                    g!.Slug, "Reparacao", g.DataFim,
                    (int)Math.Max(0, (g.DataFim - agora).TotalDays),
                    r.Equipamento));
            }
        }

        return new ExternalClienteHistoricoResponse(
            cliente.Id, cliente.Nome, cliente.Email, cliente.Telefone,
            vendaResumos, reparacaoResumos, garantiasActivas);
    }

    public async Task<ExternalGarantiaDetalhe?> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var g = await _garantias.FindBySlugAsync(slug.Trim(), ct);
        if (g is null) return null;

        var agora = DateTime.UtcNow;
        var activa = !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;
        var diasRestantes = (int)Math.Max(0, (g.DataFim - agora).TotalDays);

        string equipamento;
        string origem;
        string? docRef;
        string? numFatura;
        if (g.SourceType == GarantiaSourceType.Venda && g.Venda is not null)
        {
            equipamento = g.Venda.Items.FirstOrDefault()?.Descricao ?? "Artigos vendidos";
            origem = "Venda";
            docRef = $"Venda #{g.Venda.Numero:D5}";
            numFatura = g.Venda.InvoiceNumber;
        }
        else
        {
            equipamento = g.Reparacao?.Equipamento ?? "Equipamento";
            origem = "Reparacao";
            docRef = g.Reparacao is not null ? $"Reparação #{g.Reparacao.Numero:D5}" : null;
            numFatura = null;
        }

        return new ExternalGarantiaDetalhe(
            g.Slug, origem, g.DataInicio, g.DataFim, g.DiasGarantia, diasRestantes,
            activa, g.Anulada, g.MotivoAnulacao,
            equipamento, g.Cobertura, g.Exclusoes, docRef, numFatura);
    }

    public async Task<Clientes.PagedResult<ExternalPartDto>> ListPartsAsync(
        string? search, PartCategoria? categoria, int page, int pageSize, bool? lojaOnline = null, bool lowStockOnly = false, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        // Sprint 132: lowStockOnly delegado ao repo — filter eficiente no SQL.
        var (items, total) = await _parts.SearchAsync(search, categoria, marca: null, lowStockOnly, page, pageSize, ct);
        var query = items.Where(p => p.Activo);
        if (lojaOnline.HasValue)
            query = query.Where(p => p.MostrarLojaOnline == lojaOnline.Value);
        var dtos = query
            .Select(p => new ExternalPartDto(
                p.Id, p.Sku, p.Nome, p.Categoria, p.Marca, p.Modelo, p.MostrarLojaOnline, p.QtdStock, p.Activo))
            .ToList();
        return new Clientes.PagedResult<ExternalPartDto>(dtos, page, pageSize, total);
    }

    public async Task<ExternalCheckoutResponse> CheckoutAsync(ExternalCheckoutRequest req, CancellationToken ct = default)
    {
        if (req.Items is null || req.Items.Count == 0)
            throw new ValidationException("items_required", "Pelo menos uma linha é obrigatória.");
        if (string.IsNullOrWhiteSpace(req.Cliente.Nome))
            throw new ValidationException("cliente_nome_required", "Nome do cliente é obrigatório.");

        // 1. Cliente — lookup por NIF ou cria. Idempotente.
        var clienteResp = await _clientes.LookupOrCreateAsync(new CreateClienteRequest(
            req.Cliente.Nome,
            req.Cliente.Telefone,
            req.Cliente.Email,
            req.Cliente.Nif,
            req.Cliente.Notas), ct);

        // 2. Venda — criar com origem default Online (integração externa).
        var venda = await _vendas.CreateAsync(new CreateVendaRequest(
            clienteResp.Cliente.Id,
            req.Items,
            req.Notas,
            req.Origem ?? VendaOrigem.Online), ct);

        // 3. Marcar paga — decrementa stock, emite garantia, opcionalmente fatura.
        var paga = await _vendas.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(
            req.PaymentMethod,
            EmitirFatura: req.EmitirFatura), ct);

        // 4. Garantia (Sprint 58 auto-emit) — fetch slug para incluir na response.
        var garantia = await _garantias.FindByVendaAsync(venda.Id, ct);

        await _audit.LogAsync(
            AuditAction.Create,
            "ExternalCheckout",
            paga.Venda.Id,
            new
            {
                origem = paga.Venda.Origem.ToString(),
                clienteCreated = clienteResp.Created,
                total = paga.Venda.TotalCents,
                faturaEmitida = paga.Invoice is not null,
                garantiaSlug = garantia?.Slug,
            },
            _tenant.TenantId,
            ct: ct);

        // Sprint 127: per-item garantia em dias (loja precisa para /conta/garantias).
        var tenantEntity = _tenant.TenantId is { } tid ? await _tenants.FindByIdAsync(tid, ct) : null;
        var itemsSummary = BuildItemsGarantiaSummary(paga.Venda.Items, tenantEntity);
        var garantiaEfectivo = itemsSummary.Count > 0 ? itemsSummary.Max(i => i.GarantiaDias) : (int?)null;

        return new ExternalCheckoutResponse(
            VendaId: paga.Venda.Id,
            VendaNumero: paga.Venda.Numero,
            ClienteId: clienteResp.Cliente.Id,
            ClienteCreated: clienteResp.Created,
            TotalCents: paga.Venda.TotalCents,
            IvaCents: paga.Venda.IvaCents,
            FaturaNumero: paga.Invoice?.Number,
            FaturaPdfUrl: paga.Invoice?.PdfUrl,
            GarantiaSlug: garantia?.Slug,
            GarantiaDiasEfectivo: garantiaEfectivo,
            Items: itemsSummary);
    }

    private static IReadOnlyList<ExternalCheckoutItemSummary> BuildItemsGarantiaSummary(
        IEnumerable<VendaItemDto> items, Tenant? tenant)
    {
        var novoDias = tenant?.GarantiaVendaDiasDefault ?? 1095;
        var openBox = tenant?.GarantiaVendaOpenBoxDias ?? 730;
        var recond = tenant?.GarantiaVendaRecondicionadoDias ?? 540;
        var usado = tenant?.GarantiaVendaUsadoDias ?? 540;
        return items
            .Select(it => new ExternalCheckoutItemSummary(
                Descricao: it.Descricao,
                Quantidade: it.Quantidade,
                Condicao: it.Condicao.ToString(),
                GarantiaDias: Vendas.VendaService.DiasParaCondicao(it.Condicao, novoDias, openBox, recond, usado)))
            .ToList();
    }

    public async Task<ExternalOrderStatusResponse> GetOrderAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.GetAsync(vendaId, ct);
        var garantia = await _garantias.FindByVendaAsync(vendaId, ct);
        return BuildOrderStatus(venda, garantia);
    }

    public async Task<ExternalOrderStatusResponse> CancelOrderAsync(Guid vendaId, string? motivo, CancellationToken ct = default)
    {
        var venda = await _vendas.GetAsync(vendaId, ct);

        // Idempotente: se já cancelada, devolve estado actual sem repetir trabalho.
        if (venda.Status == VendaStatus.Cancelada)
        {
            var garantiaExistente = await _garantias.FindByVendaAsync(vendaId, ct);
            return BuildOrderStatus(venda, garantiaExistente);
        }

        // CancelarAsync já cascateia: anula fatura Moloni/InvoiceXpress + revert stock (Sprint 54).
        var cancelada = await _vendas.CancelarAsync(vendaId, ct);

        // Sprint 74: também anula a garantia (não há produto a cobrir — venda cancelada).
        var garantia = await _garantias.FindByVendaAsync(vendaId, ct);
        if (garantia is not null && !garantia.Anulada)
        {
            garantia.Anulada = true;
            garantia.MotivoAnulacao = string.IsNullOrWhiteSpace(motivo)
                ? "Venda cancelada via integração externa."
                : $"Venda cancelada: {motivo.Trim()}";
            await _garantias.SaveAsync(ct);
        }

        await _audit.LogAsync(
            AuditAction.Update,
            "ExternalCheckout",
            vendaId,
            new
            {
                operation = "cancel",
                motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
                garantiaAnulada = garantia is not null,
            },
            _tenant.TenantId,
            ct: ct);

        return BuildOrderStatus(cancelada, garantia);
    }

    private static ExternalOrderStatusResponse BuildOrderStatus(VendaDto venda, Core.Entities.Garantia? garantia)
    {
        var agora = DateTime.UtcNow;
        var garantiaActiva = garantia is not null
            && !garantia.Anulada
            && agora >= garantia.DataInicio
            && agora <= garantia.DataFim;
        return new ExternalOrderStatusResponse(
            VendaId: venda.Id,
            VendaNumero: venda.Numero,
            Data: venda.Data,
            ClienteId: venda.Cliente?.Id,
            TotalCents: venda.TotalCents,
            IvaCents: venda.IvaCents,
            Status: venda.Status.ToString(),
            Origem: venda.Origem.ToString(),
            FaturaNumero: venda.InvoiceNumber,
            FaturaPdfUrl: venda.InvoicePdfUrl,
            FaturaEmitidaEm: venda.InvoiceEmittedAt,
            GarantiaSlug: garantia?.Slug,
            GarantiaActiva: garantiaActiva,
            GarantiaAnulada: garantia?.Anulada ?? false,
            CanceladaEm: venda.Status == VendaStatus.Cancelada ? venda.Data : null);
    }
}
