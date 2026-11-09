using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Conteudo;

/// <summary>
/// Sprint 598 (Doc 95): links assinados para o proxy de ficheiros da API, usado só quando o
/// storage não suporta URLs pré-assinados (dev local). O &lt;video&gt; e o PUT do browser não
/// conseguem enviar o JWT, por isso a autorização vai na própria URL (HMAC + expiração).
/// </summary>
public interface IConteudoLinkSigner
{
    string CreatePath(Guid ficheiroId, string acao, DateTimeOffset expiresAt);
    void Validate(Guid ficheiroId, string acao, long expires, string signature);
}

public sealed class ConteudoLinkSigner : IConteudoLinkSigner
{
    private readonly byte[] _key;

    public ConteudoLinkSigner(IConfiguration configuration)
    {
        var raw = configuration["Storage:SignedUrlKey"]
                  ?? configuration["Jwt:SigningKey"]
                  ?? "repairdesk-dev-signed-url-key-change-me";
        _key = Encoding.UTF8.GetBytes(raw);
    }

    public string CreatePath(Guid ficheiroId, string acao, DateTimeOffset expiresAt)
    {
        var expires = expiresAt.ToUnixTimeSeconds();
        var sig = Sign(ficheiroId, acao, expires);
        return $"/api/conteudo/ficheiros/{ficheiroId}/binario?acao={acao}&expires={expires}&sig={Uri.EscapeDataString(sig)}";
    }

    public void Validate(Guid ficheiroId, string acao, long expires, string signature)
    {
        if (DateTimeOffset.FromUnixTimeSeconds(expires) < DateTimeOffset.UtcNow)
            throw new ForbiddenException("signed_url_expired", "Link expirado.");
        var expected = Sign(ficheiroId, acao, expires);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature ?? "")))
            throw new ForbiddenException("signed_url_invalid", "Assinatura inválida.");
    }

    private string Sign(Guid ficheiroId, string acao, long expires)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"conteudo:{acao}:{ficheiroId:N}:{expires}")));
    }
}
