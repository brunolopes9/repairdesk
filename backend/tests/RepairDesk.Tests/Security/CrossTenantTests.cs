using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Security;

public class CrossTenantTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public CrossTenantTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cliente_FromOtherTenant_Returns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.GetAsync($"/api/clientes/{ids.ClienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reparacao_FromOtherTenant_Returns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.GetAsync($"/api/reparacoes/{ids.ReparacaoId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Venda_FromOtherTenant_Returns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.GetAsync($"/api/vendas/{ids.VendaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Produto_FromOtherTenant_Returns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.GetAsync($"/api/products/{ids.ProductId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Garantia_FromOtherTenant_Returns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.GetAsync($"/api/garantias/by-venda/{ids.VendaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Fornecedor_FromOtherTenant_UpdateReturns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.PutAsJsonAsync($"/api/fornecedores/{ids.FornecedorId}", new
        {
            name = $"Fornecedor Editado {Guid.NewGuid():N}",
            email = "fornecedor@test.local",
            rmaEmail = (string?)null,
            phone = (string?)null,
            website = (string?)null,
            garantiaB2BDiasDefault = (int?)null,
            notas = (string?)null,
            active = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_FromOtherTenant_DeliveriesReturns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.GetAsync($"/api/webhooks/{ids.WebhookId}/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApiKey_FromOtherTenant_RevokeReturns404()
    {
        var ids = await SeedTenantBScenarioAsync();
        var client = await NewTenantAAdminClient();

        var response = await client.PostAsJsonAsync($"/api/service-keys/{ids.ApiKeyId}/revoke", new
        {
            reason = "cross-tenant-test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> NewTenantAAdminClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<CrossTenantIds> SeedTenantBScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var tenantId = RepairDeskApiFactory.SecondTenantId;

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nome = $"Cliente Tenant B {suffix}",
            Telefone = "912345678"
        };
        var reparacao = new Reparacao
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Numero = Random.Shared.Next(50_000, 90_000),
            ClienteId = cliente.Id,
            Equipamento = "iPhone 13",
            Avaria = "Teste cross-tenant",
            Estado = RepairStatus.Recebido,
            PublicSlug = $"r{suffix[..10]}"
        };
        var venda = new Venda
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Numero = Random.Shared.Next(50_000, 90_000),
            ClienteId = cliente.Id,
            TotalCents = 10000,
            IvaCents = 2300,
            PaymentMethod = PaymentMethod.MBWay,
            Status = VendaStatus.Paga
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Sku = $"SKU-{suffix[..8]}",
            Slug = $"produto-{suffix}",
            Brand = "Apple",
            Model = "iPhone 13",
            Storage = "128GB",
            Color = "Preto",
            PriceCents = 49900,
            StockQuantity = 1,
            StockMinima = 0,
            CustoUnitarioCents = 35000,
            Active = true,
            MostrarLojaOnline = true
        };
        var garantia = new Garantia
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendaId = venda.Id,
            SourceType = GarantiaSourceType.Venda,
            Slug = $"g{suffix[..10]}",
            DataInicio = DateTime.UtcNow,
            DataFim = DateTime.UtcNow.AddDays(365),
            DiasGarantia = 365
        };
        var fornecedor = new Fornecedor
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Fornecedor Tenant B {suffix}",
            Active = true
        };
        var webhook = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Webhook Tenant B {suffix}",
            Url = $"https://example.com/hooks/{suffix}",
            Secret = $"secret-{suffix}",
            Events = WebhookEvents.VendaCriada,
            Active = true
        };
        var apiKey = new ServiceApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"API Key Tenant B {suffix}",
            KeyPrefix = $"rd_test_{suffix[..8]}",
            KeyHash = Guid.NewGuid().ToString("N"),
            Scopes = ServiceApiKeyScopes.Read
        };

        db.AddRange(cliente, reparacao, venda, product, garantia, fornecedor, webhook, apiKey);
        await db.SaveChangesAsync();

        return new CrossTenantIds(
            cliente.Id,
            reparacao.Id,
            venda.Id,
            product.Id,
            garantia.Id,
            fornecedor.Id,
            webhook.Id,
            apiKey.Id);
    }

    private sealed record CrossTenantIds(
        Guid ClienteId,
        Guid ReparacaoId,
        Guid VendaId,
        Guid ProductId,
        Guid GarantiaId,
        Guid FornecedorId,
        Guid WebhookId,
        Guid ApiKeyId);
}
