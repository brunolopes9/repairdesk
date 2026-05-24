using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.PublicPortal;

public class PublicPortalRateLimitTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public PublicPortalRateLimitTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PublicPortal_AllowsSixtyRequestsPerMinute_ThenReturns429()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var slugPrefix = $"missing-{Guid.NewGuid():N}";
        for (var i = 0; i < 60; i++)
        {
            var response = await client.GetAsync($"/api/public/repair/{slugPrefix}-{i}");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await client.GetAsync($"/api/public/repair/{slugPrefix}-61");

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
