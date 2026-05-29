using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.StockTakes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.StockTakes;

/// <summary>
/// Sprint 425 (Doc 90 Tier 1 #3 follow-up): tests para o flow do inventário físico.
/// Cobre as regras com risco real: snapshot imutável, mutex (1 aberto), ajustes
/// gerados no Close, lock estado, permissões Admin-only.
/// </summary>
public class StockTakeApiTests : IClassFixture<RepairDeskApiFactory>
{
    private const string Prefix = "[STKTK]";
    private readonly RepairDeskApiFactory _factory;

    public StockTakeApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Open_CreatesSnapshotOfActiveParts()
    {
        await EnsureNoOpenStockTakeAsync();
        await SeedPartsAsync((Sku: $"{Prefix}-A", Qtd: 10), (Sku: $"{Prefix}-B", Qtd: 5));
        var client = await NewAdminClient();

        var resp = await client.PostAsync("/api/stock-takes", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await resp.Content.ReadFromJsonAsync<StockTakeDto>())!;

        dto.Status.Should().Be(StockTakeStatus.Aberto);
        dto.TotalItems.Should().BeGreaterThanOrEqualTo(2);
        dto.ContadosCount.Should().Be(0);
        dto.Items.Should().NotBeNull();
        dto.Items!.Should().Contain(i => i.PartSku == $"{Prefix}-A" && i.QtdSistema == 10);
        dto.Items!.Should().Contain(i => i.PartSku == $"{Prefix}-B" && i.QtdSistema == 5);
    }

    [Fact]
    public async Task Open_WhenAlreadyOpen_ReturnsConflict()
    {
        await EnsureNoOpenStockTakeAsync();
        await SeedPartsAsync((Sku: $"{Prefix}-DUP", Qtd: 1));
        var client = await NewAdminClient();
        var first = await client.PostAsync("/api/stock-takes", content: null);
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsync("/api/stock-takes", content: null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Count_ValidQuantity_PersistsCountedAt()
    {
        await EnsureNoOpenStockTakeAsync();
        await SeedPartsAsync((Sku: $"{Prefix}-CNT", Qtd: 8));
        var client = await NewAdminClient();
        var (st, partId) = await OpenAndPickPartAsync(client, $"{Prefix}-CNT");

        var resp = await client.PutAsJsonAsync($"/api/stock-takes/{st.Id}/items/{partId}", new CountItemRequest(7));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await resp.Content.ReadFromJsonAsync<StockTakeItemDto>())!;

        item.QtdContada.Should().Be(7);
        item.Diferenca.Should().Be(-1);
        item.ContadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Count_NegativeQuantity_ReturnsValidationError()
    {
        await EnsureNoOpenStockTakeAsync();
        await SeedPartsAsync((Sku: $"{Prefix}-NEG", Qtd: 3));
        var client = await NewAdminClient();
        var (st, partId) = await OpenAndPickPartAsync(client, $"{Prefix}-NEG");

        var resp = await client.PutAsJsonAsync($"/api/stock-takes/{st.Id}/items/{partId}", new CountItemRequest(-1));

        // Middleware global mapeia ValidationException → 422 Unprocessable Entity.
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Close_GeneratesAjusteForDifferences_AndUpdatesPartStock()
    {
        await EnsureNoOpenStockTakeAsync();
        await SeedPartsAsync((Sku: $"{Prefix}-CLS", Qtd: 12));
        var client = await NewAdminClient();
        var (st, partId) = await OpenAndPickPartAsync(client, $"{Prefix}-CLS");
        await client.PutAsJsonAsync($"/api/stock-takes/{st.Id}/items/{partId}", new CountItemRequest(15));

        var resp = await client.PostAsJsonAsync($"/api/stock-takes/{st.Id}/close", new CloseStockTakeRequest("teste"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = (await resp.Content.ReadFromJsonAsync<StockTakeDto>())!;
        closed.Status.Should().Be(StockTakeStatus.Concluido);
        closed.ClosedAt.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var part = await db.Parts.IgnoreQueryFilters().SingleAsync(p => p.Id == partId);
        part.QtdStock.Should().Be(15);

        var movimento = await db.PartMovimentos
            .IgnoreQueryFilters()
            .Where(m => m.PartId == partId && m.Motivo == PartMovimentoMotivo.AjusteManual)
            .OrderByDescending(m => m.CreatedAt)
            .FirstAsync();
        movimento.Quantidade.Should().Be(3);
        movimento.StockAntes.Should().Be(12);
        movimento.StockDepois.Should().Be(15);
        movimento.Notas.Should().Contain("Inventário");
    }

    [Fact]
    public async Task Close_WithZeroDifference_DoesNotCreateMovement()
    {
        await EnsureNoOpenStockTakeAsync();
        await SeedPartsAsync((Sku: $"{Prefix}-ZRO", Qtd: 4));
        var client = await NewAdminClient();
        var (st, partId) = await OpenAndPickPartAsync(client, $"{Prefix}-ZRO");
        await client.PutAsJsonAsync($"/api/stock-takes/{st.Id}/items/{partId}", new CountItemRequest(4));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var movsBefore = await db.PartMovimentos.IgnoreQueryFilters().CountAsync(m => m.PartId == partId);
            movsBefore.Should().Be(0);
        }

        var resp = await client.PostAsJsonAsync($"/api/stock-takes/{st.Id}/close", new CloseStockTakeRequest(null));
        resp.EnsureSuccessStatusCode();

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var movsAfter = await db2.PartMovimentos.IgnoreQueryFilters().CountAsync(m => m.PartId == partId);
        movsAfter.Should().Be(0);
    }

    [Fact]
    public async Task Cancel_MarksCancelado_NoMovementsCreated()
    {
        await EnsureNoOpenStockTakeAsync();
        await SeedPartsAsync((Sku: $"{Prefix}-CXL", Qtd: 6));
        var client = await NewAdminClient();
        var (st, partId) = await OpenAndPickPartAsync(client, $"{Prefix}-CXL");
        await client.PutAsJsonAsync($"/api/stock-takes/{st.Id}/items/{partId}", new CountItemRequest(20));

        var resp = await client.PostAsync($"/api/stock-takes/{st.Id}/cancel", content: null);
        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<StockTakeDto>())!;
        dto.Status.Should().Be(StockTakeStatus.Cancelado);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var movs = await db.PartMovimentos.IgnoreQueryFilters().CountAsync(m => m.PartId == partId);
        movs.Should().Be(0);
        var part = await db.Parts.IgnoreQueryFilters().SingleAsync(p => p.Id == partId);
        part.QtdStock.Should().Be(6);
    }

    [Fact]
    public async Task NonAdmin_CannotOpenStockTake()
    {
        await EnsureNoOpenStockTakeAsync();
        var nonAdmin = await SeedNonAdminUserAsync($"stktk-nonadmin-{Guid.NewGuid():N}@test.local");
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(nonAdmin.Email!, "Test!Pass2026"));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await client.PostAsync("/api/stock-takes", content: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    /// <summary>
    /// O fixture é IClassFixture (DB partilhada entre tests). A regra de servidor diz
    /// que só pode haver 1 stocktake Aberto por tenant. Cancela qualquer aberto para
    /// que cada test arranque do zero. EF InMemory: update directo + SaveChanges.
    /// </summary>
    private async Task EnsureNoOpenStockTakeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var open = await db.StockTakes
            .IgnoreQueryFilters()
            .Where(s => s.Status == StockTakeStatus.Aberto)
            .ToListAsync();
        foreach (var s in open)
        {
            s.Status = StockTakeStatus.Cancelado;
            s.ClosedAt = DateTime.UtcNow;
        }
        if (open.Count > 0) await db.SaveChangesAsync();
    }

    private async Task SeedPartsAsync(params (string Sku, int Qtd)[] parts)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (sku, qtd) in parts)
        {
            db.Parts.Add(new Part
            {
                Id = Guid.NewGuid(),
                TenantId = RepairDeskApiFactory.TenantId,
                Sku = sku,
                Nome = sku,
                QtdStock = qtd,
                Activo = true,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<(StockTakeDto St, Guid PartId)> OpenAndPickPartAsync(HttpClient client, string sku)
    {
        var resp = await client.PostAsync("/api/stock-takes", content: null);
        resp.EnsureSuccessStatusCode();
        var st = (await resp.Content.ReadFromJsonAsync<StockTakeDto>())!;
        var item = st.Items!.Single(i => i.PartSku == sku);
        return (st, item.PartId);
    }

    private async Task<HttpClient> NewAdminClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<AppUser> SeedNonAdminUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Non-admin StockTake Test",
            TenantId = RepairDeskApiFactory.TenantId,
            IsActive = true,
        };
        var result = await users.CreateAsync(user, "Test!Pass2026");
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors.Select(e => e.Code)));
        return user;
    }
}
