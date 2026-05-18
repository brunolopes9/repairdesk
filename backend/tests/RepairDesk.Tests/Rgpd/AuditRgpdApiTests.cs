using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Audit;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Rgpd;

public class AuditRgpdApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public AuditRgpdApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ExportarCliente_ReturnsPortableJson_WithRelatedDataAndSignedPhotoUrls()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateClienteAsync(client);
        var reparacao = await CreateReparacaoAsync(client, cliente.Id);
        var despesa = await CreateDespesaAsync(client, reparacao.Id);
        var fotoId = await AddFotoMetadataAsync(reparacao.Id);

        var resp = await client.GetAsync($"/api/clientes/{cliente.Id}/exportar");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await resp.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<ClientePortableExportDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        dto.Should().NotBeNull();
        dto!.Cliente.Id.Should().Be(cliente.Id);
        dto.Cliente.Nome.Should().Be(cliente.Nome);
        dto.Reparacoes.Should().ContainSingle(r => r.Id == reparacao.Id);
        dto.Despesas.Should().ContainSingle(d => d.Id == despesa.Id);
        dto.Fotos.Should().ContainSingle(f => f.Id == fotoId);
        dto.Fotos[0].SignedUrl.Should().Contain($"/api/reparacoes/fotos/{fotoId}/export-content");
        dto.Fotos[0].SignedUrlExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(6));
        dto.AuditEntries.Should().Contain(a => a.EntityType == "Cliente" && a.EntityId == cliente.Id);
    }

    [Fact]
    public async Task HardDelete_RemovesClienteAndRelatedRows_ButKeepsFinalAuditEntry()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateClienteAsync(client);
        var reparacao = await CreateReparacaoAsync(client, cliente.Id);
        var despesa = await CreateDespesaAsync(client, reparacao.Id);
        var fotoId = await AddFotoMetadataAsync(reparacao.Id);

        var bad = await DeleteJsonAsync(client, $"/api/clientes/{cliente.Id}/hard-delete",
            new HardDeleteClienteRequest("APAGAR outro nome", null));
        bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var resp = await DeleteJsonAsync(client, $"/api/clientes/{cliente.Id}/hard-delete",
            new HardDeleteClienteRequest($"APAGAR {cliente.Nome}", "Pedido RGPD no balcão"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleted = await resp.Content.ReadFromJsonAsync<HardDeleteClienteResponse>();
        deleted!.ClienteId.Should().Be(cliente.Id);
        deleted.Reparacoes.Should().Be(1);
        deleted.Despesas.Should().Be(1);
        deleted.Fotos.Should().Be(1);

        var getCliente = await client.GetAsync($"/api/clientes/{cliente.Id}");
        getCliente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Clientes.IgnoreQueryFilters().AnyAsync(c => c.Id == cliente.Id)).Should().BeFalse();
        (await db.Reparacoes.IgnoreQueryFilters().AnyAsync(r => r.Id == reparacao.Id)).Should().BeFalse();
        (await db.Despesas.IgnoreQueryFilters().AnyAsync(d => d.Id == despesa.Id)).Should().BeFalse();
        (await db.ReparacaoFotos.IgnoreQueryFilters().AnyAsync(f => f.Id == fotoId)).Should().BeFalse();

        var audit = await client.GetFromJsonAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=Cliente&entityId={cliente.Id}");
        audit!.Items.Should().ContainSingle(a => a.Action == AuditAction.HardDelete && a.EntityId == cliente.Id);

        var persistedAudit = await db.AuditEntries.IgnoreQueryFilters()
            .Where(a => a.EntityType == "Cliente" && a.EntityId == cliente.Id)
            .ToListAsync();
        persistedAudit.Should().ContainSingle(a => a.Action == AuditAction.HardDelete);
    }

    [Fact]
    public void AuditEntry_AppUserForeignKey_UsesSetNullSoAuditSurvivesUserDeletion()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var designModel = db.GetService<IDesignTimeModel>().Model;
        var auditEntity = designModel.FindEntityType(typeof(AuditEntry));
        auditEntity.Should().NotBeNull();

        var fk = auditEntity!.GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(AppUser));

        fk.DeleteBehavior.Should().Be(DeleteBehavior.SetNull);

        var createdAtIndex = auditEntity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(AuditEntry.TenantId), nameof(AuditEntry.CreatedAt) }));
        createdAtIndex.IsDescending.Should().Equal(false, true);
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

    private async Task<Guid> AddFotoMetadataAsync(Guid reparacaoId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var foto = new ReparacaoFoto
        {
            TenantId = RepairDeskApiFactory.TenantId,
            ReparacaoId = reparacaoId,
            StorageKey = $"tenants/{RepairDeskApiFactory.TenantId}/reparacoes/{reparacaoId}/{Guid.NewGuid():N}.jpg",
            FileName = "antes.jpg",
            ContentType = "image/jpeg",
            Size = 123,
            Tipo = FotoTipo.Antes,
            Ordem = 0,
            Legenda = "Antes",
            VisivelNoPortal = true,
        };
        db.ReparacaoFotos.Add(foto);
        await db.SaveChangesAsync();
        return foto.Id;
    }

    private static async Task<ClienteDto> CreateClienteAsync(HttpClient client)
    {
        var marker = Guid.NewGuid().ToString("N")[..6];
        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Cliente RGPD " + marker, "912333444", $"rgpd-{marker}@example.test", null, "Notas RGPD"));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static async Task<ReparacaoDto> CreateReparacaoAsync(HttpClient client, Guid clienteId)
    {
        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(clienteId, "iPhone 13", "Ecrã partido", "359123456789012", 12000, "Teste RGPD"));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }

    private static async Task<DespesaDto> CreateDespesaAsync(HttpClient client, Guid reparacaoId)
    {
        var resp = await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest("Peça RGPD", DespesaCategoria.Pecas, 3500, DateTime.UtcNow, "Fornecedor", null, null, null, reparacaoId));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DespesaDto>())!;
    }

    private static async Task<HttpResponseMessage> DeleteJsonAsync<T>(HttpClient client, string url, T payload)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(payload)
        };
        return await client.SendAsync(req);
    }
}
