using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.External;
using RepairDesk.Services.Products;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.External;

/// <summary>
/// Sprint 530 — contrato com a loja (lopestech-shop): a montra só trabalha em 4 graus (A+/A/B+/B).
/// Decisão do Bruno: "aceito A+ A B+ B do Molano, os restantes elimina". A++ (open-box) colapsa em
/// A+; Selado/C+/C NÃO são publicáveis na loja, mesmo com MostrarLojaOnline ligado.
/// </summary>
public class ShopGradeContractTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ShopGradeContractTests(RepairDeskApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(ProductGrade.APlusPlus, "A+")] // open-box colapsa em A+
    [InlineData(ProductGrade.APlus, "A+")]
    [InlineData(ProductGrade.A, "A")]
    [InlineData(ProductGrade.BPlus, "B+")]
    [InlineData(ProductGrade.B, "B")]
    public async Task GrausDeLoja_SaemNos4EstadosCanonicos(ProductGrade grade, string esperado)
    {
        var jwt = await NewJwtClient();
        var api = await NewApiClient(jwt);
        var prod = await CreateProductAsync(jwt, grade);

        var ext = await api.GetFromJsonAsync<ExternalProductDto>($"/api/external/products/{prod.Slug}");
        ext.Should().NotBeNull();
        ext!.Grade.Should().Be(esperado);
    }

    [Theory]
    [InlineData(ProductGrade.Sealed)]
    [InlineData(ProductGrade.CPlus)]
    [InlineData(ProductGrade.C)]
    public async Task GrausForaDoContrato_NaoVaoParaLoja_MesmoComMostrarLojaOnline(ProductGrade grade)
    {
        var jwt = await NewJwtClient();
        var api = await NewApiClient(jwt);
        var prod = await CreateProductAsync(jwt, grade);

        // Detalhe por slug → 404 (não publicável).
        var detalhe = await api.GetAsync($"/api/external/products/{prod.Slug}");
        detalhe.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // E não aparece na listagem da loja.
        var lista = await api.GetFromJsonAsync<PagedProbe>("/api/external/products?pageSize=100");
        lista.Should().NotBeNull();
        lista!.Items.Should().NotContain(p => p.Slug == prod.Slug);
    }

    private sealed record PagedProbe(List<ExternalProductDto> Items, int Page, int PageSize, int Total);

    private async Task<ProductDto> CreateProductAsync(HttpClient jwt, ProductGrade grade)
    {
        var req = new ProductWriteRequest(
            Sku: $"TST-{Guid.NewGuid():N}".Substring(0, 16),
            Slug: null,
            Brand: "Apple", Model: $"iPhone Grade {Guid.NewGuid():N}".Substring(0, 20), Storage: "128GB", Color: "Black",
            Grading: ProductGrading.GradeA, Origin: ProductOrigin.Used, Grade: grade,
            SupplyType: ProductSupplyType.Stock, Category: ProductCategory.Phone,
            DropshipSupplierSku: null,
            PriceCents: 30000, CompareAtPriceCents: null, StockQuantity: 1, StockMinima: 0, CustoUnitarioCents: 20000,
            DescriptionMarkdown: null, AttributesJson: null, SeoTitle: null, SeoDescription: null,
            OpenBoxReason: null, IsOpenBox: false, BatteryHealthPercent: 90,
            TechnicalState: ProductTechnicalState.Unknown, TechnicalNotes: null,
            Active: true, MostrarLojaOnline: true, FornecedorId: null, Images: null);
        var resp = await jwt.PostAsJsonAsync("/api/products", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private async Task<HttpClient> NewJwtClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> NewApiClient(HttpClient jwt)
    {
        var create = await jwt.PostAsJsonAsync("/api/service-keys", new CreateServiceApiKeyRequest($"shopgrade-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var resp = (await create.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;
        var api = _factory.CreateClient();
        api.DefaultRequestHeaders.Add("X-Api-Key", resp.PlainKey);
        return api;
    }
}
