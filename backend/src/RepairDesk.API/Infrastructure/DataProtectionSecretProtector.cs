using Microsoft.AspNetCore.DataProtection;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.Infrastructure;

public class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("RepairDesk.Billing.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}
