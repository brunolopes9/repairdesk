using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Billing;

public class BillingOAuthApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public BillingOAuthApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task BillingOAuth_Start_ReturnsAuthorizationUrlWithState()
    {
        var client = await NewAuthedClient();
        await ConfigureMoloniAsync(client);

        var resp = await client.PostAsync("/api/billing/moloni/oauth/start", null);

        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<MoloniOAuthStartDto>())!;
        dto.AuthorizationUrl.Should().StartWith("https://www.moloni.pt/ac/root/oauth/");

        var query = QueryHelpers.ParseQuery(new Uri(dto.AuthorizationUrl).Query);
        query["response_type"].ToString().Should().Be("code");
        query["client_id"].ToString().Should().Be("repairdesk-test");
        query["redirect_uri"].ToString().Should().Contain("/api/billing/moloni/oauth/callback");
        query["state"].ToString().Should().HaveLength(32);
        dto.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task BillingOAuth_Callback_WithValidState_StoresTokensAndRedirectsConnected()
    {
        var client = await NewAuthedClient();
        await ConfigureMoloniAsync(client);
        var start = await StartAsync(client);
        var state = QueryHelpers.ParseQuery(new Uri(start.AuthorizationUrl).Query)["state"].ToString();

        var callback = await client.GetAsync($"/api/billing/moloni/oauth/callback?code=test-code&state={state}");

        callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
        callback.Headers.Location!.ToString().Should().StartWith("/definicoes?moloni=connected");

        var billing = await client.GetFromJsonAsync<TenantBillingSettingsDto>("/api/tenant-settings/me/billing");
        billing!.HasApiKey.Should().BeTrue();
        billing.HasRefreshToken.Should().BeTrue();
    }

    [Fact]
    public async Task BillingOAuth_Callback_WithInvalidState_RedirectsError()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var callback = await client.GetAsync("/api/billing/moloni/oauth/callback?code=test-code&state=invalid");

        callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
        callback.Headers.Location!.ToString().Should().StartWith("/definicoes?moloni=error");
    }

    [Fact]
    public async Task BillingOAuth_Start_WhenAlreadyConnected_Returns409()
    {
        var client = await NewAuthedClient();
        await ConfigureMoloniAsync(client, apiKey: "existing-access", refreshToken: "existing-refresh");

        var resp = await client.PostAsync("/api/billing/moloni/oauth/start", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpClient> NewAuthedClient()
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IMoloniClient>();
                    services.AddScoped<IMoloniClient, FakeOAuthMoloniClient>();
                });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task ConfigureMoloniAsync(
        HttpClient client,
        string? apiKey = null,
        string? refreshToken = null)
    {
        var resp = await client.PutAsJsonAsync("/api/tenant-settings/me/billing",
            new UpdateTenantBillingSettingsRequest(
                BillingProvider.Moloni,
                ApiKey: apiKey,
                ClientId: "repairdesk-test",
                ClientSecret: "client-secret",
                RefreshToken: refreshToken,
                CompanyId: null,
                DefaultDocumentType: BillingDocumentType.FaturaSimplificada,
                DefaultSerieId: null,
                SandboxMode: true,
                DefaultProductId: null,
                DefaultTaxId: null,
                DefaultPaymentMethodId: null,
                DefaultMaturityDateId: null,
                FallbackCustomerId: null,
                ExemptionReason: null));
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<MoloniOAuthStartDto> StartAsync(HttpClient client)
    {
        var resp = await client.PostAsync("/api/billing/moloni/oauth/start", null);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<MoloniOAuthStartDto>())!;
    }

    private sealed class FakeOAuthMoloniClient : IMoloniClient
    {
        private readonly ITenantBillingSettingsRepository _repo;

        public FakeOAuthMoloniClient(ITenantBillingSettingsRepository repo) => _repo = repo;

        public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BillingSerieDto>)Array.Empty<BillingSerieDto>());
        public Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default)
            => Task.FromResult<int?>(null);
        public Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("1", "FT 2026/1", null, DateTime.UtcNow));
        public Task<MoloniEstimateResult> InsertEstimateAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniEstimateResult("E1", "OR 2026/1", null, DateTime.UtcNow));
        public Task<int?> GetEstimateStatusAsync(TenantBillingSettings settings, int estimateId, CancellationToken ct = default)
            => Task.FromResult<int?>(1);
        public Task<MoloniInvoiceResult> ConvertEstimateToInvoiceAsync(TenantBillingSettings settings, int estimateId, BillingDocumentType? documentTypeOverride = null, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("1", "FT 2026/1", null, DateTime.UtcNow));
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
        public Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("NC1", "NC 2026/1", null, DateTime.UtcNow));
        public Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<int?> GetDocumentStatusAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default)
            => Task.FromResult<int?>(1);
        public Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default)
            => Task.CompletedTask;

        public async Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default)
        {
            code.Should().Be("test-code");
            redirectUri.Should().Contain("/api/billing/moloni/oauth/callback");
            settings.ApiKeyCipherText = "oauth-access";
            settings.RefreshTokenCipherText = "oauth-refresh";
            await _repo.SaveAsync(ct);
        }

        public Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniCompanyDto>)Array.Empty<MoloniCompanyDto>());
        public Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniProductDto>)Array.Empty<MoloniProductDto>());
        public Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniTaxDto>)Array.Empty<MoloniTaxDto>());
        public Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniPaymentMethodDto>)Array.Empty<MoloniPaymentMethodDto>());
        public Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniMaturityDateDto>)Array.Empty<MoloniMaturityDateDto>());
        public Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniCustomerDto>)Array.Empty<MoloniCustomerDto>());
        public Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default)
            => Task.FromResult(new MoloniProductDto(1, name, true));
        public Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, CancellationToken ct = default)
            => Task.FromResult(new MoloniCustomerDto(1, name, vat, true));
    }
}
