using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Billing;

public sealed class TenantBillingProvider : IBillingProvider
{
    private readonly ITenantContext _tenant;
    private readonly ITenantBillingSettingsRepository _settings;
    private readonly BillingProviderFactory _factory;

    public TenantBillingProvider(
        ITenantContext tenant,
        ITenantBillingSettingsRepository settings,
        BillingProviderFactory factory)
    {
        _tenant = tenant;
        _settings = settings;
        _factory = factory;
    }

    public async Task<InvoiceDto> EmitReparacaoInvoiceAsync(Guid reparacaoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).EmitReparacaoInvoiceAsync(reparacaoId, vatPercent, paymentMethod, ct);

    public async Task<InvoiceDto> EmitTrabalhoInvoiceAsync(Guid trabalhoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).EmitTrabalhoInvoiceAsync(trabalhoId, vatPercent, paymentMethod, ct);

    public async Task<InvoiceDto> EmitVendaInvoiceAsync(Guid vendaId, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).EmitVendaInvoiceAsync(vendaId, ct);

    public async Task<Stream> GetPdfStreamAsync(string invoiceId, CancellationToken ct = default)
        => await (await ResolveAsync(ct)).GetPdfStreamAsync(invoiceId, ct);

    private async Task<IBillingProvider> ResolveAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var settings = await _settings.FindByTenantIdAsync(tenantId, ct);
        if (settings is null)
            throw new ValidationException("billing_not_configured", "Configura a faturacao em Definicoes > Faturacao.");

        return _factory.GetProvider(settings);
    }
}
