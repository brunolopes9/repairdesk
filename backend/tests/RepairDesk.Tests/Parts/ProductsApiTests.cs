using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Services.Products;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Parts;

public class ProductsApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public ProductsApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Search_FiltersByFornecedorActiveAndShopVisibility()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = "S309-" + Guid.NewGuid().ToString("N")[..8];
        var fornecedor = await CreateFornecedorAsync(client, "Molano " + marker);

        var molano = await CreateProductAsync(client, ProductRequest(
            marker,
            model: "Fornecedor",
            fornecedorId: fornecedor.Id,
            active: true,
            mostrarLojaOnline: true));
        var hiddenOwn = await CreateProductAsync(client, ProductRequest(
            marker,
            model: "Oculto",
            fornecedorId: null,
            active: false,
            mostrarLojaOnline: false));

        var byFornecedor = await client.GetFromJsonAsync<PagedResult<ProductDto>>(
            $"/api/products?search={marker}&fornecedorId={fornecedor.Id}&ativo=true&includeInactive=true");
        byFornecedor!.Items.Select(p => p.Id).Should().ContainSingle().Which.Should().Be(molano.Id);
        byFornecedor.Items[0].FornecedorId.Should().Be(fornecedor.Id);

        var inactive = await client.GetFromJsonAsync<PagedResult<ProductDto>>(
            $"/api/products?search={marker}&ativo=false&includeInactive=true");
        inactive!.Items.Select(p => p.Id).Should().Contain(hiddenOwn.Id).And.NotContain(molano.Id);

        var hidden = await client.GetFromJsonAsync<PagedResult<ProductDto>>(
            $"/api/products?search={marker}&mostrarLojaOnline=false&includeInactive=true");
        hidden!.Items.Select(p => p.Id).Should().Contain(hiddenOwn.Id).And.NotContain(molano.Id);

        var own = await client.GetFromJsonAsync<PagedResult<ProductDto>>(
            $"/api/products?search={marker}&fornecedorId={Guid.Empty}&includeInactive=true");
        own!.Items.Select(p => p.Id).Should().Contain(hiddenOwn.Id).And.NotContain(molano.Id);
    }

    private async Task<HttpClient> NewAuthedClient(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<FornecedorDto> CreateFornecedorAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/fornecedores", new FornecedorWriteRequest(
            name,
            Email: null,
            RmaEmail: null,
            Phone: null,
            Website: null,
            GarantiaB2BDiasDefault: null,
            Notas: null,
            Active: true));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<FornecedorDto>())!;
    }

    private static async Task<ProductDto> CreateProductAsync(HttpClient client, ProductWriteRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/products", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private static ProductWriteRequest ProductRequest(
        string marker,
        string model,
        Guid? fornecedorId,
        bool active,
        bool mostrarLojaOnline)
        => new(
            Sku: null,
            Slug: null,
            Brand: marker,
            Model: model,
            Storage: "128GB",
            Color: "Preto",
            Grading: ProductGrading.GradeB,
            Origin: ProductOrigin.Used,
            Grade: ProductGrade.B,
            SupplyType: fornecedorId.HasValue ? ProductSupplyType.Dropship : ProductSupplyType.Stock,
            Category: ProductCategory.Phone,
            DropshipSupplierSku: fornecedorId.HasValue ? marker + "-" + model : null,
            PriceCents: 24900,
            CompareAtPriceCents: null,
            StockQuantity: 3,
            StockMinima: 0,
            CustoUnitarioCents: 18000,
            DescriptionMarkdown: null,
            AttributesJson: null,
            SeoTitle: null,
            SeoDescription: null,
            OpenBoxReason: null,
            IsOpenBox: false,
            BatteryHealthPercent: null,
            TechnicalState: ProductTechnicalState.Unknown,
            TechnicalNotes: null,
            Active: active,
            MostrarLojaOnline: mostrarLojaOnline,
            FornecedorId: fornecedorId,
            Images: Array.Empty<ProductImageWriteRequest>());
}
