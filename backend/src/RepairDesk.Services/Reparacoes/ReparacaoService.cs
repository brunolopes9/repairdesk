using FluentValidation;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.EquipmentFields;
using RepairDesk.Services.Push;
using RepairDesk.Services.Webhooks;
// ImeiValidator do Common.Helpers

namespace RepairDesk.Services.Reparacoes;

public interface IReparacaoService
{
    Task<PagedResult<ReparacaoDto>> SearchAsync(string? query, RepairStatus? estado, Guid? clienteId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<ReparacaoDto>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default);
    Task<ReparacaoDto> AnularFaturaAsync(Guid id, CancellationToken ct = default);
    Task<ReparacaoDto> EmitirOrcamentoMoloniAsync(Guid id, CancellationToken ct = default);
    Task<ReparacaoDto> ConverterOrcamentoEmFaturaAsync(Guid id, CancellationToken ct = default);
    Task<ReparacaoDetalhadaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ReparacaoDto> CreateAsync(CreateReparacaoRequest req, CancellationToken ct = default);
    Task<ReparacaoDto> UpdateAsync(Guid id, UpdateReparacaoRequest req, CancellationToken ct = default);
    Task<ReparacaoDto> ChangeEstadoAsync(Guid id, ChangeEstadoRequest req, CancellationToken ct = default);
    Task<ReparacaoDto> ReabrirAsync(Guid id, string? notas, CancellationToken ct = default);
    Task<IReadOnlyList<EquipmentFieldValueDto>> SetFieldsAsync(Guid id, SetEquipmentFieldValuesRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ReparacaoHistoricoResponse> HistoricoPorImeiAsync(string imei, Guid? excludeId, CancellationToken ct = default);
    Task<ImportReparacoesResponse> ImportCsvAsync(string csv, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
}

public class ReparacaoService : IReparacaoService
{
    private readonly IReparacaoRepository _repo;
    private readonly IClienteRepository _clientes;
    private readonly IDespesaRepository _despesas;
    private readonly IGarantiaRepository _garantias;
    private readonly IVendaRepository _vendas;
    private readonly ITenantRepository _tenants;
    private readonly IEquipmentFieldService _equipmentFields;
    private readonly IPushNotificationQueue _pushQueue;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly ITenantBillingSettingsRepository _billingSettings;
    private readonly IMoloniClient _moloni;
    private readonly IInvoiceXpressClient _invoiceXpress;
    private readonly IAuditLogger _audit;
    private readonly IWebhookPublisher _webhooks;
    private readonly IPartRepository _parts;
    private readonly IValidator<CreateReparacaoRequest> _createV;
    private readonly IValidator<UpdateReparacaoRequest> _updateV;
    private readonly IValidator<ChangeEstadoRequest> _estadoV;

    public ReparacaoService(
        IReparacaoRepository repo,
        IClienteRepository clientes,
        IDespesaRepository despesas,
        IGarantiaRepository garantias,
        IVendaRepository vendas,
        ITenantRepository tenants,
        IEquipmentFieldService equipmentFields,
        IPushNotificationQueue pushQueue,
        ITenantContext tenant,
        ICurrentUser user,
        ITenantBillingSettingsRepository billingSettings,
        IMoloniClient moloni,
        IInvoiceXpressClient invoiceXpress,
        IAuditLogger audit,
        IWebhookPublisher webhooks,
        IPartRepository parts,
        IValidator<CreateReparacaoRequest> createV,
        IValidator<UpdateReparacaoRequest> updateV,
        IValidator<ChangeEstadoRequest> estadoV)
    {
        _repo = repo;
        _clientes = clientes;
        _despesas = despesas;
        _garantias = garantias;
        _vendas = vendas;
        _tenants = tenants;
        _equipmentFields = equipmentFields;
        _pushQueue = pushQueue;
        _tenant = tenant;
        _user = user;
        _billingSettings = billingSettings;
        _moloni = moloni;
        _invoiceXpress = invoiceXpress;
        _audit = audit;
        _webhooks = webhooks;
        _parts = parts;
        _createV = createV;
        _updateV = updateV;
        _estadoV = estadoV;
    }

    public async Task<PagedResult<ReparacaoDto>> SearchAsync(
        string? query, RepairStatus? estado, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(query, estado, clienteId, page, pageSize, ct);
        var dtos = new List<ReparacaoDto>(items.Count);
        foreach (var r in items)
        {
            var custo = await _despesas.SumByReparacaoAsync(r.Id, ct);
            dtos.Add(ToDto(r, custo));
        }
        return new PagedResult<ReparacaoDto>(dtos, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ReparacaoDto>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default)
    {
        var items = await _repo.ListPagasSemFaturaAsync(limit, ct);
        var dtos = new List<ReparacaoDto>(items.Count);
        foreach (var r in items)
        {
            var custo = await _despesas.SumByReparacaoAsync(r.Id, ct);
            dtos.Add(ToDto(r, custo));
        }
        return dtos;
    }

    public async Task<ReparacaoDto> AnularFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var rep = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);
        if (string.IsNullOrEmpty(rep.InvoiceExternalId))
            throw new ConflictException("reparacao_sem_fatura", "Esta reparacao nao tem fatura emitida para anular.");

        // Estrategia: tentar documentCancel primeiro (1 doc anulado, sem criar NC).
        // Se Moloni rejeitar (ja processado pela AT, etc), fallback para Nota de Credito.
        if (_tenant.TenantId is { } tenantId)
        {
            var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
            if (settings?.Provider == BillingProvider.Moloni && int.TryParse(rep.InvoiceExternalId, out var originalDocId))
            {
                var cancelled = await _moloni.CancelDocumentAsync(
                    settings,
                    originalDocId,
                    $"Anulado via RepairDesk — reparacao #{rep.Numero}",
                    ct);

                if (!cancelled)
                {
                    var valor = rep.PrecoFinalCents ?? rep.OrcamentoCents ?? 0;
                    var items = new List<RepairDesk.Services.Billing.MoloniInvoiceDraftItem>
                    {
                        new($"Reparacao {rep.Equipamento}", rep.Avaria, 1, valor, 0, 23m),
                    };
                    var customerId = settings.FallbackCustomerId ?? 0;
                    if (customerId <= 0)
                        throw new RepairDesk.Core.Exceptions.ValidationException("moloni_customer_fallback_missing", "Cliente fallback Moloni nao configurado.");

                    await _moloni.InsertCreditNoteAsync(settings, new RepairDesk.Services.Billing.MoloniCreditNoteDraft(
                        originalDocId,
                        customerId,
                        $"Reparacao #{rep.Numero}",
                        items,
                        $"Anulacao da Fatura {rep.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }
            else if (settings?.Provider == BillingProvider.InvoiceXpress && rep.InvoiceProvider == BillingProvider.InvoiceXpress)
            {
                rep.Cliente ??= await _clientes.FindByIdAsync(rep.ClienteId, ct);
                var reason = $"Anulado via RepairDesk - reparacao #{rep.Numero}";
                var cancelled = await _invoiceXpress.CancelDocumentAsync(settings, rep.InvoiceExternalId, reason, ct);

                if (!cancelled)
                {
                    var valor = rep.PrecoFinalCents ?? rep.OrcamentoCents ?? 0;
                    var items = new List<InvoiceXpressInvoiceDraftItem>
                    {
                        new($"Reparacao {rep.Equipamento}", rep.Avaria, 1, valor, 0, 23m),
                    };

                    await _invoiceXpress.InsertCreditNoteAsync(settings, new InvoiceXpressCreditNoteDraft(
                        rep.InvoiceExternalId,
                        new InvoiceXpressClientDraft(
                            string.IsNullOrWhiteSpace(rep.Cliente?.Nome) ? "Consumidor Final" : rep.Cliente.Nome,
                            rep.Cliente?.Email,
                            rep.Cliente?.Nif,
                            rep.Cliente?.Telefone),
                        $"Reparacao #{rep.Numero}",
                        items,
                        $"Anulacao da Fatura {rep.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }
        }

        rep.InvoiceProvider = BillingProvider.None;
        rep.InvoiceExternalId = null;
        rep.InvoiceNumber = null;
        rep.InvoicePdfUrl = null;
        rep.InvoiceEmittedAt = null;

        await _repo.SaveAsync(ct);
        var custoFinal = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        return ToDto(rep, custoFinal);
    }

    public async Task<ReparacaoDto> EmitirOrcamentoMoloniAsync(Guid id, CancellationToken ct = default)
    {
        var rep = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);
        if (!string.IsNullOrWhiteSpace(rep.EstimateExternalId))
        {
            var custoExistente = await _despesas.SumByReparacaoAsync(rep.Id, ct);
            return ToDto(rep, custoExistente);
        }

        var settings = await RequireMoloniSettingsAsync(ct);
        var tenant = await RequireTenantAsync(ct);
        rep.Cliente ??= await _clientes.FindByIdAsync(rep.ClienteId, ct);
        var customerId = await ResolveCustomerIdAsync(settings, rep.Cliente, ct);
        var amount = RequireAmount(rep.OrcamentoCents ?? rep.PrecoFinalCents);
        var vat = tenant.RegimeFiscal == RegimeFiscal.IsentoArt53 ? 0m : 23m;

        // Sprint 136: discrimina peças do stock + mão-de-obra (Bruno opção A: peça ao custo).
        // Se não há peças válidas ou se custaram mais que o orçamento, fallback à linha sintética.
        var moloniLines = await BuildBillingItemsAsync(rep, amount, vat, ct);

        var estimate = await _moloni.InsertEstimateAsync(settings, new MoloniInvoiceDraft(
            customerId,
            $"Reparacao #{rep.Numero}",
            $"Reparacao {rep.Equipamento}",
            rep.Avaria,
            amount,
            vat,
            null,
            Items: moloniLines),
            ct);

        rep.EstimateExternalId = estimate.ExternalId;
        rep.EstimateNumber = estimate.Number;
        rep.EstimatePdfUrl = estimate.PdfUrl;
        rep.EstimateEmittedAt = estimate.EmittedAt;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Reparacao), rep.Id, new
        {
            operation = "emit_moloni_estimate",
            estimate.ExternalId,
            estimate.Number,
        }, ct: ct);

        var custo = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        return ToDto(rep, custo);
    }

    public async Task<ReparacaoDto> ConverterOrcamentoEmFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var rep = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);
        if (string.IsNullOrWhiteSpace(rep.EstimateExternalId))
            throw new ConflictException("reparacao_sem_orcamento_moloni", "Esta reparacao nao tem orçamento Moloni emitido.");
        if (!string.IsNullOrWhiteSpace(rep.InvoiceExternalId))
        {
            var custoExistente = await _despesas.SumByReparacaoAsync(rep.Id, ct);
            return ToDto(rep, custoExistente);
        }
        if (!int.TryParse(rep.EstimateExternalId, out var estimateId))
            throw new RepairDesk.Core.Exceptions.ValidationException("moloni_estimate_id_invalid", "ID do orçamento Moloni inválido.");

        var settings = await RequireMoloniSettingsAsync(ct);
        var invoice = await _moloni.ConvertEstimateToInvoiceAsync(settings, estimateId, ct: ct);

        rep.InvoiceProvider = BillingProvider.Moloni;
        rep.InvoiceExternalId = invoice.ExternalId;
        rep.InvoiceNumber = invoice.Number;
        rep.InvoicePdfUrl = invoice.PdfUrl;
        rep.InvoiceEmittedAt = invoice.EmittedAt;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Reparacao), rep.Id, new
        {
            operation = "convert_moloni_estimate_to_invoice",
            estimateId = rep.EstimateExternalId,
            invoice.ExternalId,
            invoice.Number,
        }, ct: ct);

        var custo = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        return ToDto(rep, custo);
    }

    public async Task<ReparacaoDetalhadaDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _repo.FindByIdWithTimelineAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);
        var timeline = r.Timeline
            .OrderBy(t => t.MudouEm)
            .Select(t => new EstadoLogDto(t.Id, t.EstadoFrom, t.EstadoTo, t.MudouEm, t.Notas))
            .ToList();
        var custo = await _despesas.SumByReparacaoAsync(r.Id, ct);
        var fields = await _equipmentFields.GetValuesAsync(r.Id, visibleInPortalOnly: false, ct);

        // Sprint 87: se IMEI bate venda anterior, anexa info para banner "em garantia"
        var vendaOrigem = await ResolveVendaOrigemAsync(r, ct);

        return new ReparacaoDetalhadaDto(ToDto(r, custo, fields), timeline, vendaOrigem);
    }

    private async Task<ReparacaoVendaOrigemDto?> ResolveVendaOrigemAsync(Reparacao r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Imei)) return null;
        var vendaRow = await _vendas.FindVendaByImeiAsync(r.Imei, ct);
        if (vendaRow is null || vendaRow.Data >= r.CreatedAt) return null;

        var garantia = await _garantias.FindByVendaAsync(vendaRow.VendaId, ct);
        var agora = DateTime.UtcNow;
        var activa = garantia is not null
            && !garantia.Anulada
            && agora >= garantia.DataInicio
            && agora <= garantia.DataFim;
        var diasRestantes = garantia is null
            ? 0
            : (int)Math.Max(0, (garantia.DataFim - agora).TotalDays);
        var diasEntre = (int)Math.Round((r.CreatedAt - vendaRow.Data).TotalDays);

        return new ReparacaoVendaOrigemDto(
            vendaRow.VendaId,
            vendaRow.Numero,
            vendaRow.Data,
            garantia?.Slug,
            activa,
            diasRestantes,
            diasEntre,
            vendaRow.FornecedorNome,
            vendaRow.Condicao,
            vendaRow.GarantiaFornecedorAteAo);
    }

    public async Task<ReparacaoDto> CreateAsync(CreateReparacaoRequest req, CancellationToken ct = default)
    {
        await _createV.ValidateAndThrowAsync(req, ct);
        if (!_tenant.HasTenant) throw new ForbiddenException("no_tenant", "Tenant não definido.");

        var cliente = await _clientes.FindByIdAsync(req.ClienteId, ct)
            ?? throw new NotFoundException("Cliente", req.ClienteId);

        var estadoInicial = req.EstadoInicial ?? RepairStatus.Recebido;
        if (estadoInicial != RepairStatus.Recebido && estadoInicial != RepairStatus.Orcamento)
            throw new RepairDesk.Core.Exceptions.ValidationException("estado_inicial_invalido", "Estado inicial só pode ser Recebido ou Orçamento.");

        var now = DateTime.UtcNow;
        var rep = new Reparacao
        {
            ClienteId = cliente.Id,
            Equipamento = req.Equipamento.Trim(),
            Avaria = req.Avaria.Trim(),
            Imei = NormalizeImei(req.Imei),
            OrcamentoCents = req.OrcamentoCents,
            // Copia orçamento → preço final por default (utilizador pode editar)
            PrecoFinalCents = req.OrcamentoCents,
            Notas = req.Notas?.Trim(),
            Estado = estadoInicial,
            EstadoSince = now,
            PublicSlug = PublicSlugGenerator.New(),
        };
        rep.Timeline.Add(new ReparacaoEstadoLog
        {
            EstadoFrom = null,
            EstadoTo = estadoInicial,
            MudouEm = now,
            UserId = _user.UserId,
            Notas = estadoInicial == RepairStatus.Orcamento ? "Orçamento criado" : "Recebida",
        });
        await _repo.CreateWithNextNumeroAsync(rep, _tenant.TenantId!.Value, ct);
        IReadOnlyList<EquipmentFieldValueDto> fields = Array.Empty<EquipmentFieldValueDto>();
        if (req.EquipmentFieldTemplateId is not null || req.Fields is not null)
        {
            fields = await _equipmentFields.SetValuesAsync(rep.Id,
                new SetEquipmentFieldValuesRequest(req.EquipmentFieldTemplateId, req.Fields ?? Array.Empty<SetEquipmentFieldValueRequest>()),
                ct);
        }
        rep.Cliente = cliente;
        return ToDto(rep, 0, fields);
    }

    public async Task<ReparacaoDto> UpdateAsync(Guid id, UpdateReparacaoRequest req, CancellationToken ct = default)
    {
        await _updateV.ValidateAndThrowAsync(req, ct);
        var rep = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);

        if (req.ClienteId is not null && req.ClienteId.Value != rep.ClienteId)
        {
            var novoCliente = await _clientes.FindByIdAsync(req.ClienteId.Value, ct)
                ?? throw new NotFoundException("Cliente", req.ClienteId.Value);
            rep.ClienteId = novoCliente.Id;
            rep.Cliente = novoCliente;
        }

        rep.Equipamento = req.Equipamento.Trim();
        rep.Avaria = req.Avaria.Trim();
        rep.Imei = NormalizeImei(req.Imei);
        rep.Diagnostico = req.Diagnostico?.Trim();
        rep.OrcamentoCents = req.OrcamentoCents;
        rep.OrcamentoAprovado = req.OrcamentoAprovado;
        rep.PrecoFinalCents = req.PrecoFinalCents;
        // CustoPecasCents e derivado dos movimentos de stock, nao editado manualmente.
        rep.HorasGastas = req.HorasGastas;
        rep.Notas = req.Notas?.Trim();
        rep.EstadoPagamento = req.EstadoPagamento;

        await _repo.SaveAsync(ct);
        IReadOnlyList<EquipmentFieldValueDto>? fields = null;
        if (req.Fields is not null)
        {
            fields = await _equipmentFields.SetValuesAsync(rep.Id,
                new SetEquipmentFieldValuesRequest(req.EquipmentFieldTemplateId, req.Fields),
                ct);
        }
        rep.Cliente ??= await _clientes.FindByIdAsync(rep.ClienteId, ct);
        var custo = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        fields ??= await _equipmentFields.GetValuesAsync(rep.Id, visibleInPortalOnly: false, ct);
        return ToDto(rep, custo, fields);
    }

    public async Task<ReparacaoDto> ChangeEstadoAsync(Guid id, ChangeEstadoRequest req, CancellationToken ct = default)
    {
        await _estadoV.ValidateAndThrowAsync(req, ct);
        var rep = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);

        if (rep.Estado == req.Estado)
            throw new ConflictException("estado_unchanged", $"Reparação já está no estado {req.Estado}.");

        if (!IsValidTransition(rep.Estado, req.Estado))
            throw new ConflictException("estado_invalid_transition",
                $"Transição inválida: {rep.Estado} → {req.Estado}.");

        var now = DateTime.UtcNow;
        var log = new ReparacaoEstadoLog
        {
            ReparacaoId = rep.Id,
            EstadoFrom = rep.Estado,
            EstadoTo = req.Estado,
            MudouEm = now,
            UserId = _user.UserId,
            Notas = req.Notas?.Trim(),
        };
        rep.Estado = req.Estado;
        rep.EstadoSince = now;
        if (req.Estado == RepairStatus.Entregue)
        {
            rep.EntregueEm = now;
            // Default Pago ao entregar — utilizador pode override editando manualmente
            if (rep.EstadoPagamento == PaymentStatus.NaoPago)
                rep.EstadoPagamento = PaymentStatus.Pago;
        }

        _repo.AddEstadoLog(log);
        await _repo.SaveAsync(ct);

        // Auto-emite garantia ao Entregar (se ainda não existir)
        if (req.Estado == RepairStatus.Entregue)
        {
            await EmitirGarantiaSeNecessarioAsync(rep, now, ct);

            if (_tenant.TenantId is { } publishTenantId)
            {
                await _webhooks.PublishAsync(publishTenantId, WebhookEvents.ReparacaoConcluida, new
                {
                    reparacaoId = rep.Id,
                    reparacaoNumero = rep.Numero,
                    clienteId = rep.ClienteId,
                    equipamento = rep.Equipamento,
                    imei = rep.Imei,
                    precoFinalCents = rep.PrecoFinalCents,
                    entregueEm = rep.EntregueEm,
                }, ct);
            }
        }

        if (!string.IsNullOrWhiteSpace(rep.PublicSlug))
            await _pushQueue.EnqueueStatusChangedAsync(new RepairStatusChangedPushJob(rep.Id), ct);

        rep.Cliente ??= await _clientes.FindByIdAsync(rep.ClienteId, ct);
        var custoChange = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        return ToDto(rep, custoChange);
    }

    private async Task EmitirGarantiaSeNecessarioAsync(Reparacao rep, DateTime agora, CancellationToken ct)
    {
        var existente = await _garantias.FindByReparacaoAsync(rep.Id, ct);
        if (existente is not null) return;

        var tenant = _tenant.TenantId is { } tid ? await _tenants.FindByIdAsync(tid, ct) : null;
        var dias = tenant?.GarantiaDiasDefault ?? 90;
        var g = new Garantia
        {
            ReparacaoId = rep.Id,
            SourceType = GarantiaSourceType.Reparacao,
            Slug = PublicSlugGenerator.New(),
            DataInicio = agora,
            DataFim = agora.AddDays(dias),
            DiasGarantia = dias,
            Cobertura = tenant?.GarantiaCoberturaDefault,
            Exclusoes = tenant?.GarantiaExclusoesDefault,
        };
        await _garantias.AddAsync(g, ct);
        await _garantias.SaveAsync(ct);
    }

    public async Task<ReparacaoDto> ReabrirAsync(Guid id, string? notas, CancellationToken ct = default)
    {
        var rep = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);

        if (rep.Estado != RepairStatus.Entregue && rep.Estado != RepairStatus.Cancelado)
            throw new ConflictException("reabrir_estado_invalido",
                $"Só é possível reabrir reparações Entregues ou Canceladas. Estado actual: {rep.Estado}.");

        var now = DateTime.UtcNow;
        var estadoFrom = rep.Estado;
        // Volta para Pronto (Reparado) e desmarca pago. EntregueEm preservado para histórico.
        rep.Estado = RepairStatus.Pronto;
        rep.EstadoSince = now;
        rep.EstadoPagamento = PaymentStatus.NaoPago;
        rep.EntregueEm = null;

        var log = new ReparacaoEstadoLog
        {
            ReparacaoId = rep.Id,
            EstadoFrom = estadoFrom,
            EstadoTo = RepairStatus.Pronto,
            MudouEm = now,
            UserId = _user.UserId,
            Notas = string.IsNullOrWhiteSpace(notas) ? "Reaberta para correcção" : $"Reaberta: {notas}",
        };
        _repo.AddEstadoLog(log);
        await _repo.SaveAsync(ct);
        rep.Cliente ??= await _clientes.FindByIdAsync(rep.ClienteId, ct);
        var custo = await _despesas.SumByReparacaoAsync(rep.Id, ct);
        return ToDto(rep, custo);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rep = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Reparacao", id);
        _repo.Remove(rep);
        await _repo.SaveAsync(ct);
    }

    public Task<IReadOnlyList<EquipmentFieldValueDto>> SetFieldsAsync(Guid id, SetEquipmentFieldValuesRequest req, CancellationToken ct = default)
        => _equipmentFields.SetValuesAsync(id, req, ct);

    public async Task<ImportReparacoesResponse> ImportCsvAsync(string csv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_vazio", "CSV vazio.");
        if (!_tenant.HasTenant)
            throw new ForbiddenException("no_tenant", "Tenant não definido.");

        var rows = CsvParser.Parse(csv);
        if (rows.Count < 2)
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_sem_dados", "CSV precisa de header + pelo menos 1 linha de dados.");

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int Idx(params string[] names) => header
            .Select((h, i) => new { h, i })
            .FirstOrDefault(x => names.Contains(x.h))?.i ?? -1;

        var iEquip = Idx("equipamento", "device");
        var iAvaria = Idx("avaria", "problema", "issue");
        var iCNome = Idx("cliente", "clientenome", "cliente_nome", "nome");
        var iCTel = Idx("telefone", "clientetelefone", "cliente_telefone", "phone");
        var iCNif = Idx("nif", "clientenif", "cliente_nif");
        var iCEmail = Idx("email", "clienteemail", "cliente_email");
        var iImei = Idx("imei", "serial");
        var iEstado = Idx("estado", "status");
        var iOrc = Idx("orcamento", "orçamento", "estimativa");
        var iPreco = Idx("preco", "preço", "precofinal", "preço_final", "preco_final", "valor");
        var iData = Idx("recebidoem", "data", "recebido_em", "dataentrada", "data_entrada");
        var iPago = Idx("pago", "pagamento", "estadopagamento");
        var iDiag = Idx("diagnostico", "diagnóstico", "diagnostic");
        var iNotas = Idx("notas", "observacoes", "observações", "notes");

        if (iEquip < 0 || iAvaria < 0 || (iCNif < 0 && iCTel < 0 && iCNome < 0))
            throw new RepairDesk.Core.Exceptions.ValidationException(
                "csv_falta_coluna",
                "Faltam colunas obrigatórias. Mínimo: equipamento, avaria + (cliente OU telefone OU nif).");

        var erros = new List<ImportReparacaoError>();
        var clienteCache = new Dictionary<string, Cliente>(); // key: nif|tel|nome+tel
        var clientesCriados = 0;
        var clientesReutilizados = 0;
        var criadas = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            var linha = i + 1;
            var row = rows[i];
            string? Get(int idx) => idx >= 0 && idx < row.Length ? row[idx].Trim() : null;

            var equip = Get(iEquip);
            var avaria = Get(iAvaria);
            if (string.IsNullOrWhiteSpace(equip) || string.IsNullOrWhiteSpace(avaria))
            {
                erros.Add(new ImportReparacaoError(linha, "equipamento/avaria", "Equipamento e avaria são obrigatórios.", equip));
                continue;
            }

            var nif = Get(iCNif);
            var tel = Get(iCTel);
            var telNorm = string.IsNullOrWhiteSpace(tel) ? null : new string(tel.Where(c => !char.IsWhiteSpace(c)).ToArray());
            var nomeC = Get(iCNome);

            try
            {
                Cliente? cliente = null;
                var cacheKey = nif ?? telNorm ?? nomeC ?? Guid.NewGuid().ToString();
                if (!clienteCache.TryGetValue(cacheKey, out cliente!))
                {
                    if (!string.IsNullOrWhiteSpace(nif))
                        cliente = await _clientes.FindByNifAsync(nif, ct);
                    if (cliente is null && !string.IsNullOrWhiteSpace(telNorm))
                        cliente = await _clientes.FindByTelefoneAsync(telNorm, ct);

                    if (cliente is null)
                    {
                        if (string.IsNullOrWhiteSpace(nomeC) && string.IsNullOrWhiteSpace(telNorm) && string.IsNullOrWhiteSpace(nif))
                        {
                            erros.Add(new ImportReparacaoError(linha, "cliente", "Sem dados suficientes para identificar/criar cliente.", null));
                            continue;
                        }
                        cliente = new Cliente
                        {
                            Nome = string.IsNullOrWhiteSpace(nomeC) ? (telNorm ?? nif ?? "Cliente sem nome") : nomeC,
                            Telefone = telNorm,
                            Email = Get(iCEmail),
                            Nif = nif,
                        };
                        await _clientes.AddAsync(cliente, ct);
                        await _clientes.SaveAsync(ct);
                        clientesCriados++;
                    }
                    else
                    {
                        clientesReutilizados++;
                    }
                    clienteCache[cacheKey] = cliente;
                }

                var estado = ParseEstado(Get(iEstado));
                var orcCents = ParseEuros(Get(iOrc));
                var precoCents = ParseEuros(Get(iPreco)) ?? orcCents;
                var pago = ParsePago(Get(iPago));
                var imei = NormalizeImei(Get(iImei));
                var data = ParseData(Get(iData)) ?? DateTime.UtcNow;

                var rep = new Reparacao
                {
                    ClienteId = cliente.Id,
                    Equipamento = equip,
                    Avaria = avaria,
                    Imei = imei,
                    Diagnostico = Get(iDiag),
                    OrcamentoCents = orcCents,
                    PrecoFinalCents = precoCents,
                    Notas = Get(iNotas),
                    Estado = estado,
                    EstadoSince = data,
                    EntregueEm = estado == RepairStatus.Entregue ? data : null,
                    OrcamentoAprovado = estado != RepairStatus.Orcamento,
                    EstadoPagamento = pago,
                    PublicSlug = PublicSlugGenerator.New(),
                };
                rep.Timeline.Add(new ReparacaoEstadoLog
                {
                    EstadoFrom = null,
                    EstadoTo = estado,
                    MudouEm = data,
                    UserId = _user.UserId,
                    Notas = "Importado de CSV",
                });
                await _repo.CreateWithNextNumeroAsync(rep, _tenant.TenantId!.Value, ct);
                criadas++;
            }
            catch (Exception ex)
            {
                erros.Add(new ImportReparacaoError(linha, "?", ex.Message, equip));
            }
        }

        return new ImportReparacoesResponse(
            TotalLinhas: rows.Count - 1,
            Criadas: criadas,
            ClientesCriados: clientesCriados,
            ClientesReutilizados: clientesReutilizados,
            ComErro: erros.Count,
            Erros: erros);
    }

    public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
    {
        var rows = await _repo.ExportAllAsync(ct);
        var csv = new CsvBuilder();
        csv.Row(
            "numero", "equipamento", "avaria", "imei", "diagnostico",
            "clientenome", "telefone", "nif", "email",
            "estado", "estadosince", "recebidoem", "entregueem",
            "orcamento", "precofinal", "pago", "notas", "slug");

        foreach (var r in rows)
        {
            var estadoLabel = r.Estado switch
            {
                RepairStatus.Orcamento => "Orçamento",
                RepairStatus.Recebido => "Recebido",
                RepairStatus.Diagnostico => "Diagnóstico",
                RepairStatus.AguardaPeca => "Aguarda peça",
                RepairStatus.EmReparacao => "Em reparação",
                RepairStatus.Pronto => "Pronto",
                RepairStatus.Entregue => "Entregue",
                RepairStatus.Cancelado => "Cancelado",
                _ => r.Estado.ToString(),
            };
            var pagoLabel = r.EstadoPagamento switch
            {
                PaymentStatus.Pago => "Sim",
                PaymentStatus.PagoParcial => "Parcial",
                PaymentStatus.Anulado => "Anulado",
                _ => "Não",
            };
            csv.Row(
                r.Numero,
                r.Equipamento,
                r.Avaria,
                r.Imei,
                r.Diagnostico,
                r.Cliente?.Nome,
                r.Cliente?.Telefone,
                r.Cliente?.Nif,
                r.Cliente?.Email,
                estadoLabel,
                r.EstadoSince,
                r.CreatedAt,
                r.EntregueEm,
                r.OrcamentoCents.HasValue ? (r.OrcamentoCents.Value / 100m) : (object?)null,
                r.PrecoFinalCents.HasValue ? (r.PrecoFinalCents.Value / 100m) : (object?)null,
                pagoLabel,
                r.Notas,
                r.PublicSlug);
        }
        return csv.ToUtf8WithBom();
    }

    private static RepairStatus ParseEstado(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return RepairStatus.Recebido;
        var n = raw.Trim().ToLowerInvariant();
        // numérico directo
        if (int.TryParse(n, out var num) && Enum.IsDefined(typeof(RepairStatus), num))
            return (RepairStatus)num;
        return n switch
        {
            "orcamento" or "orçamento" or "orcamento pendente" => RepairStatus.Orcamento,
            "recebido" or "recebida" or "novo" or "entrada" => RepairStatus.Recebido,
            "diagnostico" or "diagnóstico" or "em análise" or "em analise" or "analise" => RepairStatus.Diagnostico,
            "aguarda peca" or "aguarda peça" or "aguardar peca" or "aguardar peça" or "aguarda" => RepairStatus.AguardaPeca,
            "em reparacao" or "em reparação" or "reparacao" or "reparação" or "a reparar" => RepairStatus.EmReparacao,
            "pronto" or "reparado" or "reparada" or "terminado" => RepairStatus.Pronto,
            "entregue" or "entregado" or "fechado" or "concluído" or "concluido" => RepairStatus.Entregue,
            "cancelado" or "cancelada" or "anulado" => RepairStatus.Cancelado,
            _ => RepairStatus.Recebido,
        };
    }

    private static PaymentStatus ParsePago(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PaymentStatus.NaoPago;
        var n = raw.Trim().ToLowerInvariant();
        return n switch
        {
            "1" or "sim" or "s" or "y" or "yes" or "true" or "pago" or "x" or "✓" => PaymentStatus.Pago,
            "parcial" or "pago parcial" or "metade" or "1/2" => PaymentStatus.PagoParcial,
            "anulado" or "void" => PaymentStatus.Anulado,
            _ => PaymentStatus.NaoPago,
        };
    }

    private static int? ParseEuros(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().Replace("€", "").Replace(" ", "").Replace(" ", "");
        // PT: vírgula decimal, ponto separador de milhares
        // EN: ponto decimal, vírgula separador de milhares
        // heurística: o último separador presente é o decimal
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot) s = s.Replace(".", "").Replace(",", ".");
            else s = s.Replace(",", "");
        }
        else if (lastComma >= 0) s = s.Replace(",", ".");

        if (!decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d)) return null;
        return (int)Math.Round(d * 100);
    }

    private static DateTime? ParseData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var formats = new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "dd/MM/yyyy", "dd-MM-yyyy", "dd/MM/yyyy HH:mm", "dd-MM-yyyy HH:mm" };
        if (DateTime.TryParseExact(raw.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        if (DateTime.TryParse(raw.Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out dt))
            return dt;
        return null;
    }

    public async Task<ReparacaoHistoricoResponse> HistoricoPorImeiAsync(string imei, Guid? excludeId, CancellationToken ct = default)
    {
        var normalizado = ImeiValidator.Normalize(imei);
        if (string.IsNullOrWhiteSpace(normalizado) || normalizado.Length < 6)
            return new ReparacaoHistoricoResponse(normalizado, false, 0, Array.Empty<ReparacaoHistoricoItem>());

        var luhn = ImeiValidator.IsValid(normalizado);
        var rows = await _repo.SearchByImeiAsync(normalizado, excludeId, ct);
        var items = rows.Select(r => new ReparacaoHistoricoItem(
            r.Id,
            r.Numero,
            r.Equipamento,
            r.Imei,
            r.Cliente is not null
                ? new ClienteResumo(r.Cliente.Id, r.Cliente.Nome, r.Cliente.Telefone ?? string.Empty, r.Cliente.Nif)
                : new ClienteResumo(r.ClienteId, "(?)", string.Empty),
            r.Estado,
            r.CreatedAt,
            r.EntregueEm,
            r.PrecoFinalCents,
            r.Diagnostico)).ToList();
        return new ReparacaoHistoricoResponse(normalizado, luhn, items.Count, items);
    }

    private static string? NormalizeImei(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var clean = ImeiValidator.Normalize(raw);
        return string.IsNullOrEmpty(clean) ? null : clean;
    }

    private static InvoiceXpressClientDraft ToInvoiceXpressClientDraft(Cliente? cliente)
        => new(
            string.IsNullOrWhiteSpace(cliente?.Nome) ? "Consumidor Final" : cliente.Nome,
            cliente?.Email,
            cliente?.Nif,
            cliente?.Telefone);

    private static bool IsValidTransition(RepairStatus from, RepairStatus to)
    {
        // Workflow granular (Sprint 17):
        //   Orcamento → Recebido
        //   Recebido → Diagnostico
        //   Diagnostico → AguardaPeca | EmReparacao | Pronto
        //   AguardaPeca → EmReparacao | Diagnostico (re-diagnóstico se peça era diferente)
        //   EmReparacao → Pronto | AguardaPeca (descobre que precisa de mais peça)
        //   Pronto → Entregue | Diagnostico (reabrir)
        //   * → Cancelado (qualquer estado pode ser cancelado)
        // Entregue e Cancelado são terminais.
        if (from == RepairStatus.Entregue) return false;
        if (from == RepairStatus.Cancelado) return false;

        return (from, to) switch
        {
            (_, RepairStatus.Cancelado) => true,
            (RepairStatus.Orcamento, RepairStatus.Recebido) => true,
            (RepairStatus.Recebido, RepairStatus.Diagnostico) => true,
            (RepairStatus.Diagnostico, RepairStatus.AguardaPeca) => true,
            (RepairStatus.Diagnostico, RepairStatus.EmReparacao) => true,
            (RepairStatus.Diagnostico, RepairStatus.Pronto) => true,
            (RepairStatus.AguardaPeca, RepairStatus.EmReparacao) => true,
            (RepairStatus.AguardaPeca, RepairStatus.Diagnostico) => true,
            (RepairStatus.EmReparacao, RepairStatus.Pronto) => true,
            (RepairStatus.EmReparacao, RepairStatus.AguardaPeca) => true,
            (RepairStatus.Pronto, RepairStatus.Entregue) => true,
            (RepairStatus.Pronto, RepairStatus.Diagnostico) => true, // reabrir
            _ => false
        };
    }

    private async Task<TenantBillingSettings> RequireMoloniSettingsAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new RepairDesk.Core.Exceptions.ValidationException("no_tenant_context", "Sem contexto de tenant.");
        var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
        if (settings is null || settings.Provider != BillingProvider.Moloni)
            throw new RepairDesk.Core.Exceptions.ValidationException("billing_provider_not_moloni", "Configura Moloni em Definicoes > Faturacao.");
        return settings;
    }

    private async Task<Tenant> RequireTenantAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new RepairDesk.Core.Exceptions.ValidationException("no_tenant_context", "Sem contexto de tenant.");
        return await _tenants.FindByIdAsync(tenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantId);
    }

    private async Task<int> ResolveCustomerIdAsync(TenantBillingSettings settings, Cliente? cliente, CancellationToken ct)
    {
        // 1. Se tem NIF, tenta encontrar na Moloni.
        if (!string.IsNullOrWhiteSpace(cliente?.Nif))
        {
            var id = await _moloni.FindCustomerIdByVatAsync(settings, cliente.Nif, ct);
            if (id is > 0) return id.Value;

            // 2. Não encontrado mas tem nome + NIF: cria automaticamente na Moloni.
            //    Sprint 65 fix: orçamento/fatura para cliente novo não falha mais por falta de ficha.
            if (!string.IsNullOrWhiteSpace(cliente.Nome))
            {
                var nome = cliente.Nome.Trim();
                if (nome.Length > 0)
                {
                    var created = await _moloni.InsertCustomerAsync(settings, nome, cliente.Nif.Trim(), ct);
                    if (created.Id > 0) return created.Id;
                }
            }
        }

        // 3. Sem NIF (ou criação falhou): fallback (típicamente "Consumidor Final" 999999990).
        if (settings.FallbackCustomerId is > 0)
            return settings.FallbackCustomerId.Value;

        // 4. Sprint 113: fallback hardcoded — tenta encontrar o "Consumidor Final" PT (NIF 999999990)
        //    no Moloni. É um cliente padrão que existe em todas as contas Moloni configuradas para PT.
        //    Bruno usa "Sérgio de Guimarães" sem NIF e o orçamento dava 422; agora cai aqui.
        var consumidorFinalId = await _moloni.FindCustomerIdByVatAsync(settings, "999999990", ct);
        if (consumidorFinalId is > 0) return consumidorFinalId.Value;

        throw new RepairDesk.Core.Exceptions.ValidationException(
            "moloni_customer_missing",
            "Cliente sem NIF e não foi possível encontrar Consumidor Final no Moloni. "
            + "Liga Moloni nas Definições (auto-discovery cria 'Consumidor Final') ou adiciona NIF ao cliente.");
    }

    /// <summary>
    /// Sprint 136: carrega peças usadas (líquido das devoluções) e constrói as linhas Moloni
    /// discriminadas (1 linha por peça + 1 linha de mão-de-obra). Devolve null se não há peças
    /// ou se não consegue calcular (peças > orçamento) — caller usa fallback à linha sintética.
    /// </summary>
    private async Task<IReadOnlyList<MoloniInvoiceDraftItem>?> BuildBillingItemsAsync(
        Reparacao rep, int totalCents, decimal vatPercent, CancellationToken ct)
    {
        var movimentos = await _parts.MovimentosAsync(partId: null, reparacaoId: rep.Id, ct);
        if (movimentos.Count == 0) return null;

        // Líquido por Part: Uso=quantidade negativa, Devolução=positiva. -Sum > 0 = consumido.
        var usedParts = movimentos
            .GroupBy(m => m.PartId)
            .Select(g =>
            {
                var first = g.First();
                var netQty = -g.Sum(m => m.Quantidade);
                var name = first.Part?.Nome ?? "Peça";
                var unitCost = first.Part?.CustoUnitarioCents ?? 0;
                return new Billing.ReparacaoBillingItemsBuilder.UsedPart(name, netQty, unitCost);
            })
            .Where(p => p.Quantity > 0)
            .ToList();

        if (usedParts.Count == 0) return null;
        return Billing.ReparacaoBillingItemsBuilder.Build(rep.Equipamento, usedParts, totalCents, vatPercent);
    }

    private static int RequireAmount(int? amountCents)
    {
        if (amountCents is null or <= 0)
            throw new RepairDesk.Core.Exceptions.ValidationException("estimate_amount_missing", "Define um valor de orçamento antes de emitir.");
        return amountCents.Value;
    }

    private static ReparacaoDto ToDto(Reparacao r, int custoDespesasCents, IReadOnlyList<EquipmentFieldValueDto>? fields = null)
    {
        var receita = r.PrecoFinalCents ?? 0;
        var lucro = receita - custoDespesasCents - r.CustoPecasCents;
        var cliente = r.Cliente is not null
            ? new ClienteResumo(r.Cliente.Id, r.Cliente.Nome, r.Cliente.Telefone ?? string.Empty, r.Cliente.Nif)
            : new ClienteResumo(r.ClienteId, "(?)", "");
        return new ReparacaoDto(
            r.Id, r.Numero, cliente,
            r.Equipamento, r.Avaria, r.Imei, r.Diagnostico,
            r.Estado, r.EstadoSince, r.CreatedAt, r.EntregueEm,
            r.OrcamentoCents, r.OrcamentoAprovado, r.PrecoFinalCents,
            r.CustoPecasCents, r.HorasGastas, lucro,
            custoDespesasCents,
            r.Notas, r.EstadoPagamento,
            r.PublicSlug,
            r.InvoiceProvider,
            r.InvoiceExternalId,
            r.InvoicePdfUrl,
            r.InvoiceNumber,
            r.InvoiceEmittedAt,
            r.EstimateExternalId,
            r.EstimateNumber,
            r.EstimatePdfUrl,
            r.EstimateEmittedAt,
            r.EquipmentFieldTemplateId,
            r.EquipmentFieldTemplate?.Nome,
            fields ?? Array.Empty<EquipmentFieldValueDto>());
    }
}
