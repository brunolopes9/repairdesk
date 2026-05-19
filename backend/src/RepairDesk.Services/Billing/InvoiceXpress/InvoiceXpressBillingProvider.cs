using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Billing.InvoiceXpress;

public sealed class InvoiceXpressBillingProvider : IBillingProvider
{
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITrabalhoRepository _trabalhos;
    private readonly IVendaRepository _vendas;
    private readonly ITenantBillingSettingsRepository _settingsRepo;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly IInvoiceXpressClient _invoiceXpress;

    public InvoiceXpressBillingProvider(
        IReparacaoRepository reparacoes,
        ITrabalhoRepository trabalhos,
        IVendaRepository vendas,
        ITenantBillingSettingsRepository settingsRepo,
        ITenantRepository tenants,
        ITenantContext tenant,
        IInvoiceXpressClient invoiceXpress)
    {
        _reparacoes = reparacoes;
        _trabalhos = trabalhos;
        _vendas = vendas;
        _settingsRepo = settingsRepo;
        _tenants = tenants;
        _tenant = tenant;
        _invoiceXpress = invoiceXpress;
    }

    public async Task<InvoiceDto> EmitReparacaoInvoiceAsync(Guid reparacaoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
    {
        var reparacao = await _reparacoes.FindByIdWithTimelineAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);

        if (reparacao.InvoiceExternalId is not null)
            return ToDto(reparacao.InvoiceNumber, reparacao.InvoicePdfUrl, reparacao.InvoiceEmittedAt);

        EnsurePaid(reparacao.EstadoPagamento);
        var settings = await RequireSettingsAsync(ct);
        var tenant = await RequireTenantAsync(ct);
        var amount = RequireAmount(reparacao.PrecoFinalCents ?? reparacao.OrcamentoCents);
        var effectiveVat = ResolveVatPercent(tenant, vatPercent);

        var result = await _invoiceXpress.InsertInvoiceAsync(settings, new InvoiceXpressInvoiceDraft(
            ToClientDraft(reparacao.Cliente),
            $"Reparacao #{reparacao.Numero}",
            $"Reparacao {reparacao.Equipamento}",
            reparacao.Avaria,
            amount,
            effectiveVat,
            paymentMethod),
            ct);

        reparacao.InvoiceProvider = BillingProvider.InvoiceXpress;
        reparacao.InvoiceExternalId = result.ExternalId;
        reparacao.InvoicePdfUrl = result.PdfUrl;
        reparacao.InvoiceNumber = result.Number;
        reparacao.InvoiceEmittedAt = result.EmittedAt;
        await _reparacoes.SaveAsync(ct);

        return new InvoiceDto(result.Number, result.PdfUrl, result.EmittedAt);
    }

    public async Task<InvoiceDto> EmitTrabalhoInvoiceAsync(Guid trabalhoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
    {
        var trabalho = await _trabalhos.FindByIdAsync(trabalhoId, ct)
            ?? throw new NotFoundException("Trabalho", trabalhoId);

        if (trabalho.InvoiceExternalId is not null)
            return ToDto(trabalho.InvoiceNumber, trabalho.InvoicePdfUrl, trabalho.InvoiceEmittedAt);

        EnsurePaid(trabalho.EstadoPagamento);
        var settings = await RequireSettingsAsync(ct);
        var tenant = await RequireTenantAsync(ct);
        var amount = RequireAmount(trabalho.PrecoFinalCents ?? trabalho.OrcamentoCents);
        var effectiveVat = ResolveVatPercent(tenant, vatPercent);

        var result = await _invoiceXpress.InsertInvoiceAsync(settings, new InvoiceXpressInvoiceDraft(
            ToClientDraft(trabalho.Cliente),
            $"Trabalho #{trabalho.Numero}",
            trabalho.Titulo,
            trabalho.Descricao,
            amount,
            effectiveVat,
            paymentMethod),
            ct);

        trabalho.InvoiceProvider = BillingProvider.InvoiceXpress;
        trabalho.InvoiceExternalId = result.ExternalId;
        trabalho.InvoicePdfUrl = result.PdfUrl;
        trabalho.InvoiceNumber = result.Number;
        trabalho.InvoiceEmittedAt = result.EmittedAt;
        await _trabalhos.SaveAsync(ct);

        return new InvoiceDto(result.Number, result.PdfUrl, result.EmittedAt);
    }

    public async Task<InvoiceDto> EmitVendaInvoiceAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct)
            ?? throw new NotFoundException("Venda", vendaId);

        if (venda.InvoiceExternalId is not null)
            return ToDto(venda.InvoiceNumber, venda.InvoicePdfUrl, venda.InvoiceEmittedAt);
        if (venda.Status != VendaStatus.Paga)
            throw new ValidationException("invoice_requires_paid", "So podes emitir fatura quando a venda estiver paga.");
        if (venda.Items.Count == 0)
            throw new ValidationException("invoice_items_missing", "A venda nao tem linhas para faturar.");

        var settings = await RequireSettingsAsync(ct);
        var documentType = venda.ClienteId is null
            ? BillingDocumentType.FaturaSimplificada
            : BillingDocumentType.Fatura;

        var items = venda.Items.Select(i => new InvoiceXpressInvoiceDraftItem(
            i.Descricao,
            i.Part?.Sku,
            i.Quantidade,
            i.PrecoUnitarioCents,
            i.DescontoCents,
            i.IvaRate)).ToList();

        var result = await _invoiceXpress.InsertInvoiceAsync(settings, new InvoiceXpressInvoiceDraft(
            ToClientDraft(venda.Cliente),
            $"Venda #{venda.Numero}",
            $"Venda #{venda.Numero}",
            venda.Notas,
            venda.TotalCents,
            items.Max(i => i.VatPercent),
            venda.PaymentMethod.ToString(),
            documentType,
            items),
            ct);

        venda.InvoiceProvider = BillingProvider.InvoiceXpress;
        venda.InvoiceExternalId = result.ExternalId;
        venda.InvoicePdfUrl = result.PdfUrl;
        venda.InvoiceNumber = result.Number;
        venda.InvoiceEmittedAt = result.EmittedAt;
        await _vendas.SaveAsync(ct);

        return new InvoiceDto(result.Number, result.PdfUrl, result.EmittedAt);
    }

    public async Task<Stream> GetPdfStreamAsync(string invoiceId, CancellationToken ct = default)
    {
        var settings = await RequireSettingsAsync(ct);
        return await _invoiceXpress.GetPdfStreamAsync(settings, invoiceId, ct);
    }

    private async Task<TenantBillingSettings> RequireSettingsAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var settings = await _settingsRepo.FindByTenantIdAsync(tenantId, ct);
        if (settings is null || settings.Provider == BillingProvider.None)
            throw new ValidationException("billing_not_configured", "Configura a faturacao em Definicoes > Faturacao.");
        if (settings.Provider != BillingProvider.InvoiceXpress)
            throw new ValidationException("billing_provider_not_invoicexpress", "Configura InvoiceXpress como provider de faturacao.");
        return settings;
    }

    private async Task<Tenant> RequireTenantAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        return await _tenants.FindByIdAsync(tenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantId);
    }

    private static InvoiceXpressClientDraft ToClientDraft(Cliente? cliente)
        => new(
            string.IsNullOrWhiteSpace(cliente?.Nome) ? "Consumidor Final" : cliente.Nome,
            cliente?.Email,
            cliente?.Nif,
            cliente?.Telefone);

    private static decimal ResolveVatPercent(Tenant tenant, decimal? explicitVat)
    {
        if (explicitVat is not null) return explicitVat.Value;
        return tenant.RegimeFiscal == RegimeFiscal.IsentoArt53 ? 0m : 23m;
    }

    private static void EnsurePaid(PaymentStatus paymentStatus)
    {
        if (paymentStatus != PaymentStatus.Pago)
            throw new ValidationException("invoice_requires_paid", "So podes emitir fatura quando estiver marcado como pago.");
    }

    private static int RequireAmount(int? amountCents)
    {
        if (amountCents is null or <= 0)
            throw new ValidationException("invoice_amount_missing", "Define um valor final antes de emitir fatura.");
        return amountCents.Value;
    }

    private static InvoiceDto ToDto(string? number, string? pdfUrl, DateTime? emittedAt)
        => new(number ?? "Fatura emitida", pdfUrl, emittedAt ?? DateTime.UtcNow);
}
