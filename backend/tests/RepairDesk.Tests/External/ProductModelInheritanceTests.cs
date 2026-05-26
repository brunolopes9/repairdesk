using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.External;
using RepairDesk.Services.Products;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.External;

/// <summary>
/// Sprint 359/362: herança ProductModel → variante no webhook/external. É o contrato
/// de que o upsell de bateria da loja depende — batteryUpgradePriceCents + modelTemplateId
/// + descrição/specs resolvidos. Testa herança E override (unidade sobrepõe modelo).
/// </summary>
public class ProductModelInheritanceTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ProductModelInheritanceTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record CreateModelReq(string Brand, string Model, string? DescriptionMarkdown, string? SpecsJson, int? BatteryUpgradePriceCents, int? Category, string? Series, bool? Active);
    private sealed record ModelDto(Guid Id, string Brand, string Model, string? DescriptionMarkdown, int? BatteryUpgradePriceCents, string? Series);

    [Fact]
    public async Task UnidadeSemConteudo_HerdaDescricaoEPrecoBateriaDoModelo()
    {
        var jwt = await NewJwtClient();
        var api = await NewApiClient(jwt);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var brand = "Apple";
        var model = $"iPhone Test {marker}";

        // 1) Modelo com descrição + preço bateria.
        var modelResp = await jwt.PostAsJsonAsync("/api/product-models",
            new CreateModelReq(brand, model, "Top de gama com câmara excelente.", null, 5000, null, "iPhone Test", true));
        modelResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var modelo = (await modelResp.Content.ReadFromJsonAsync<ModelDto>())!;

        // 2) Variante SEM descrição própria (deve herdar). Liga ao modelo via brand+model (Sprint 359 D).
        var prod = await CreateProductAsync(jwt, brand, model, storage: "128GB", color: "Black", descricaoPropia: null);

        // 3) Webhook/external resolve a herança.
        var ext = await api.GetFromJsonAsync<ExternalProductDto>($"/api/external/products/{prod.Slug}");
        ext.Should().NotBeNull();
        ext!.ModelTemplateId.Should().Be(modelo.Id, "a variante aponta para o modelo-template");
        ext.BatteryUpgradePriceCents.Should().Be(5000, "preço da bateria vem do modelo");
        ext.Series.Should().Be("iPhone Test");
        ext.DescriptionMarkdown.Should().Be("Top de gama com câmara excelente.", "herdou a descrição do modelo");
    }

    [Fact]
    public async Task UnidadeComDescricaoPropia_NaoHerda_FazOverride()
    {
        var jwt = await NewJwtClient();
        var api = await NewApiClient(jwt);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var brand = "Samsung";
        var model = $"Galaxy Test {marker}";

        await jwt.PostAsJsonAsync("/api/product-models",
            new CreateModelReq(brand, model, "Descrição do MODELO.", null, 4500, null, null, true));

        var prod = await CreateProductAsync(jwt, brand, model, storage: "256GB", color: "Blue", descricaoPropia: "Descrição PRÓPRIA da unidade.");

        var ext = await api.GetFromJsonAsync<ExternalProductDto>($"/api/external/products/{prod.Slug}");
        ext!.DescriptionMarkdown.Should().Be("Descrição PRÓPRIA da unidade.", "override: a unidade tem a sua descrição");
        ext.BatteryUpgradePriceCents.Should().Be(4500, "o preço da bateria é sempre do modelo (não há override por unidade)");
    }

    [Fact]
    public async Task ModeloSemPrecoBateria_DevolveNull()
    {
        var jwt = await NewJwtClient();
        var api = await NewApiClient(jwt);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var brand = "Xiaomi";
        var model = $"Redmi Test {marker}";

        await jwt.PostAsJsonAsync("/api/product-models",
            new CreateModelReq(brand, model, "Sem upgrade de bateria.", null, null, null, null, true));

        var prod = await CreateProductAsync(jwt, brand, model, storage: "128GB", color: "Green", descricaoPropia: null);

        var ext = await api.GetFromJsonAsync<ExternalProductDto>($"/api/external/products/{prod.Slug}");
        ext!.BatteryUpgradePriceCents.Should().BeNull("modelo sem preço → null → shop não mostra upgrade");
    }

    private async Task<ProductDto> CreateProductAsync(HttpClient jwt, string brand, string model, string storage, string color, string? descricaoPropia)
    {
        var req = new ProductWriteRequest(
            Sku: $"TST-{Guid.NewGuid():N}".Substring(0, 16),
            Slug: null,
            Brand: brand, Model: model, Storage: storage, Color: color,
            Grading: ProductGrading.GradeA, Origin: ProductOrigin.Used, Grade: ProductGrade.A,
            SupplyType: ProductSupplyType.Stock, Category: ProductCategory.Phone,
            DropshipSupplierSku: null,
            PriceCents: 30000, CompareAtPriceCents: null, StockQuantity: 1, StockMinima: 0, CustoUnitarioCents: 20000,
            DescriptionMarkdown: descricaoPropia, AttributesJson: null, SeoTitle: null, SeoDescription: null,
            OpenBoxReason: null, IsOpenBox: false, BatteryHealthPercent: 88,
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
        var create = await jwt.PostAsJsonAsync("/api/service-keys", new CreateServiceApiKeyRequest($"inherit-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var resp = (await create.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;
        var api = _factory.CreateClient();
        api.DefaultRequestHeaders.Add("X-Api-Key", resp.PlainKey);
        return api;
    }
}
