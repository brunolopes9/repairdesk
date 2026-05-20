using FluentValidation;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Trabalhos;

public interface ITrabalhoService
{
    Task<PagedResult<TrabalhoDto>> SearchAsync(string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<TrabalhoDto>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default);
    Task<TrabalhoDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<TrabalhoDto> CreateAsync(CreateTrabalhoRequest req, CancellationToken ct = default);
    Task<TrabalhoDto> UpdateAsync(Guid id, UpdateTrabalhoRequest req, CancellationToken ct = default);
    Task<TrabalhoDto> ReabrirAsync(Guid id, CancellationToken ct = default);
    Task<TrabalhoDto> AnularFaturaAsync(Guid id, CancellationToken ct = default);
    Task<TrabalhoDto> EmitirOrcamentoMoloniAsync(Guid id, CancellationToken ct = default);
    Task<TrabalhoDto> ConverterOrcamentoEmFaturaAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class TrabalhoService : ITrabalhoService
{
    private readonly ITrabalhoRepository _repo;
    private readonly IClienteRepository _clientes;
    private readonly IDespesaRepository _despesas;
    private readonly ITenantContext _tenant;
    private readonly ITenantBillingSettingsRepository _billingSettings;
    private readonly IMoloniClient _moloni;
    private readonly IInvoiceXpressClient _invoiceXpress;
    private readonly IAuditLogger _audit;
    private readonly IValidator<CreateTrabalhoRequest> _createV;
    private readonly IValidator<UpdateTrabalhoRequest> _updateV;

    public TrabalhoService(
        ITrabalhoRepository repo,
        IClienteRepository clientes,
        IDespesaRepository despesas,
        ITenantContext tenant,
        ITenantBillingSettingsRepository billingSettings,
        IMoloniClient moloni,
        IInvoiceXpressClient invoiceXpress,
        IAuditLogger audit,
        IValidator<CreateTrabalhoRequest> createV,
        IValidator<UpdateTrabalhoRequest> updateV)
    {
        _repo = repo;
        _clientes = clientes;
        _despesas = despesas;
        _tenant = tenant;
        _billingSettings = billingSettings;
        _moloni = moloni;
        _invoiceXpress = invoiceXpress;
        _audit = audit;
        _createV = createV;
        _updateV = updateV;
    }

    public async Task<TrabalhoDto> AnularFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);
        if (string.IsNullOrEmpty(t.InvoiceExternalId))
            throw new ConflictException("trabalho_sem_fatura", "Este trabalho nao tem fatura emitida para anular.");

        if (_tenant.TenantId is { } tenantId)
        {
            var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
            if (settings?.Provider == BillingProvider.Moloni && int.TryParse(t.InvoiceExternalId, out var originalDocId))
            {
                var cancelled = await _moloni.CancelDocumentAsync(
                    settings,
                    originalDocId,
                    $"Anulado via RepairDesk — trabalho #{t.Numero}",
                    ct);

                if (!cancelled)
                {
                    var valor = t.PrecoFinalCents ?? t.OrcamentoCents ?? 0;
                    var items = new List<RepairDesk.Services.Billing.MoloniInvoiceDraftItem>
                    {
                        new(t.Titulo, t.Descricao, 1, valor, 0, 23m),
                    };
                    var customerId = settings.FallbackCustomerId ?? 0;
                    if (customerId <= 0)
                        throw new RepairDesk.Core.Exceptions.ValidationException("moloni_customer_fallback_missing", "Cliente fallback Moloni nao configurado.");

                    await _moloni.InsertCreditNoteAsync(settings, new RepairDesk.Services.Billing.MoloniCreditNoteDraft(
                        originalDocId,
                        customerId,
                        $"Trabalho #{t.Numero}",
                        items,
                        $"Anulacao da Fatura {t.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }
            else if (settings?.Provider == BillingProvider.InvoiceXpress && t.InvoiceProvider == BillingProvider.InvoiceXpress)
            {
                if (t.ClienteId is not null) t.Cliente ??= await _clientes.FindByIdAsync(t.ClienteId.Value, ct);
                var reason = $"Anulado via RepairDesk - trabalho #{t.Numero}";
                var cancelled = await _invoiceXpress.CancelDocumentAsync(settings, t.InvoiceExternalId, reason, ct);

                if (!cancelled)
                {
                    var valor = t.PrecoFinalCents ?? t.OrcamentoCents ?? 0;
                    var items = new List<InvoiceXpressInvoiceDraftItem>
                    {
                        new(t.Titulo, t.Descricao, 1, valor, 0, 23m),
                    };

                    await _invoiceXpress.InsertCreditNoteAsync(settings, new InvoiceXpressCreditNoteDraft(
                        t.InvoiceExternalId,
                        ToInvoiceXpressClientDraft(t.Cliente),
                        $"Trabalho #{t.Numero}",
                        items,
                        $"Anulacao da Fatura {t.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }
        }

        t.InvoiceProvider = BillingProvider.None;
        t.InvoiceExternalId = null;
        t.InvoiceNumber = null;
        t.InvoicePdfUrl = null;
        t.InvoiceEmittedAt = null;

        await _repo.SaveAsync(ct);
        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task<TrabalhoDto> EmitirOrcamentoMoloniAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);
        if (!string.IsNullOrWhiteSpace(t.EstimateExternalId))
        {
            var custoExistente = await _despesas.SumByTrabalhoAsync(t.Id, ct);
            return ToDto(t, custoExistente);
        }

        var settings = await RequireMoloniSettingsAsync(ct);
        if (t.ClienteId is not null) t.Cliente ??= await _clientes.FindByIdAsync(t.ClienteId.Value, ct);
        var customerId = await ResolveCustomerIdAsync(settings, t.Cliente, ct);
        var amount = RequireAmount(t.OrcamentoCents ?? t.PrecoFinalCents);

        var estimate = await _moloni.InsertEstimateAsync(settings, new MoloniInvoiceDraft(
            customerId,
            $"Trabalho #{t.Numero}",
            t.Titulo,
            t.Descricao,
            amount,
            23m,
            null),
            ct);

        t.EstimateExternalId = estimate.ExternalId;
        t.EstimateNumber = estimate.Number;
        t.EstimatePdfUrl = estimate.PdfUrl;
        t.EstimateEmittedAt = estimate.EmittedAt;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Trabalho), t.Id, new
        {
            operation = "emit_moloni_estimate",
            estimate.ExternalId,
            estimate.Number,
        }, ct: ct);

        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task<TrabalhoDto> ConverterOrcamentoEmFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);
        if (string.IsNullOrWhiteSpace(t.EstimateExternalId))
            throw new ConflictException("trabalho_sem_orcamento_moloni", "Este trabalho nao tem orçamento Moloni emitido.");
        if (!string.IsNullOrWhiteSpace(t.InvoiceExternalId))
        {
            var custoExistente = await _despesas.SumByTrabalhoAsync(t.Id, ct);
            return ToDto(t, custoExistente);
        }
        if (!int.TryParse(t.EstimateExternalId, out var estimateId))
            throw new RepairDesk.Core.Exceptions.ValidationException("moloni_estimate_id_invalid", "ID do orçamento Moloni inválido.");

        var settings = await RequireMoloniSettingsAsync(ct);
        var invoice = await _moloni.ConvertEstimateToInvoiceAsync(settings, estimateId, ct: ct);

        t.InvoiceProvider = BillingProvider.Moloni;
        t.InvoiceExternalId = invoice.ExternalId;
        t.InvoiceNumber = invoice.Number;
        t.InvoicePdfUrl = invoice.PdfUrl;
        t.InvoiceEmittedAt = invoice.EmittedAt;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Trabalho), t.Id, new
        {
            operation = "convert_moloni_estimate_to_invoice",
            estimateId = t.EstimateExternalId,
            invoice.ExternalId,
            invoice.Number,
        }, ct: ct);

        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task<PagedResult<TrabalhoDto>> SearchAsync(
        string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(query, status, categoria, clienteId, page, pageSize, ct);
        var dtos = new List<TrabalhoDto>(items.Count);
        foreach (var t in items)
        {
            var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
            dtos.Add(ToDto(t, custo));
        }
        return new PagedResult<TrabalhoDto>(dtos, page, pageSize, total);
    }

    public async Task<IReadOnlyList<TrabalhoDto>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default)
    {
        var items = await _repo.ListPagasSemFaturaAsync(limit, ct);
        var dtos = new List<TrabalhoDto>(items.Count);
        foreach (var t in items)
        {
            var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
            dtos.Add(ToDto(t, custo));
        }
        return dtos;
    }

    public async Task<TrabalhoDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);
        if (t.ClienteId is not null) t.Cliente = await _clientes.FindByIdAsync(t.ClienteId.Value, ct);
        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task<TrabalhoDto> CreateAsync(CreateTrabalhoRequest req, CancellationToken ct = default)
    {
        await _createV.ValidateAndThrowAsync(req, ct);
        if (!_tenant.HasTenant) throw new ForbiddenException("no_tenant", "Tenant não definido.");

        Cliente? cliente = null;
        if (req.ClienteId is not null)
        {
            cliente = await _clientes.FindByIdAsync(req.ClienteId.Value, ct)
                ?? throw new NotFoundException("Cliente", req.ClienteId.Value);
        }

        var trabalho = new Trabalho
        {
            ClienteId = cliente?.Id,
            Titulo = req.Titulo.Trim(),
            Descricao = req.Descricao?.Trim(),
            Categoria = req.Categoria,
            OrcamentoCents = req.OrcamentoCents,
            // Copia orçamento → preço final (a UI passa a usar só "preço final")
            PrecoFinalCents = req.OrcamentoCents,
            Notas = req.Notas?.Trim(),
            Status = TrabalhoStatus.Orcamento,
        };
        await _repo.CreateWithNextNumeroAsync(trabalho, _tenant.TenantId!.Value, ct);
        trabalho.Cliente = cliente;
        return ToDto(trabalho, 0);
    }

    public async Task<TrabalhoDto> UpdateAsync(Guid id, UpdateTrabalhoRequest req, CancellationToken ct = default)
    {
        await _updateV.ValidateAndThrowAsync(req, ct);
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);

        Cliente? cliente = null;
        if (req.ClienteId is not null)
        {
            cliente = await _clientes.FindByIdAsync(req.ClienteId.Value, ct)
                ?? throw new NotFoundException("Cliente", req.ClienteId.Value);
        }

        t.ClienteId = cliente?.Id;
        t.Titulo = req.Titulo.Trim();
        t.Descricao = req.Descricao?.Trim();
        t.Categoria = req.Categoria;

        // Quando transita para Concluido, marca data e auto-pago se ainda NaoPago
        if (req.Status == TrabalhoStatus.Concluido && t.Status != TrabalhoStatus.Concluido)
        {
            t.DataConclusao = req.DataConclusao ?? DateTime.UtcNow;
            if (t.EstadoPagamento == PaymentStatus.NaoPago && req.EstadoPagamento == PaymentStatus.NaoPago)
                t.EstadoPagamento = PaymentStatus.Pago;
            else
                t.EstadoPagamento = req.EstadoPagamento;
        }
        else
        {
            t.DataConclusao = req.DataConclusao;
            t.EstadoPagamento = req.EstadoPagamento;
        }

        // Quando começa Em Execução pela primeira vez, marca data início
        if (req.Status == TrabalhoStatus.EmExecucao && t.DataInicio is null)
            t.DataInicio = req.DataInicio ?? DateTime.UtcNow;
        else
            t.DataInicio = req.DataInicio;

        t.Status = req.Status;
        t.OrcamentoCents = req.OrcamentoCents;
        t.PrecoFinalCents = req.PrecoFinalCents;
        t.HorasGastas = req.HorasGastas;
        t.Notas = req.Notas?.Trim();

        await _repo.SaveAsync(ct);
        t.Cliente = cliente ?? (t.ClienteId is not null ? await _clientes.FindByIdAsync(t.ClienteId.Value, ct) : null);
        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task<TrabalhoDto> ReabrirAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);

        if (t.Status != TrabalhoStatus.Concluido && t.Status != TrabalhoStatus.Cancelado)
            throw new ConflictException("reabrir_estado_invalido",
                $"Só é possível reabrir trabalhos Concluídos ou Cancelados. Estado actual: {t.Status}.");

        // Volta para EmExecucao + desmarca Pago + limpa DataConclusao
        t.Status = TrabalhoStatus.EmExecucao;
        t.EstadoPagamento = PaymentStatus.NaoPago;
        t.DataConclusao = null;
        await _repo.SaveAsync(ct);
        t.Cliente ??= (t.ClienteId is not null ? await _clientes.FindByIdAsync(t.ClienteId.Value, ct) : null);
        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);
        _repo.Remove(t);
        await _repo.SaveAsync(ct);
    }

    private static TrabalhoDto ToDto(Trabalho t, int custoDespesasCents)
    {
        var cliente = t.Cliente is not null
            ? new ClienteResumo(t.Cliente.Id, t.Cliente.Nome, t.Cliente.Telefone ?? string.Empty)
            : null;
        var receita = t.PrecoFinalCents ?? t.OrcamentoCents ?? 0;
        var lucro = receita - custoDespesasCents;
        return new TrabalhoDto(
            t.Id, t.Numero, cliente,
            t.Titulo, t.Descricao, t.Categoria, t.Status,
            t.CreatedAt, t.DataInicio, t.DataConclusao,
            t.OrcamentoCents, t.PrecoFinalCents, t.HorasGastas,
            t.Notas, t.EstadoPagamento,
            custoDespesasCents, lucro,
            t.InvoiceProvider,
            t.InvoiceExternalId,
            t.InvoicePdfUrl,
            t.InvoiceNumber,
            t.InvoiceEmittedAt,
            t.EstimateExternalId,
            t.EstimateNumber,
            t.EstimatePdfUrl,
            t.EstimateEmittedAt);
    }

    private static InvoiceXpressClientDraft ToInvoiceXpressClientDraft(Cliente? cliente)
        => new(
            string.IsNullOrWhiteSpace(cliente?.Nome) ? "Consumidor Final" : cliente.Nome,
            cliente?.Email,
            cliente?.Nif,
            cliente?.Telefone);

    private async Task<TenantBillingSettings> RequireMoloniSettingsAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new RepairDesk.Core.Exceptions.ValidationException("no_tenant_context", "Sem contexto de tenant.");
        var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
        if (settings is null || settings.Provider != BillingProvider.Moloni)
            throw new RepairDesk.Core.Exceptions.ValidationException("billing_provider_not_moloni", "Configura Moloni em Definicoes > Faturacao.");
        return settings;
    }

    private async Task<int> ResolveCustomerIdAsync(TenantBillingSettings settings, Cliente? cliente, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(cliente?.Nif))
        {
            var id = await _moloni.FindCustomerIdByVatAsync(settings, cliente.Nif, ct);
            if (id is > 0) return id.Value;

            // Sprint 66: cliente novo (nunca facturado) — cria na Moloni automaticamente.
            if (!string.IsNullOrWhiteSpace(cliente.Nome))
            {
                var created = await _moloni.InsertCustomerAsync(settings, cliente.Nome.Trim(), cliente.Nif.Trim(), ct);
                if (created.Id > 0) return created.Id;
            }
        }

        if (settings.FallbackCustomerId is > 0)
            return settings.FallbackCustomerId.Value;

        // Sprint 113: fallback hardcoded — Consumidor Final PT (NIF 999999990).
        var consumidorFinalId = await _moloni.FindCustomerIdByVatAsync(settings, "999999990", ct);
        if (consumidorFinalId is > 0) return consumidorFinalId.Value;

        throw new RepairDesk.Core.Exceptions.ValidationException(
            "moloni_customer_missing",
            "Cliente sem NIF e não foi possível encontrar Consumidor Final no Moloni. "
            + "Liga Moloni nas Definições (auto-discovery cria 'Consumidor Final') ou adiciona NIF ao cliente.");
    }

    private static int RequireAmount(int? amountCents)
    {
        if (amountCents is null or <= 0)
            throw new RepairDesk.Core.Exceptions.ValidationException("estimate_amount_missing", "Define um valor de orçamento antes de emitir.");
        return amountCents.Value;
    }
}
