namespace RepairDesk.Core.Abstractions;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string cipherText);
}
