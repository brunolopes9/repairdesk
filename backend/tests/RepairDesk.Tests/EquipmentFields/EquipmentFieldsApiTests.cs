using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.EquipmentFields;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.EquipmentFields;

public class EquipmentFieldsApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public EquipmentFieldsApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SetFields_RequiredDefinitionEmpty_Returns422()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var template = await CreateTemplateAsync(client, "Required-" + Marker(), required: true);
        var cliente = await CreateClienteAsync(client);
        var reparacao = await CreateReparacaoAsync(client, cliente.Id);

        var resp = await client.PostAsJsonAsync($"/api/reparacoes/{reparacao.Id}/fields",
            new SetEquipmentFieldValuesRequest(
                template.Id,
                new[] { new SetEquipmentFieldValueRequest(template.Fields[0].Id, "") }));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task TenantIsolation_TenantA_Template_NotVisibleToTenantB()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        var name = "Iso-" + Marker();

        var templateA = await CreateTemplateAsync(clientA, name, required: false);

        var activeB = await clientB.GetFromJsonAsync<IReadOnlyList<EquipmentFieldTemplateDto>>("/api/equipment-field-templates/active");

        activeB!.Select(t => t.Id).Should().NotContain(templateA.Id);
        activeB!.Select(t => t.Nome).Should().NotContain(name);
    }

    [Fact]
    public async Task DeleteTemplate_UsedByActiveRepair_Returns409()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var template = await CreateTemplateAsync(client, "Used-" + Marker(), required: false);
        var cliente = await CreateClienteAsync(client);

        var reparacao = await CreateReparacaoAsync(client, cliente.Id, template);

        var resp = await client.DeleteAsync($"/api/equipment-field-templates/{template.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var detail = await client.GetFromJsonAsync<ReparacaoDetalhadaDto>($"/api/reparacoes/{reparacao.Id}");
        detail!.Reparacao.Fields.Should().ContainSingle(f => f.FieldDefinitionId == template.Fields[0].Id && f.Value == "i7-12700H");
    }

    [Fact]
    public async Task DeleteTemplate_AfterRepairCancelled_PreservesHistoricalValues()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var template = await CreateTemplateAsync(client, "Archive-" + Marker(), required: false);
        var cliente = await CreateClienteAsync(client);
        var reparacao = await CreateReparacaoAsync(client, cliente.Id, template);

        var cancel = await client.PostAsJsonAsync($"/api/reparacoes/{reparacao.Id}/estado",
            new ChangeEstadoRequest(RepairStatus.Cancelado, "Teste"));
        cancel.EnsureSuccessStatusCode();

        var del = await client.DeleteAsync($"/api/equipment-field-templates/{template.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await client.GetFromJsonAsync<ReparacaoDetalhadaDto>($"/api/reparacoes/{reparacao.Id}");
        detail!.Reparacao.Fields.Should().ContainSingle(f => f.Label == "CPU" && f.Value == "i7-12700H");
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

    private static async Task<EquipmentFieldTemplateDto> CreateTemplateAsync(HttpClient client, string nome, bool required)
    {
        var resp = await client.PostAsJsonAsync("/api/equipment-field-templates",
            new CreateEquipmentFieldTemplateRequest(
                nome,
                DeviceCategory.Laptop,
                IsActive: true,
                Fields: new[]
                {
                    new UpsertEquipmentFieldDefinitionRequest(
                        Id: null,
                        Label: "CPU",
                        Type: EquipmentFieldType.Text,
                        Options: Array.Empty<string>(),
                        Required: required,
                        Ordem: 0,
                        VisibleInPortal: true),
                    new UpsertEquipmentFieldDefinitionRequest(
                        Id: null,
                        Label: "RAM",
                        Type: EquipmentFieldType.Text,
                        Options: Array.Empty<string>(),
                        Required: false,
                        Ordem: 1,
                        VisibleInPortal: true),
                }));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<EquipmentFieldTemplateDto>())!;
    }

    private static async Task<ClienteDto> CreateClienteAsync(HttpClient client)
    {
        var marker = Marker();
        var phoneSuffix = Random.Shared.Next(100000, 999999).ToString();
        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Cliente Campos " + marker, "912" + phoneSuffix, null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static async Task<ReparacaoDto> CreateReparacaoAsync(
        HttpClient client,
        Guid clienteId,
        EquipmentFieldTemplateDto? template = null)
    {
        var values = template is null
            ? null
            : new[]
            {
                new SetEquipmentFieldValueRequest(template.Fields[0].Id, "i7-12700H"),
                new SetEquipmentFieldValueRequest(template.Fields[1].Id, "16 GB"),
            };

        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(
                clienteId,
                "Lenovo Legion",
                "Nao liga",
                null,
                9500,
                null,
                EstadoInicial: null,
                EquipmentFieldTemplateId: template?.Id,
                Fields: values));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }

    private static string Marker() => Guid.NewGuid().ToString("N")[..8];
}
