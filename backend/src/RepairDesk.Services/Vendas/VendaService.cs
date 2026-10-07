using System.Globalization;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Fiscal;
using RepairDesk.Services.TenantPreferences;

namespace RepairDesk.Services.Vendas;

public interface IVendaService
{
    Task<PagedResult<VendaDto>> SearchAsync(VendaFiltro filtro, int page, int pageSize, CancellationToken ct = default);
    Task<VendaImeiLookupDto?> ImeiLookupAsync(string imei, CancellationToken ct = default);
    Task<IReadOnlyList<VendaReparacaoRelacionadaDto>> GetReparacoesRelacionadasAsync(Guid vendaId, CancellationToken ct = default);
    Task<VendaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> CreateAsync(VendaWriteRequest req, CancellationToken ct = default);
    Task<VendaDto> UpdateAsync(Guid id, VendaWriteRequest req, CancellationToken ct = default);
    Task<VendaDto> MudarEstadoAsync(Guid id, MudarEstadoVendaRequest req, CancellationToken ct = default);
    Task<VendaDto> RegistarFaturaAsync(Guid id, RegistarFaturaRequest req, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

/// <summary>
/// Vendas unificadas (Doc 94 Fase 4, SPEC §2.4 + §5.3 + §5.5): reparação, serviço ou produto, com
/// linhas de serviço ou de lote de stock.
///
/// Stock: Orçamento não mexe no stock. Nos estados À espera de peça / Pronta / Entregue as unidades
/// estão fora dos lotes (<c>CompraLinha.QuantidadeVendida</c>); Cancelada devolve-as. Ao editar,
/// liberta as linhas antigas e volta a consumir as novas — tudo no mesmo SaveChanges.
///
/// Snapshot: custo e taxa de IVA do lote são copiados para a linha e refrescados ao passar a
/// Entregue, para os relatórios não mudarem se a compra for editada depois (SPEC §2.4).
///
/// "Não existe venda sem fatura": Entregue sem nº de fatura fica em "Fatura por registar" (alerta).
/// </summary>
public class VendaService : IVendaService
{
    private const int MaxLinhas = 200;

    private readonly IVendaRepository _vendas;
    private readonly ICompraRepository _compras;
    private readonly IClienteRepository _clientes;
    private readonly ITenantContext _tenant;
    private readonly IGarantiaRepository _garantias;
    private readonly ITenantRepository _tenants;
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITenantPreferencesService _preferences;
    private readonly IAuditLogger _audit;

    public VendaService(
        IVendaRepository vendas,
        ICompraRepository compras,
        IClienteRepository clientes,
        ITenantContext tenant,
        IGarantiaRepository garantias,
        ITenantRepository tenants,
        IReparacaoRepository reparacoes,
        ITenantPreferencesService preferences,
        IAuditLogger audit)
    {
        _vendas = vendas;
        _compras = compras;
        _clientes = clientes;
        _tenant = tenant;
        _garantias = garantias;
        _tenants = tenants;
        _reparacoes = reparacoes;
        _preferences = preferences;
        _audit = audit;
    }

    private static decimal TaxaVendaNormalPct => FiscalDefaults.TaxaIvaNormal * 100m;

    public static bool ConsomeStock(VendaEstado estado)
        => estado is VendaEstado.AEsperaPeca or VendaEstado.Pronta or VendaEstado.Entregue;

    public async Task<PagedResult<VendaDto>> SearchAsync(VendaFiltro filtro, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _vendas.SearchAsync(filtro, page, pageSize, ct);
        return new PagedResult<VendaDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<VendaImeiLookupDto?> ImeiLookupAsync(string imei, CancellationToken ct = default)
    {
        var clean = ImeiValidator.Normalize(imei);
        if (string.IsNullOrEmpty(clean) || !ImeiValidator.IsValid(clean)) return null;
        var row = await _vendas.FindVendaByImeiAsync(clean, ct);
        if (row is null) return null;
        return new VendaImeiLookupDto(row.VendaId, row.Numero, row.Data, row.Descricao, row.ClienteNome);
    }

    public async Task<IReadOnlyList<VendaReparacaoRelacionadaDto>> GetReparacoesRelacionadasAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        var imeis = venda.Items.Select(i => i.Imei).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().ToList();
        var resultado = new List<VendaReparacaoRelacionadaDto>();
        foreach (var imei in imeis)
        {
            var reparacoes = await _reparacoes.SearchByImeiAsync(imei, excludeId: null, ct);
            resultado.AddRange(reparacoes.Where(r => r.CreatedAt > venda.Data).Select(r => new VendaReparacaoRelacionadaDto(
                r.Id, r.Numero, r.CreatedAt, r.Equipamento, r.Imei ?? imei, (int)r.Estado,
                (int)Math.Round((r.CreatedAt - venda.Data).TotalDays), r.OrcamentoCents)));
        }
        return resultado.OrderByDescending(r => r.RecebidoEm).ToList();
    }

    public async Task<VendaDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id));

    public async Task<VendaDto> CreateAsync(VendaWriteRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        var estado = req.Estado ?? VendaEstado.Orcamento;
        if (estado == VendaEstado.Cancelada)
            throw new ValidationException("estado_invalido", "Uma venda nova não pode nascer cancelada.");
        if (estado == VendaEstado.Entregue && req.Linhas.Count == 0)
            throw new ValidationException("venda_sem_linhas", "Adiciona pelo menos uma linha antes de entregar.");

        var venda = new Venda { TenantId = tenantId, Estado = estado };
        await ApplyHeaderAsync(venda, req, ct);
        var lotes = await LoadLotesAsync(req.Linhas, [], ct);
        ApplyLinhas(venda, req.Linhas, lotes);
        if (ConsomeStock(estado)) ConsumirStock(venda, lotes);
        if (estado == VendaEstado.Entregue) MarcarEntregue(venda, req.PaymentMethod, lotes);
        RecalculateTotals(venda);

        await _vendas.CreateWithNextNumeroAsync(venda, tenantId, ct);
        if (estado == VendaEstado.Entregue) await EmitirGarantiaSeAplicavelAsync(venda, ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Venda), venda.Id,
            new { venda.Numero, venda.Tipo, venda.Estado, venda.TotalCents }, ct: ct);
        return ToDto(venda);
    }

    public async Task<VendaDto> UpdateAsync(Guid id, VendaWriteRequest req, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (venda.Estado is VendaEstado.Entregue or VendaEstado.Cancelada)
            throw new ConflictException("venda_fechada",
                "Venda entregue ou cancelada não pode ser alterada (a fatura já foi emitida). Cancela e cria outra se for preciso.");

        await ApplyHeaderAsync(venda, req, ct);
        var lotes = await LoadLotesAsync(req.Linhas, venda.Items, ct);
        var consome = ConsomeStock(venda.Estado);
        if (consome) LibertarStock(venda, lotes);
        ApplyLinhas(venda, req.Linhas, lotes);
        if (consome) ConsumirStock(venda, lotes);
        RecalculateTotals(venda);

        await _vendas.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Venda), venda.Id, new { venda.Numero, venda.TotalCents }, ct: ct);
        return ToDto(venda);
    }

    public async Task<VendaDto> MudarEstadoAsync(Guid id, MudarEstadoVendaRequest req, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        var de = venda.Estado;
        var para = req.Estado;
        if (de == para) return ToDto(venda);
        if (de == VendaEstado.Cancelada)
            throw new ConflictException("venda_cancelada", "Venda cancelada não pode mudar de estado.");
        if (de == VendaEstado.Entregue && para != VendaEstado.Cancelada)
            throw new ConflictException("venda_entregue",
                "Venda entregue só pode ser cancelada (emite a nota de crédito no programa de faturação).");
        if (para == VendaEstado.Entregue && venda.Items.Count == 0)
            throw new ValidationException("venda_sem_linhas", "Adiciona pelo menos uma linha antes de entregar.");

        var lotes = await LoadLotesAsync([], venda.Items, ct);
        if (ConsomeStock(de) && !ConsomeStock(para)) LibertarStock(venda, lotes);
        if (!ConsomeStock(de) && ConsomeStock(para)) ConsumirStock(venda, lotes);

        venda.Estado = para;
        if (para == VendaEstado.Entregue)
        {
            MarcarEntregue(venda, req.PaymentMethod, lotes);
            RecalculateTotals(venda);
        }

        await _vendas.SaveAsync(ct);
        if (para == VendaEstado.Entregue) await EmitirGarantiaSeAplicavelAsync(venda, ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Venda), venda.Id, new { venda.Numero, de, para }, ct: ct);
        return ToDto(venda);
    }

    public async Task<VendaDto> RegistarFaturaAsync(Guid id, RegistarFaturaRequest req, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        var numero = Clean(req.InvoiceNumber, 120);
        venda.InvoiceNumber = numero;
        venda.InvoiceEmittedAt = numero is null
            ? null
            : DateTime.SpecifyKind((req.InvoiceEmittedAt ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
        await _vendas.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Venda), venda.Id, new { venda.Numero, venda.InvoiceNumber }, ct: ct);
        return ToDto(venda);
    }

    // ---------- regras ----------

    private async Task ApplyHeaderAsync(Venda venda, VendaWriteRequest req, CancellationToken ct)
    {
        if (req.Linhas is null || req.Linhas.Count > MaxLinhas)
            throw new ValidationException("linhas_invalidas", $"Máximo {MaxLinhas} linhas por venda.");
        if (req.ClienteId is { } clienteId)
        {
            venda.Cliente = await _clientes.FindByIdAsync(clienteId, ct) ?? throw new NotFoundException("Cliente", clienteId);
            venda.ClienteId = clienteId;
        }
        else
        {
            venda.Cliente = null;
            venda.ClienteId = null;
        }
        if (req.Tipo == VendaTipo.Reparacao && venda.ClienteId is null)
            throw new ValidationException("cliente_obrigatorio", "Uma reparação precisa de cliente (para o avisar quando estiver pronta).");

        venda.Tipo = req.Tipo;
        venda.Equipamento = Clean(req.Equipamento, 200);
        venda.Problema = Clean(req.Problema, 2000);
        venda.Notas = Clean(req.Notas, 2000);
    }

    private async Task<Dictionary<Guid, CompraLinha>> LoadLotesAsync(
        IReadOnlyList<VendaLinhaWriteRequest> novas, IEnumerable<VendaItem> atuais, CancellationToken ct)
    {
        var ids = novas.Select(l => l.CompraLinhaId).Concat(atuais.Select(i => i.CompraLinhaId))
            .OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return [];
        var lotes = (await _compras.FindLinhasAsync(ids, ct)).ToDictionary(l => l.Id);
        foreach (var pedido in novas.Select(l => l.CompraLinhaId).OfType<Guid>())
            if (!lotes.ContainsKey(pedido)) throw new NotFoundException("CompraLinha", pedido);
        return lotes;
    }

    private static void ApplyLinhas(Venda venda, IReadOnlyList<VendaLinhaWriteRequest> linhas, Dictionary<Guid, CompraLinha> lotes)
    {
        var pedidas = linhas.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToHashSet();
        venda.Items.RemoveAll(i => !pedidas.Contains(i.Id));

        foreach (var l in linhas)
        {
            Validate(l);
            var item = l.Id is { } itemId
                ? venda.Items.FirstOrDefault(i => i.Id == itemId)
                    ?? throw new ValidationException("linha_inexistente", "Linha não pertence a esta venda.")
                : null;
            if (item is null)
            {
                item = new VendaItem { TenantId = venda.TenantId, Descricao = string.Empty };
                venda.Items.Add(item);
            }

            CompraLinha? lote = l.CompraLinhaId is { } loteId ? lotes[loteId] : null;
            if (item.CompraLinhaId != lote?.Id || item.CustoUnitarioPago is null) SnapshotLote(item, lote);
            item.CompraLinhaId = lote?.Id;
            item.CompraLinha = lote;
            item.Descricao = Clean(l.Descricao, 300) ?? lote?.Descricao
                ?? throw new ValidationException("descricao_obrigatoria", "Linha de serviço precisa de descrição.");
            item.Quantidade = l.Quantidade;
            item.PrecoUnitarioCents = l.PrecoUnitarioCents;
            item.DescontoCents = l.DescontoCents;
            item.IvaRate = l.IvaRate ?? TaxaVendaNormalPct;
            item.Imei = NormalizeImei(l.Imei);
        }
    }

    private static void Validate(VendaLinhaWriteRequest l)
    {
        if (l.Quantidade < 1) throw new ValidationException("quantidade_invalida", "Quantidade tem de ser pelo menos 1.");
        if (l.PrecoUnitarioCents < 0) throw new ValidationException("preco_invalido", "Preço não pode ser negativo.");
        if (l.DescontoCents < 0 || l.DescontoCents > l.Quantidade * l.PrecoUnitarioCents)
            throw new ValidationException("desconto_invalido", "Desconto entre 0 e o total da linha.");
        if (l.IvaRate is < 0 or > 100) throw new ValidationException("iva_invalido", "Taxa de IVA entre 0 e 100%.");
    }

    private static string? NormalizeImei(string? imei)
    {
        var clean = Clean(imei, 20);
        if (clean is null) return null;
        if (!ImeiValidator.IsValid(clean))
            throw new ValidationException("imei_invalido", "IMEI inválido — verifica os dígitos.");
        return ImeiValidator.Normalize(clean);
    }

    private static void SnapshotLote(VendaItem item, CompraLinha? lote)
    {
        item.CustoUnitarioPago = lote?.PrecoUnitarioPago;
        item.TaxaIvaCompra = lote?.TaxaIvaCompra;
    }

    private static void ConsumirStock(Venda venda, Dictionary<Guid, CompraLinha> lotes)
    {
        foreach (var grupo in venda.Items.Where(i => i.CompraLinhaId is not null).GroupBy(i => i.CompraLinhaId!.Value))
        {
            var lote = lotes[grupo.Key];
            var qtd = grupo.Sum(i => i.Quantidade);
            if (lote.QuantidadeEmStock < qtd)
                throw new ValidationException("stock_insuficiente",
                    $"\"{lote.Descricao}\": só há {lote.QuantidadeEmStock} em stock (pedido {qtd}).");
            lote.QuantidadeVendida += qtd;
        }
    }

    private static void LibertarStock(Venda venda, Dictionary<Guid, CompraLinha> lotes)
    {
        foreach (var grupo in venda.Items.Where(i => i.CompraLinhaId is not null).GroupBy(i => i.CompraLinhaId!.Value))
        {
            if (!lotes.TryGetValue(grupo.Key, out var lote)) continue;
            lote.QuantidadeVendida = Math.Max(0, lote.QuantidadeVendida - grupo.Sum(i => i.Quantidade));
        }
    }

    private static void MarcarEntregue(Venda venda, PaymentMethod? metodo, Dictionary<Guid, CompraLinha> lotes)
    {
        venda.Data = DateTime.UtcNow;
        if (metodo is { } m) venda.PaymentMethod = m;
        // Snapshot definitivo: o custo que conta é o do momento da venda.
        foreach (var item in venda.Items.Where(i => i.CompraLinhaId is not null))
            if (lotes.TryGetValue(item.CompraLinhaId!.Value, out var lote)) SnapshotLote(item, lote);
    }

    private async Task EmitirGarantiaSeAplicavelAsync(Venda venda, CancellationToken ct)
    {
        // Garantia legal de bens (DL 84/2021) só nas vendas de produtos.
        if (venda.Tipo != VendaTipo.Produto) return;
        var prefs = await _preferences.GetAsync(ct);
        if (prefs.Sales.VendaGarantia == GarantiaAutoMode.Sim)
            await EmitirGarantiaVendaSeNecessarioAsync(venda, venda.Data, ct);
    }

    /// <summary>Contas da linha pelo motor de IVA (SPEC §3.3). Serviço = custo 0 e IVA de compra 0.</summary>
    public static CalculoVenda Calcular(VendaItem i)
        => IvaEngine.VendaPorTotal(i.Quantidade, i.TotalCents / 100m, i.CustoUnitarioPago ?? 0m, i.TaxaIvaCompra ?? 0m, i.IvaRate / 100m);

    private static void RecalculateTotals(Venda venda)
    {
        venda.TotalCents = venda.Items.Sum(i => i.TotalCents);
        venda.IvaCents = venda.Items.Sum(CalculateIvaCents);
    }

    private static int CalculateIvaCents(VendaItem item)
        => (int)Math.Round(Calcular(item).IvaDaVenda * 100m, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Emite garantia automática para a Venda ao ser entregue.
    /// Sprint 127: período é resolvido a partir do <see cref="CondicaoArtigo"/> mais favorável
    /// entre os items (DL 84/2021 — bens móveis consumo). Configurável por tenant.
    /// Idempotente: se já existe, não faz nada.
    /// </summary>
    private async Task EmitirGarantiaVendaSeNecessarioAsync(Venda venda, DateTime agora, CancellationToken ct)
    {
        var existente = await _garantias.FindByVendaAsync(venda.Id, ct);
        if (existente is not null) return;

        var tenant = _tenant.TenantId is { } tid ? await _tenants.FindByIdAsync(tid, ct) : null;
        var dias = ResolveGarantiaDiasFromItems(venda, tenant);
        var condicaoDominante = ResolveCondicaoDominante(venda, tenant);
        var cobertura = tenant?.GarantiaVendaCoberturaDefault
            ?? "Conformidade do bem com o descrito na fatura (DL 84/2021). O comprador tem direito à reposição da conformidade (reparação ou substituição), redução do preço ou resolução do contrato.";
        var exclusoes = tenant?.GarantiaVendaExclusoesDefault
            ?? "Danos por uso indevido, líquidos, quedas, abertura/desmontagem do equipamento, desgaste normal de baterias e acessórios.";

        var g = new Garantia
        {
            VendaId = venda.Id,
            SourceType = GarantiaSourceType.Venda,
            Slug = PublicSlugGenerator.New(),
            DataInicio = agora,
            DataFim = agora.AddDays(dias),
            DiasGarantia = dias,
            Cobertura = cobertura,
            Exclusoes = exclusoes,
            CondicaoUsada = condicaoDominante,
        };
        await _garantias.AddAsync(g, ct);
        await _garantias.SaveAsync(ct);
    }

    /// <summary>
    /// Sprint 128: identifica a CondicaoArtigo que ditou o período da garantia (a do item
    /// com o Max das dias). Vendas sem items devolvem <see cref="CondicaoArtigo.NaoAplicavel"/>.
    /// </summary>
    public static CondicaoArtigo ResolveCondicaoDominante(Venda venda, Tenant? tenant)
    {
        if (venda.Items is null || venda.Items.Count == 0) return CondicaoArtigo.NaoAplicavel;
        var novo = tenant?.GarantiaVendaDiasDefault ?? 1095;
        var openBox = tenant?.GarantiaVendaOpenBoxDias ?? 730;
        var recond = tenant?.GarantiaVendaRecondicionadoDias ?? 540;
        var usado = tenant?.GarantiaVendaUsadoDias ?? 540;
        return venda.Items
            .Select(it => (cond: it.Condicao, dias: DiasParaCondicao(it.Condicao, novo, openBox, recond, usado)))
            .OrderByDescending(t => t.dias)
            .First().cond;
    }

    /// <summary>
    /// Sprint 127: período de garantia de uma Venda em função das condições dos items — o MAIOR
    /// período entre as condições presentes (favorável ao consumidor, DL 84/2021).
    /// </summary>
    public static int ResolveGarantiaDiasFromItems(Venda venda, Tenant? tenant)
    {
        var defaultDias = tenant?.GarantiaVendaDiasDefault ?? 1095;
        var openBox = tenant?.GarantiaVendaOpenBoxDias ?? 730;
        var recondicionado = tenant?.GarantiaVendaRecondicionadoDias ?? 540;
        var usado = tenant?.GarantiaVendaUsadoDias ?? 540;

        var items = venda.Items;
        if (items is null || items.Count == 0) return defaultDias;
        return items.Max(it => DiasParaCondicao(it.Condicao, defaultDias, openBox, recondicionado, usado));
    }

    public static int DiasParaCondicao(CondicaoArtigo condicao, int novoDias, int openBoxDias, int recondicionadoDias, int usadoDias) => condicao switch
    {
        CondicaoArtigo.Novo => novoDias,
        CondicaoArtigo.OpenBox => openBoxDias,
        CondicaoArtigo.Recondicionado => recondicionadoDias,
        CondicaoArtigo.Usado => usadoDias,
        _ => novoDias, // NaoAplicavel — assume novo, é o caso mais comum (acessórios, peças)
    };

    public async Task<byte[]> ExportCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var (rows, _) = await _vendas.SearchAsync(new VendaFiltro(fromUtc, toUtc, Estado: VendaEstado.Entregue), 1, 5000, ct);
        var csv = new CsvBuilder();
        csv.Row("numero", "data", "tipo", "cliente_nome", "cliente_nif", "total_eur", "iva_eur", "iva_a_pagar_eur",
            "lucro_eur", "metodo_pagamento", "fatura_numero", "fatura_data", "notas");
        foreach (var v in rows)
        {
            var calc = v.Items.Select(Calcular).ToList();
            csv.Row(
                v.Numero,
                v.Data.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                v.Tipo.ToString(),
                v.Cliente?.Nome ?? "",
                v.Cliente?.Nif ?? "",
                (v.TotalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                IvaEngine.Euros(calc.Sum(c => c.IvaDaVenda)).ToString("0.00", CultureInfo.InvariantCulture),
                IvaEngine.Euros(calc.Sum(c => c.IvaAPagarEstado)).ToString("0.00", CultureInfo.InvariantCulture),
                IvaEngine.Euros(calc.Sum(c => c.Lucro)).ToString("0.00", CultureInfo.InvariantCulture),
                v.PaymentMethod.ToString(),
                v.InvoiceNumber ?? "",
                v.InvoiceEmittedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                v.Notas ?? "");
        }
        return csv.ToUtf8WithBom();
    }

    private static VendaDto ToDto(Venda venda)
    {
        var cliente = venda.Cliente is null || venda.ClienteId is null
            ? null
            : new VendaClienteResumo(venda.Cliente.Id, venda.Cliente.Nome, venda.Cliente.Telefone ?? string.Empty);
        var items = venda.Items.OrderBy(i => i.CreatedAt).Select(i =>
        {
            var c = Calcular(i);
            return new VendaItemDto(i.Id, i.CompraLinhaId, i.Descricao, i.Quantidade, i.PrecoUnitarioCents, i.DescontoCents,
                i.IvaRate, i.TotalCents, CalculateIvaCents(i), i.Imei, i.CustoUnitarioPago, i.TaxaIvaCompra,
                c.CustoDasPecas, c.IvaAPagarEstado, c.Lucro);
        }).ToList();

        return new VendaDto(
            venda.Id, venda.Numero, venda.Tipo, venda.Estado, venda.Data, venda.CreatedAt, cliente,
            venda.Equipamento, venda.Problema, venda.TotalCents, venda.IvaCents,
            items.Sum(i => i.IvaAPagarEstado), items.Sum(i => i.Lucro),
            venda.PaymentMethod, venda.InvoiceNumber, venda.InvoiceEmittedAt,
            venda.Estado == VendaEstado.Entregue && venda.InvoiceNumber is null,
            venda.Notas, items);
    }

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
