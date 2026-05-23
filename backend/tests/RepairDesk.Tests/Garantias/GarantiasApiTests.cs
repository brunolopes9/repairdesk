using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Garantias;

public class GarantiasApiTests : IClassFixture<RepairDeskApiFactory>
{
    private const string StaffScheme = "TestStaff";
    private readonly RepairDeskApiFactory _factory;

    public GarantiasApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anular_WithStaffUser_Returns403()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(StaffScheme)
                    .AddScheme<AuthenticationSchemeOptions, StaffAuthHandler>(StaffScheme, _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = StaffScheme;
                    options.DefaultChallengeScheme = StaffScheme;
                    options.DefaultForbidScheme = StaffScheme;
                });
            });
        });
        var client = NewClient(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/garantias/{Guid.NewGuid()}/anular",
            new { motivo = "Teste de autorizacao" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    private sealed class StaffAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public StaffAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, "staff@test.local"),
                new Claim(ClaimTypes.Role, "Staff"),
            };
            var identity = new ClaimsIdentity(claims, StaffScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, StaffScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
