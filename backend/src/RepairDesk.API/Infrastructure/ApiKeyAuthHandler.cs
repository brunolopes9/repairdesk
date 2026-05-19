using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.Infrastructure;

/// <summary>
/// Aceita <c>Authorization: ApiKey rd_live_xxx</c> ou <c>X-Api-Key: rd_live_xxx</c>.
/// Para integrações servidor-a-servidor (loja online, importadores).
/// Resolve <c>tenant_id</c> a partir da chave — autenticação E tenancy num passo.
/// </summary>
public sealed class ApiKeyAuthSchemeOptions : AuthenticationSchemeOptions { }

public sealed class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string AuthHeaderPrefix = "ApiKey ";
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly IServiceApiKeyRepository _repo;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceApiKeyRepository repo) : base(options, logger, encoder)
    {
        _repo = repo;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var plainKey = ExtractKey();
        if (plainKey is null) return AuthenticateResult.NoResult();

        if (!ApiKeyGenerator.LooksLikeApiKey(plainKey))
            return AuthenticateResult.Fail("Formato de API key inválido.");

        var hash = ApiKeyGenerator.Hash(plainKey);
        var apiKey = await _repo.FindActiveByHashAsync(hash, Context.RequestAborted);
        if (apiKey is null) return AuthenticateResult.Fail("API key inválida ou revogada.");

        // Best-effort tracking — não bloqueia o request se falhar.
        try
        {
            await _repo.UpdateLastUsedAsync(apiKey.Id, DateTime.UtcNow, Context.RequestAborted);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Falha a atualizar LastUsedAt para ServiceApiKey {KeyId}", apiKey.Id);
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim("tenant_id", apiKey.TenantId.ToString()));
        identity.AddClaim(new Claim("service_api_key_id", apiKey.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, $"service:{apiKey.Name}"));
        identity.AddClaim(new Claim("auth_type", "api_key"));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    private string? ExtractKey()
    {
        // X-Api-Key tem precedência (mais comum em integrações).
        if (Request.Headers.TryGetValue(ApiKeyHeaderName, out var xKey)
            && !string.IsNullOrWhiteSpace(xKey))
            return xKey.ToString().Trim();

        if (Request.Headers.TryGetValue("Authorization", out var auth))
        {
            var v = auth.ToString();
            if (v.StartsWith(AuthHeaderPrefix, StringComparison.OrdinalIgnoreCase))
                return v[AuthHeaderPrefix.Length..].Trim();
        }
        return null;
    }
}
