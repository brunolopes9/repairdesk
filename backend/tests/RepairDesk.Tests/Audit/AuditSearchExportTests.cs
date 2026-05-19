using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Audit;
using RepairDesk.Services.Clientes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Audit;

public class AuditSearchExportTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public AuditSearchExportTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Search_FiltersByEntityUserActionAndText()
    {
        var (userId, otherUserId) = await SeedAuditAsync();
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var page = await client.GetFromJsonAsync<PagedResult<AuditEntryDto>>(
            $"/api/audit/search?entityTypes=Cliente&userIds={userId}&actions=0&search=Alice&pageSize=50");

        page!.Items.Should().ContainSingle();
        page.Items[0].EntityType.Should().Be("Cliente");
        page.Items[0].AppUserId.Should().Be(userId);
        page.Items[0].Action.Should().Be(AuditAction.Create);
        page.Items.Should().NotContain(x => x.AppUserId == otherUserId);
    }

    [Fact]
    public async Task ExportCsv_UsesActiveFiltersAndUtf8Bom()
    {
        await SeedAuditAsync();
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var bytes = await client.GetByteArrayAsync("/api/audit/export.csv?entityTypes=Cliente&actions=0&search=Alice");

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        text.Should().Contain("quando,utilizador,email,acao,entidade,entityId,ip,detalhe");
        text.Should().Contain("Alice");
        text.Should().NotContain("Stock ajustado");
    }

    private async Task<(Guid UserId, Guid OtherUserId)> SeedAuditAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == RepairDeskApiFactory.AdminEmail);
        var other = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == RepairDeskApiFactory.SecondAdminEmail);

        db.AuditEntries.RemoveRange(db.AuditEntries.IgnoreQueryFilters().Where(a => a.ChangesJson != null && a.ChangesJson.Contains("audit-test")));
        db.AuditEntries.AddRange(
            new AuditEntry
            {
                TenantId = RepairDeskApiFactory.TenantId,
                AppUserId = admin.Id,
                Action = AuditAction.Create,
                EntityType = "Cliente",
                EntityId = Guid.NewGuid(),
                ChangesJson = """{"marker":"audit-test","nome":"Alice"}""",
                IpAddress = "127.0.0.1",
                CreatedAt = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc),
            },
            new AuditEntry
            {
                TenantId = RepairDeskApiFactory.TenantId,
                AppUserId = admin.Id,
                Action = AuditAction.Update,
                EntityType = "Part",
                EntityId = Guid.NewGuid(),
                ChangesJson = """{"marker":"audit-test","descricao":"Stock ajustado"}""",
                IpAddress = "127.0.0.1",
                CreatedAt = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc),
            },
            new AuditEntry
            {
                TenantId = RepairDeskApiFactory.SecondTenantId,
                AppUserId = other.Id,
                Action = AuditAction.Create,
                EntityType = "Cliente",
                EntityId = Guid.NewGuid(),
                ChangesJson = """{"marker":"audit-test","nome":"Alice B"}""",
                IpAddress = "127.0.0.2",
                CreatedAt = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc),
            });
        await db.SaveChangesAsync();
        return (admin.Id, other.Id);
    }

    private async Task<HttpClient> NewAuthedClient(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
