using System.Security.Cryptography;
using FluentAssertions;
using RepairDesk.API.Backups;

namespace RepairDesk.Tests.Backups;

/// <summary>
/// Sprint 352 (Doc 76 gap): tests do encrypt/decrypt round-trip de dp-keys.
/// Não testa upload R2 (precisa de credentials) — testa só a parte crítica:
/// payload encriptado é determinístico-recuperável com a mesma password.
/// </summary>
public class DpKeysBackupTests
{
    private const string Password = "test-password-min-16-chars-long!";

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        // Simular o tarball como bytes arbitrários
        var original = System.Text.Encoding.UTF8.GetBytes("dp key fake content with some chars éç");

        var encrypted = RunEncrypt(original, Password);
        var decrypted = DpKeysBackupService.DecryptStream(encrypted, Password);

        decrypted.Should().Equal(original);
    }

    [Fact]
    public void EncryptedPayload_HasExpectedHeader()
    {
        var encrypted = RunEncrypt(System.Text.Encoding.UTF8.GetBytes("x"), Password);

        // Magic "MDRDP_K1"
        var magic = System.Text.Encoding.ASCII.GetString(encrypted, 0, 8);
        magic.Should().Be("MDRDP_K1");
        // Version byte = 0x01
        encrypted[8].Should().Be(0x01);
        // Total length: 8 magic + 1 ver + 16 salt + 12 nonce + 16 tag + cipher(=plaintext)
        encrypted.Length.Should().Be(8 + 1 + 16 + 12 + 16 + 1);
    }

    [Fact]
    public void Decrypt_WithWrongPassword_Throws()
    {
        var encrypted = RunEncrypt(System.Text.Encoding.UTF8.GetBytes("data"), Password);

        var act = () => DpKeysBackupService.DecryptStream(encrypted, "wrong-password-also-16-chars!");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithTamperedCipher_Throws()
    {
        var encrypted = RunEncrypt(System.Text.Encoding.UTF8.GetBytes("data"), Password);
        // Flip um bit no cipher (último byte)
        encrypted[^1] ^= 0x01;

        var act = () => DpKeysBackupService.DecryptStream(encrypted, Password);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithBadMagic_Throws()
    {
        var encrypted = RunEncrypt(System.Text.Encoding.UTF8.GetBytes("data"), Password);
        encrypted[0] = 0xFF;

        var act = () => DpKeysBackupService.DecryptStream(encrypted, Password);

        act.Should().Throw<CryptographicException>()
            .WithMessage("*magic*");
    }

    [Fact]
    public void Decrypt_WithUnknownVersion_Throws()
    {
        var encrypted = RunEncrypt(System.Text.Encoding.UTF8.GetBytes("data"), Password);
        encrypted[8] = 0xFF;

        var act = () => DpKeysBackupService.DecryptStream(encrypted, Password);

        act.Should().Throw<CryptographicException>()
            .WithMessage("*versão*");
    }

    [Fact]
    public void EncryptedPayload_ChangesEachTime()
    {
        // Salt + nonce aleatórios → cipher diferente cada vez para o mesmo input
        var data = System.Text.Encoding.UTF8.GetBytes("same input");
        var a = RunEncrypt(data, Password);
        var b = RunEncrypt(data, Password);
        a.Should().NotEqual(b);
    }

    /// <summary>
    /// Reflexão para chamar o método private EncryptStream — o public é só RunBackupAsync
    /// que requer R2 + filesystem. Mantém o test focado no algoritmo.
    /// </summary>
    private static byte[] RunEncrypt(byte[] plaintext, string password)
    {
        var method = typeof(DpKeysBackupService)
            .GetMethod("EncryptStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        using var ms = new MemoryStream(plaintext);
        return (byte[])method.Invoke(null, [ms, password])!;
    }
}
