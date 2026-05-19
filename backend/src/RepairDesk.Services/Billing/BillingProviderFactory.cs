using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing.InvoiceXpress;

namespace RepairDesk.Services.Billing;

public sealed class BillingProviderFactory
{
    private readonly MoloniBillingProvider _moloni;
    private readonly InvoiceXpressBillingProvider _invoiceXpress;

    public BillingProviderFactory(
        MoloniBillingProvider moloni,
        InvoiceXpressBillingProvider invoiceXpress)
    {
        _moloni = moloni;
        _invoiceXpress = invoiceXpress;
    }

    public IBillingProvider GetProvider(TenantBillingSettings settings)
        => settings.Provider switch
        {
            BillingProvider.Moloni => _moloni,
            BillingProvider.InvoiceXpress => _invoiceXpress,
            BillingProvider.None => throw new ValidationException(
                "billing_not_configured",
                "Configura a faturacao em Definicoes > Faturacao."),
            _ => throw new ValidationException(
                "billing_provider_not_supported",
                "Provider de faturacao desconhecido.")
        };
}
