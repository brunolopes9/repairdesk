using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.InternalTasks;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.InternalTasks;

/// <summary>
/// Sprint 426 (Doc 90 Tier 2 #7 follow-up): tests para o flow das tarefas internas.
/// Foco nas regras com risco: validação de título, CompletedAt stamp/clear ao
/// mudar de estado, filtros, autenticação obrigatória.
/// </summary>
public class InternalTaskApiTests : IClassFixture<RepairDeskApiFactory>
{
    private const string Prefix = "[ITSK]";
    private readonly RepairDeskApiFactory _factory;

    public InternalTaskApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_ValidPayload_ReturnsTaskWithPendenteStatus()
    {
        var client = await NewAuthedClient();
        var due = DateTime.UtcNow.AddDays(2);

        var resp = await client.PostAsJsonAsync("/api/internal-tasks",
            new CreateInternalTaskRequest($"{Prefix} Pedir bateria iPhone 13", "ao fornecedor Tudo4Mobile", due, null, null));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await resp.Content.ReadFromJsonAsync<InternalTaskDto>())!;
        dto.Title.Should().Be($"{Prefix} Pedir bateria iPhone 13");
        dto.Description.Should().Be("ao fornecedor Tudo4Mobile");
        dto.Status.Should().Be(InternalTaskStatus.Pendente);
        dto.CompletedAt.Should().BeNull();
        dto.DueAt.Should().NotBeNull();
        dto.DueAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task Create_TitleTooShort_ReturnsValidationError(string title)
    {
        var client = await NewAuthedClient();

        var resp = await client.PostAsJsonAsync("/api/internal-tasks",
            new CreateInternalTaskRequest(title, null, null, null, null));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ChangeStatus_ToConcluida_StampsCompletedAt()
    {
        var client = await NewAuthedClient();
        var created = await CreateAsync(client, $"{Prefix} Toggle done");

        var beforeUtc = DateTime.UtcNow.AddSeconds(-1);
        var resp = await client.PostAsJsonAsync($"/api/internal-tasks/{created.Id}/status",
            new ChangeInternalTaskStatusRequest(InternalTaskStatus.Concluida));

        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<InternalTaskDto>())!;
        dto.Status.Should().Be(InternalTaskStatus.Concluida);
        dto.CompletedAt.Should().NotBeNull();
        dto.CompletedAt!.Value.Should().BeOnOrAfter(beforeUtc);
    }

    [Fact]
    public async Task ChangeStatus_BackToPendente_ClearsCompletedAt()
    {
        var client = await NewAuthedClient();
        var created = await CreateAsync(client, $"{Prefix} Reabrir tarefa");
        await client.PostAsJsonAsync($"/api/internal-tasks/{created.Id}/status",
            new ChangeInternalTaskStatusRequest(InternalTaskStatus.Concluida));

        var resp = await client.PostAsJsonAsync($"/api/internal-tasks/{created.Id}/status",
            new ChangeInternalTaskStatusRequest(InternalTaskStatus.Pendente));

        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<InternalTaskDto>())!;
        dto.Status.Should().Be(InternalTaskStatus.Pendente);
        dto.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var client = await NewAuthedClient();
        var pendente = await CreateAsync(client, $"{Prefix} Filter pendente {Guid.NewGuid():N}");
        var concluida = await CreateAsync(client, $"{Prefix} Filter concluida {Guid.NewGuid():N}");
        await client.PostAsJsonAsync($"/api/internal-tasks/{concluida.Id}/status",
            new ChangeInternalTaskStatusRequest(InternalTaskStatus.Concluida));

        var soPendentes = await client.GetFromJsonAsync<List<InternalTaskDto>>($"/api/internal-tasks?status={(int)InternalTaskStatus.Pendente}");
        var soConcluidas = await client.GetFromJsonAsync<List<InternalTaskDto>>($"/api/internal-tasks?status={(int)InternalTaskStatus.Concluida}");

        soPendentes!.Should().Contain(t => t.Id == pendente.Id);
        soPendentes!.Should().NotContain(t => t.Id == concluida.Id);
        soConcluidas!.Should().Contain(t => t.Id == concluida.Id);
        soConcluidas!.Should().NotContain(t => t.Id == pendente.Id);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndHidesFromQueries()
    {
        var client = await NewAuthedClient();
        var created = await CreateAsync(client, $"{Prefix} A apagar");

        var del = await client.DeleteAsync($"/api/internal-tasks/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Get com query filter activo: row escondida.
        var get = await client.GetAsync($"/api/internal-tasks/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Mas em DB persiste com IsDeleted=true (BaseEntity soft delete).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inDb = await db.InternalTasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == created.Id);
        inDb.Should().NotBeNull();
        inDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Anonymous_Cannot_List()
    {
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync("/api/internal-tasks");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- helpers ----------

    private async Task<InternalTaskDto> CreateAsync(HttpClient client, string title)
    {
        var resp = await client.PostAsJsonAsync("/api/internal-tasks",
            new CreateInternalTaskRequest(title, null, null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<InternalTaskDto>())!;
    }

    private async Task<HttpClient> NewAuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
