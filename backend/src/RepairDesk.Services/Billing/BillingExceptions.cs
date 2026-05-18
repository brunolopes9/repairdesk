using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Billing;

public sealed class BillingProviderException(string code, string message)
    : DomainException(code, message);
