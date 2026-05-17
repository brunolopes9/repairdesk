using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Services.Auth;

public class JwtTokenService : ITokenService
{
    public const string TenantIdClaim = "tenant_id";

    private readonly JwtOptions _opt;
    private readonly TimeProvider _clock;

    public JwtTokenService(IOptions<JwtOptions> opt, TimeProvider clock)
    {
        _opt = opt.Value;
        _clock = clock;
        if (string.IsNullOrWhiteSpace(_opt.SigningKey) || _opt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey is missing or shorter than 32 chars.");
    }

    public DateTime AccessTokenExpiry => _clock.GetUtcNow().UtcDateTime.AddMinutes(_opt.AccessTokenMinutes);

    public string IssueAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(TenantIdClaim, user.TenantId.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("name", user.DisplayName)
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: _clock.GetUtcNow().UtcDateTime,
            expires: AccessTokenExpiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
