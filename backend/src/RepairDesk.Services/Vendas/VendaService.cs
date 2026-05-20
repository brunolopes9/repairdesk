using System.Globalization;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Webhooks;

namespace RepairDesk.Services.Vendas;

public interface IVendaService
{
    Task<PagedResult<VendaDto>> SearchAsync(DateTime? fromUtc, DateTime? toUtc, Guid? clienteId, int page, int pageSize, CancellationToken ct = default);
    Task<VendaImeiLookupDto?> ImeiLookupAsync(string imei, CancellationToken ct = default);
    Task<IReadOnlyList<VendaReparacaoRelacionadaDto>> GetReparacoesRelacionadasAsync(Guid vendaId, CancellationToken ct = default);
    /// <summary>Para autocomplete UI Vendas — lista de fornecedores que já apareceram em vendas anteriores.</summary>
    Task<IReadOnlyList<string>> ListFornecedoresAsync(CancellationToken ct = default);
    Task<VendaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> CreateAsync(CreateVendaRequest req, CancellationToken ct = default);
    Task<EmitVendaFaturaResponse> MarcarPagaAsync(Guid id, MarcarVendaPagaRequest req, CancellationToken ct = default);
    Task<InvoiceDto> EmitirFaturaAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> CancelarAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> AnularFaturaAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> LimparReferenciaFaturaAsync(Guid id, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

public class VendaService : IVendaService
{
    private readonly IVendaRepository _vendas;
    private readonly IPartRepository _parts;
    private readonly IClienteRepository _clientes;
    private readonly ITenantContext _tenant;
    private readonly ITenantBillingSettingsRepository _billingSettings;
    private readonly IBillingProvider _billing;
    private readonly IMoloniClient _moloni;
    private readonly IInvoiceXpressClient _invoiceXpress;
    private readonly IGarantiaRepository _garantias;
    private readonly ITenantRepository _tenants;
    private readonly IReparacaoRepository _reparacoes;
    private readonly IWebhookPublisher _webhooks;

    public VendaService(
        IVendaRepository vendas,
        IPartRepository parts,
        IClienteRepository clientes,
        ITenantContext tenant,
        ITenantBillingSettingsRepository billingSettings,
        IBillingProvider billing,
        IMoloniClient moloni,
        IInvoiceXpressClient invoiceXpress,
        IGarantiaRepository garantias,
        ITenantRepository tenants,
        IReparacaoRepository reparacoes,
        IWebhookPublisher webhooks)
    {
        _vendas = vendas;
        _parts = parts;
        _clientes = clientes;
        _tenant = tenant;
        _billingSettings = billingSettings;
        _billing = billing;
        _moloni = moloni;
        _invoiceXpress = invoiceXpress;
        _garantias = garantias;
        _tenants = tenants;
        _reparacoes = reparacoes;
        _webhooks = webhooks;
    }

    public async Task<PagedResult<VendaDto>> SearchAsync(DateTime? fromUtc, DateTime? toUtc, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _vendas.SearchAsync(fromUtc, toUtc, clienteId, page, pageSize, ct);
        return new PagedResult<VendaDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public Task<IReadOnlyList<string>> ListFornecedoresAsync(CancellationToken ct = default)
        => _vendas.ListDistinctFornecedoresAsync(ct);

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
        var imeis = venda.Items
            .SelectMany(i => new[] { i.Imei, i.Imei2 })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct()
            .ToList();
        if (imeis.Count == 0) return Array.Empty<VendaReparacaoRelacionadaDto>();

        // Para cada IMEI da venda, lookup reparações pós-data da venda.
        var resultado = new List<VendaReparacaoRelacionadaDto>();
        foreach (var imei in imeis)
        {
            var reparacoes = await _reparacoes.SearchByImeiAsync(imei, excludeId: null, ct);
            foreach (var r in reparacoes.Where(r => r.CreatedAt > venda.Data))
            {
                resultado.Add(new VendaReparacaoRelacionadaDto(
                    r.Id, r.Numero, r.CreatedAt, r.Equipamento, r.Imei ?? imei,
                    (int)r.Estado,
                    (int)Math.Round((r.CreatedAt - venda.Data).TotalDays),
                    r.OrcamentoCents));
            }
        }
        return resultado.OrderByDescending(r => r.RecebidoEm).ToList();
    }

    public async Task<VendaDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        return ToDto(venda);
    }

    public async Task<VendaDto> CreateAsync(CreateVendaRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        if (req.Items.Count == 0)
            throw new ValidationException("venda_sem_items", "Adiciona pelo menos uma linha ao carrinho.");

        Cliente? cliente = null;
        if (req.ClienteId is not null)
            cliente = await _clientes.FindByIdAsync(req.ClienteId.Value, ct)
                ?? throw new NotFoundException("Cliente", req.ClienteId.Value);

        var venda = new Venda
        {
            ClienteId = cliente?.Id,
            Cliente = cliente,
            Data = DateTime.UtcNow,
            Status = VendaStatus.Pendente,
            Notas = Clean(req.Notas),
            Origem = req.Origem ?? VendaOrigem.Balcao,
        };

        foreach (var itemReq in req.Items)
        {
            ValidateItemRequest(itemReq);
            Part? part = null;

            if (itemReq.PartId is not null)
            {
                part = await _parts.FindByIdAsync(itemReq.PartId.Value, ct)
                    ?? throw new NotFoundException("Part", itemReq.PartId.Value);
                EnsurePartSellable(part, itemReq.Quantidade);
            }

            // IMEI obrigatorio para Smartphone/Tablet (Molano dropshipping refurbished).
            var requiresImei = part?.Categoria is PartCategoria.Smartphone or PartCategoria.Tablet;
            var imei = Clean(itemReq.Imei);
            var imei2 = Clean(itemReq.Imei2);
            if (requiresImei && string.IsNullOrEmpty(imei))
                throw new ValidationException("imei_obrigatorio",
                    $"IMEI obrigatorio para {part?.Nome ?? "Smartphone/Tablet"}.");
            if (!string.IsNullOrEmpty(imei) && !ImeiValidator.IsValid(imei))
                throw new ValidationException("imei_invalido",
                    "IMEI invalido — verifica os digitos (Luhn check falhou).");
            if (!string.IsNullOrEmpty(imei2) && !ImeiValidator.IsValid(imei2))
                throw new ValidationException("imei2_invalido",
                    "IMEI secundario invalido — verifica os digitos.");

            venda.Items.Add(new VendaItem
            {
                PartId = part?.Id,
                Part = part,
                Descricao = Clean(itemReq.Descricao) ?? part?.Nome ?? "Artigo",
                Quantidade = itemReq.Quantidade,
                PrecoUnitarioCents = itemReq.PrecoUnitarioCents,
                DescontoCents = itemReq.DescontoCents,
                IvaRate = itemReq.IvaRate,
                Imei = imei is null ? null : ImeiValidator.Normalize(imei),
                Imei2 = imei2 is null ? null : ImeiValidator.Normalize(imei2),
                FornecedorNome = Clean(itemReq.FornecedorNome),
                Condicao = itemReq.Condicao ?? CondicaoArtigo.NaoAplicavel,
                GarantiaFornecedorAteAo = itemReq.GarantiaFornecedorAteAo,
            });
        }

        RecalculateTotals(venda);
        await _vendas.CreateWithNextNumeroAsync(venda, tenantId, ct);

        await _webhooks.PublishAsync(tenantId, WebhookEvents.VendaCriada, new
        {
            vendaId = venda.Id,
            vendaNumero = venda.Numero,
            clienteId = venda.ClienteId,
            origem = venda.Origem.ToString(),
            totalCents = venda.TotalCents,
            status = venda.Status.ToString(),
        }, ct);

        return ToDto(venda);
    }

    public async Task<EmitVendaFaturaResponse> MarcarPagaAsync(Guid id, MarcarVendaPagaRequest req, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (venda.Status == VendaStatus.Cancelada)
            throw new ConflictException("venda_cancelada", "Venda cancelada nao pode ser marcada como paga.");

        if (venda.Status != VendaStatus.Paga)
        {
            foreach (var item in venda.Items.Where(i => i.PartId is not null))
            {
                var part = await _parts.FindByIdAsync(item.PartId!.Value, ct)
                    ?? throw new NotFoundException("Part", item.PartId.Value);
                EnsurePartSellable(part, item.Quantidade);

                var before = part.QtdStock;
                var after = before - item.Quantidade;
                part.QtdStock = after;
                _parts.AddMovimento(new PartMovimento
                {
                    PartId = part.Id,
                    Quantidade = -item.Quantidade,
                    StockAntes = before,
                    StockDepois = after,
                    Motivo = PartMovimentoMotivo.VendaCliente,
                    VendaId = venda.Id,
                    Notas = $"Venda #{venda.Numero:D5}",
                });
            }

            venda.Status = VendaStatus.Paga;
            venda.PaymentMethod = req.PaymentMethod;
            venda.Data = DateTime.UtcNow;
            RecalculateTotals(venda);
            await _vendas.SaveAsync(ct);

            if (_tenant.TenantId is { } publishTenantId)
            {
                await _webhooks.PublishAsync(publishTenantId, WebhookEvents.VendaPaga, new
                {
                    vendaId = venda.Id,
                    vendaNumero = venda.Numero,
                    clienteId = venda.ClienteId,
                    totalCents = venda.TotalCents,
                    paymentMethod = venda.PaymentMethod.ToString(),
                    data = venda.Data,
                }, ct);
            }

            // DL 84/2021: emite garantia digital automática (3 anos default para consumo).
            await EmitirGarantiaVendaSeNecessarioAsync(venda, venda.Data, ct);
        }

        InvoiceDto? invoice = null;
        if (req.EmitirFatura && await HasBillingProviderAsync(ct))
            invoice = await EmitirFaturaAsync(venda.Id, ct);

        venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? venda;
        return new EmitVendaFaturaResponse(ToDto(venda), invoice);
    }

    /// <summary>
    /// Emite garantia automática para a Venda ao marcar paga.
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
        };
        await _garantias.AddAsync(g, ct);
        await _garantias.SaveAsync(ct);

        if (_tenant.TenantId is { } publishTenantId)
        {
            await _webhooks.PublishAsync(publishTenantId, WebhookEvents.GarantiaEmitida, new
            {
                garantiaId = g.Id,
                slug = g.Slug,
                origem = "Venda",
                vendaId = venda.Id,
                vendaNumero = venda.Numero,
                clienteId = venda.ClienteId,
                dataInicio = g.DataInicio,
                dataFim = g.DataFim,
                diasGarantia = g.DiasGarantia,
            }, ct);
        }
    }

    /// <summary>
    /// Sprint 127: resolve o período de garantia para uma Venda em função das condições dos items.
    /// Aplica o MAIOR período entre as condições presentes — favorável ao consumidor e sempre
    /// conforme com DL 84/2021 (excede o mínimo legal). Items sem condição (NaoAplicavel) ou
    /// vendas sem items usam o default do tenant (campo Novo).
    /// </summary>
    public static int ResolveGarantiaDiasFromItems(Venda venda, Tenant? tenant)
    {
        var defaultDias = tenant?.GarantiaVendaDiasDefault ?? 1095;
        var openBox = tenant?.GarantiaVendaOpenBoxDias ?? 730;
        var recondicionado = tenant?.GarantiaVendaRecondicionadoDias ?? 540;
        var usado = tenant?.GarantiaVendaUsadoDias ?? 540;

        var items = venda.Items;
        if (items is null || items.Count == 0) return defaultDias;

        var diasPorItem = items
            .Select(it => DiasParaCondicao(it.Condicao, defaultDias, openBox, recondicionado, usado))
            .ToList();
        return diasPorItem.Count == 0 ? defaultDias : diasPorItem.Max();
    }

    public static int DiasParaCondicao(CondicaoArtigo condicao, int novoDias, int openBoxDias, int recondicionadoDias, int usadoDias) => condicao switch
    {
        CondicaoArtigo.Novo => novoDias,
        CondicaoArtigo.OpenBox => openBoxDias,
        CondicaoArtigo.Recondicionado => recondicionadoDias,
        CondicaoArtigo.Usado => usadoDias,
        _ => novoDias, // NaoAplicavel — assume novo, é o caso mais comum (acessórios, peças)
    };

    public async Task<InvoiceDto> EmitirFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (venda.InvoiceExternalId is not null)
            return new InvoiceDto(venda.InvoiceNumber ?? "Fatura emitida", venda.InvoicePdfUrl, venda.InvoiceEmittedAt ?? DateTime.UtcNow);
        if (venda.Status != VendaStatus.Paga)
            throw new ValidationException("venda_nao_paga", "So podes emitir fatura depois de marcar a venda como paga.");

        return await _billing.EmitVendaInvoiceAsync(id, ct);
    }

    /// <summary>Limpa apenas referencias locais da fatura. Util quando o utilizador ja anulou a
    /// fatura manualmente no painel Moloni (status 'Anulado') e so quer sincronizar com o RepairDesk.</summary>
    public async Task<byte[]> ExportCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        // SearchAsync com pageSize generoso. 1000 vendas chega para vários meses.
        var (rows, _) = await _vendas.SearchAsync(fromUtc, toUtc, null, 1, 1000, ct);

        var csv = new CsvBuilder();
        csv.Row(
            "numero", "data", "cliente_nome", "cliente_nif",
            "total_eur", "iva_eur", "metodo_pagamento", "status",
            "invoice_provider", "invoice_numero", "invoice_emitida_em",
            "notas");

        foreach (var v in rows)
        {
            // Calcular IVA total da venda a partir dos items
            var totalCents = v.Items.Sum(i => Math.Max(0, i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents));
            var ivaCents = v.Items.Sum(i =>
            {
                if (i.IvaRate <= 0) return 0;
                var gross = Math.Max(0, i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents);
                return (int)Math.Round(gross - gross / (1m + i.IvaRate / 100m));
            });

            var statusLabel = v.Status switch
            {
                VendaStatus.Pendente => "Pendente",
                VendaStatus.Paga => "Paga",
                VendaStatus.Cancelada => "Cancelada",
                _ => v.Status.ToString(),
            };
            var paymentLabel = v.PaymentMethod switch
            {
                PaymentMethod.Dinheiro => "Numerario",
                PaymentMethod.Multibanco => "Multibanco",
                PaymentMethod.MBWay => "MBWay",
                PaymentMethod.TransferenciaBancaria => "Transferencia",
                PaymentMethod.Cartao => "Cartao",
                _ => "Outro",
            };

            csv.Row(
                v.Numero,
                v.Data.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                v.Cliente?.Nome ?? "",
                v.Cliente?.Nif ?? "",
                (totalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                (ivaCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                paymentLabel,
                statusLabel,
                v.InvoiceProvider switch
                {
                    BillingProvider.Moloni => "Moloni",
                    BillingProvider.InvoiceXpress => "InvoiceXpress",
                    _ => "",
                },
                v.InvoiceNumber ?? "",
                v.InvoiceEmittedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                v.Notas ?? "");
        }

        return csv.ToUtf8WithBom();
    }

    public async Task<VendaDto> LimparReferenciaFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (string.IsNullOrEmpty(venda.InvoiceExternalId))
            throw new ConflictException("venda_sem_fatura", "Esta venda nao tem fatura para limpar.");

        venda.InvoiceProvider = BillingProvider.None;
        venda.InvoiceExternalId = null;
        venda.InvoiceNumber = null;
        venda.InvoicePdfUrl = null;
        venda.InvoiceEmittedAt = null;
        await _vendas.SaveAsync(ct);
        return ToDto(venda);
    }

    public async Task<VendaDto> AnularFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (string.IsNullOrEmpty(venda.InvoiceExternalId))
            throw new ConflictException("venda_sem_fatura", "Esta venda nao tem fatura emitida para anular.");

        // Estratégia: tentar documentCancel primeiro (1 documento, mais limpo). Se Moloni rejeitar
        // (porque ja foi processado pela AT, etc), fallback para Nota de Credito.
        if (_tenant.TenantId is { } tenantId)
        {
            var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
            if (settings?.Provider == BillingProvider.Moloni && int.TryParse(venda.InvoiceExternalId, out var originalDocId))
            {
                var cancelled = await _moloni.CancelDocumentAsync(
                    settings,
                    originalDocId,
                    $"Anulado via RepairDesk — venda #{venda.Numero}",
                    ct);

                if (!cancelled)
                {
                    // Fallback: emite Nota de Credito (caso documentCancel nao seja aplicavel)
                    var items = venda.Items.Select(i => new MoloniInvoiceDraftItem(
                        i.Descricao,
                        null,
                        i.Quantidade,
                        i.PrecoUnitarioCents,
                        i.DescontoCents,
                        i.IvaRate)).ToList();

                    var customerId = settings.FallbackCustomerId ?? 0;
                    if (customerId <= 0)
                        throw new ValidationException("moloni_customer_fallback_missing", "Cliente fallback Moloni nao configurado.");

                    await _moloni.InsertCreditNoteAsync(settings, new MoloniCreditNoteDraft(
                        originalDocId,
                        customerId,
                        $"Venda #{venda.Numero}",
                        items,
                        $"Anulacao da Fatura {venda.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }
            else if (settings?.Provider == BillingProvider.InvoiceXpress && venda.InvoiceProvider == BillingProvider.InvoiceXpress)
            {
                var reason = $"Anulado via RepairDesk - venda #{venda.Numero}";
                var cancelled = await _invoiceXpress.CancelDocumentAsync(settings, venda.InvoiceExternalId, reason, ct);

                if (!cancelled)
                {
                    var items = venda.Items.Select(i => new InvoiceXpressInvoiceDraftItem(
                        i.Descricao,
                        null,
                        i.Quantidade,
                        i.PrecoUnitarioCents,
                        i.DescontoCents,
                        i.IvaRate)).ToList();

                    await _invoiceXpress.InsertCreditNoteAsync(settings, new InvoiceXpressCreditNoteDraft(
                        venda.InvoiceExternalId,
                        new InvoiceXpressClientDraft(
                            string.IsNullOrWhiteSpace(venda.Cliente?.Nome) ? "Consumidor Final" : venda.Cliente.Nome,
                            venda.Cliente?.Email,
                            venda.Cliente?.Nif,
                            venda.Cliente?.Telefone),
                        $"Venda #{venda.Numero}",
                        items,
                        $"Anulacao da Fatura {venda.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }
        }

        // Limpa referencias locais para a venda sair do Relatorio IVA do RepairDesk
        venda.InvoiceProvider = BillingProvider.None;
        venda.InvoiceExternalId = null;
        venda.InvoiceNumber = null;
        venda.InvoicePdfUrl = null;
        venda.InvoiceEmittedAt = null;

        await _vendas.SaveAsync(ct);
        return ToDto(venda);
    }

    public async Task<VendaDto> CancelarAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (venda.Status == VendaStatus.Cancelada) return ToDto(venda);

        // Se ja tem fatura emitida no Moloni, anula primeiro (documentCancel ou NC).
        // RepairDesk eh ponto central: 1 clique cancela tudo.
        if (venda.InvoiceExternalId is not null && _tenant.TenantId is { } tenantId)
        {
            var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
            if (settings?.Provider == BillingProvider.Moloni && int.TryParse(venda.InvoiceExternalId, out var originalDocId))
            {
                var cancelled = await _moloni.CancelDocumentAsync(
                    settings,
                    originalDocId,
                    $"Cancelado via RepairDesk — venda #{venda.Numero}",
                    ct);

                if (!cancelled)
                {
                    // Fallback: emite Nota de Credito
                    var items = venda.Items.Select(i => new MoloniInvoiceDraftItem(
                        i.Descricao, null, i.Quantidade, i.PrecoUnitarioCents, i.DescontoCents, i.IvaRate)).ToList();
                    var customerId = settings.FallbackCustomerId ?? 0;
                    if (customerId > 0)
                    {
                        await _moloni.InsertCreditNoteAsync(settings, new MoloniCreditNoteDraft(
                            originalDocId, customerId, $"Venda #{venda.Numero}", items,
                            $"Cancelamento da Fatura {venda.InvoiceNumber} via RepairDesk"), ct);
                    }
                }
            }
            else if (settings?.Provider == BillingProvider.InvoiceXpress && venda.InvoiceProvider == BillingProvider.InvoiceXpress)
            {
                var reason = $"Cancelado via RepairDesk - venda #{venda.Numero}";
                var cancelled = await _invoiceXpress.CancelDocumentAsync(settings, venda.InvoiceExternalId, reason, ct);

                if (!cancelled)
                {
                    var items = venda.Items.Select(i => new InvoiceXpressInvoiceDraftItem(
                        i.Descricao,
                        null,
                        i.Quantidade,
                        i.PrecoUnitarioCents,
                        i.DescontoCents,
                        i.IvaRate)).ToList();

                    await _invoiceXpress.InsertCreditNoteAsync(settings, new InvoiceXpressCreditNoteDraft(
                        venda.InvoiceExternalId,
                        new InvoiceXpressClientDraft(
                            string.IsNullOrWhiteSpace(venda.Cliente?.Nome) ? "Consumidor Final" : venda.Cliente.Nome,
                            venda.Cliente?.Email,
                            venda.Cliente?.Nif,
                            venda.Cliente?.Telefone),
                        $"Venda #{venda.Numero}",
                        items,
                        $"Cancelamento da Fatura {venda.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }

            // Limpa Invoice* locais (ja anulada/NC emitida no Moloni)
            venda.InvoiceProvider = BillingProvider.None;
            venda.InvoiceExternalId = null;
            venda.InvoiceNumber = null;
            venda.InvoicePdfUrl = null;
            venda.InvoiceEmittedAt = null;
        }

        if (venda.Status == VendaStatus.Paga)
        {
            foreach (var item in venda.Items.Where(i => i.PartId is not null))
            {
                var part = await _parts.FindByIdAsync(item.PartId!.Value, ct)
                    ?? throw new NotFoundException("Part", item.PartId.Value);
                var before = part.QtdStock;
                var after = before + item.Quantidade;
                part.QtdStock = after;
                _parts.AddMovimento(new PartMovimento
                {
                    PartId = part.Id,
                    Quantidade = item.Quantidade,
                    StockAntes = before,
                    StockDepois = after,
                    Motivo = PartMovimentoMotivo.Devolucao,
                    VendaId = venda.Id,
                    Notas = $"Cancelamento venda #{venda.Numero:D5}",
                });
            }
        }

        venda.Status = VendaStatus.Cancelada;
        await _vendas.SaveAsync(ct);

        if (_tenant.TenantId is { } publishTenantId)
        {
            await _webhooks.PublishAsync(publishTenantId, WebhookEvents.VendaCancelada, new
            {
                vendaId = venda.Id,
                vendaNumero = venda.Numero,
                clienteId = venda.ClienteId,
                totalCents = venda.TotalCents,
                invoiceNumber = venda.InvoiceNumber,
            }, ct);
        }

        return ToDto(venda);
    }

    private async Task<bool> HasBillingProviderAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return false;
        var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
        return settings?.Provider is BillingProvider.Moloni or BillingProvider.InvoiceXpress;
    }

    private static void ValidateItemRequest(CreateVendaItemRequest item)
    {
        if (item.Quantidade <= 0)
            throw new ValidationException("quantidade_invalida", "Quantidade deve ser superior a zero.");
        if (item.PrecoUnitarioCents < 0)
            throw new ValidationException("preco_invalido", "Preco unitario nao pode ser negativo.");
        if (item.DescontoCents < 0)
            throw new ValidationException("desconto_invalido", "Desconto nao pode ser negativo.");
        if (item.DescontoCents > item.Quantidade * item.PrecoUnitarioCents)
            throw new ValidationException("desconto_excessivo", "Desconto nao pode exceder o total da linha.");
        if (item.IvaRate < 0 || item.IvaRate > 100)
            throw new ValidationException("iva_invalido", "IVA deve estar entre 0 e 100.");
        if (item.PartId is null && string.IsNullOrWhiteSpace(item.Descricao))
            throw new ValidationException("descricao_obrigatoria", "Linha sem stock precisa de descricao.");
    }

    private static void EnsurePartSellable(Part part, int quantity)
    {
        if (!part.Activo)
            throw new ConflictException("part_inactive", "Peca inactiva nao pode ser vendida.");
        if (part.QtdStock <= 0)
            throw new ValidationException("stock_zero", $"{part.Nome} esta sem stock.");
        if (part.QtdStock < quantity)
            throw new ValidationException("stock_insuficiente", $"{part.Nome} so tem {part.QtdStock} unidade(s) em stock.");
    }

    private static void RecalculateTotals(Venda venda)
    {
        venda.TotalCents = venda.Items.Sum(i => i.TotalCents);
        venda.IvaCents = venda.Items.Sum(CalculateIvaCents);
    }

    private static int CalculateIvaCents(VendaItem item)
    {
        if (item.IvaRate <= 0) return 0;
        var total = item.TotalCents;
        var net = total / (1 + item.IvaRate / 100m);
        return (int)Math.Round(total - net, MidpointRounding.AwayFromZero);
    }

    private static VendaDto ToDto(Venda venda)
    {
        var cliente = venda.Cliente is null || venda.ClienteId is null
            ? null
            : new VendaClienteResumo(venda.Cliente.Id, venda.Cliente.Nome, venda.Cliente.Telefone ?? string.Empty);

        return new VendaDto(
            venda.Id,
            venda.Numero,
            venda.Data,
            cliente,
            venda.TotalCents,
            venda.IvaCents,
            venda.PaymentMethod,
            venda.Status,
            venda.InvoiceProvider,
            venda.InvoiceExternalId,
            venda.InvoicePdfUrl,
            venda.InvoiceNumber,
            venda.InvoiceEmittedAt,
            venda.Notas,
            venda.Items.Select(i => new VendaItemDto(
                i.Id,
                i.PartId,
                i.Part?.Sku,
                i.Descricao,
                i.Quantidade,
                i.PrecoUnitarioCents,
                i.DescontoCents,
                i.IvaRate,
                i.TotalCents,
                CalculateIvaCents(i),
                i.Imei,
                i.Imei2,
                i.FornecedorNome,
                i.Condicao,
                i.GarantiaFornecedorAteAo)).ToList(),
            venda.Origem);
    }

    private static InvoiceXpressClientDraft ToInvoiceXpressClientDraft(Cliente? cliente)
        => new(
            string.IsNullOrWhiteSpace(cliente?.Nome) ? "Consumidor Final" : cliente.Nome,
            cliente?.Email,
            cliente?.Nif,
            cliente?.Telefone);

    private static string? Clean(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
