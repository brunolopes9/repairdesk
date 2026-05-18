using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Fotos;

public interface IPhotoExportLinkService
{
    string CreatePath(Guid fotoId, DateTimeOffset expiresAt);
    void Validate(Guid fotoId, long expires, string signature);
}

public class PhotoExportLinkService : IPhotoExportLinkService
{
    private readonly byte[] _key;

    public PhotoExportLinkService(IConfiguration configuration)
    {
        var raw = configuration["Storage:SignedUrlKey"]
                  ?? configuration["Jwt:SigningKey"]
                  ?? "repairdesk-dev-signed-url-key-change-me";
        _key = Encoding.UTF8.GetBytes(raw);
    }

    public string CreatePath(Guid fotoId, DateTimeOffset expiresAt)
    {
        var expires = expiresAt.ToUnixTimeSeconds();
        var sig = Sign(fotoId, expires);
        return $"/api/reparacoes/fotos/{fotoId}/export-content?expires={expires}&sig={Uri.EscapeDataString(sig)}";
    }

    public void Validate(Guid fotoId, long expires, string signature)
    {
        if (DateTimeOffset.FromUnixTimeSeconds(expires) < DateTimeOffset.UtcNow)
            throw new ForbiddenException("signed_url_expired", "Link expirado.");
        var expected = Sign(fotoId, expires);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature)))
            throw new ForbiddenException("signed_url_invalid", "Assinatura inválida.");
    }

    private string Sign(Guid fotoId, long expires)
    {
        using var hmac = new HMACSHA256(_key);
        var payload = Encoding.UTF8.GetBytes($"{fotoId:N}:{expires}");
        return Convert.ToBase64String(hmac.ComputeHash(payload));
    }
}
