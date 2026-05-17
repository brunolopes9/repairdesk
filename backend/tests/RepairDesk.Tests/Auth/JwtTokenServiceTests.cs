using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RepairDesk.Core.Entities;
using RepairDesk.Services.Auth;

namespace RepairDesk.Tests.Auth;

public class JwtTokenServiceTests
{
    private const string Key = "test-signing-key-with-at-least-32-chars-and-some-padding";

    private static (JwtTokenService svc, FakeTimeProvider clock) NewService(int? minutes = null)
    {
        var clock = new FakeTimeProvider(new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc));
        var opt = Options.Create(new JwtOptions
        {
            Issuer = "rd-test",
            Audience = "rd-test",
            SigningKey = Key,
            AccessTokenMinutes = minutes ?? 15,
            RefreshTokenDays = 7
        });
        return (new JwtTokenService(opt, clock), clock);
    }

    private static AppUser NewUser() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Email = "user@example.com",
        DisplayName = "Bruno",
        TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        UserName = "user@example.com"
    };

    [Fact]
    public void IssueAccessToken_IncludesExpectedClaims()
    {
        var (svc, _) = NewService();
        var user = NewUser();

        var jwt = svc.IssueAccessToken(user, ["Admin", "Tech"]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        token.Claims.Should().Contain(c => c.Type == JwtTokenService.TenantIdClaim && c.Value == user.TenantId.ToString());
        token.Claims.Should().Contain(c => c.Type == "name" && c.Value == user.DisplayName);
        token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo(["Admin", "Tech"]);
        token.Issuer.Should().Be("rd-test");
        token.Audiences.Should().ContainSingle().Which.Should().Be("rd-test");
    }

    [Fact]
    public void IssueAccessToken_ProducesValidSignature()
    {
        var (svc, _) = NewService();
        var jwt = svc.IssueAccessToken(NewUser(), []);

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = "rd-test",
            ValidAudience = "rd-test",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        };
        var act = () => handler.ValidateToken(jwt, parameters, out _);
        act.Should().NotThrow();
    }

    [Fact]
    public void AccessTokenExpiry_RespectsConfiguredMinutes()
    {
        var (svc, clock) = NewService(minutes: 5);
        var expiry = svc.AccessTokenExpiry;
        expiry.Should().Be(clock.UtcNow.UtcDateTime.AddMinutes(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public void Constructor_RejectsShortKey(string key)
    {
        var opt = Options.Create(new JwtOptions { SigningKey = key, Issuer = "i", Audience = "a" });
        var act = () => new JwtTokenService(opt, TimeProvider.System);
        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class FakeTimeProvider(DateTime now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(now, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
