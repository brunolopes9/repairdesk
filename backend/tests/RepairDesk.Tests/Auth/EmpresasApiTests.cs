using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Controllers;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Auth;

/// <summary>Sprint 604: só o dono da plataforma cria empresas; cada uma nasce com um admin próprio sem palavra-passe.</summary>
public class EmpresasApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public EmpresasApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dono_CriaEmpresa_ComAdminProprioSemPassword()
    {
        var client = await Login(RepairDeskApiFactory.AdminEmail);
        var email = $"cortex+{Guid.NewGuid():N}@test.local";

        var resp = await client.PostAsJsonAsync("/api/plataforma/empresas", new NovaEmpresaRequest("Cortex Automation", email));

        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var dto = (await resp.Content.ReadFromJsonAsync<NovaEmpresaDto>())!;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == email);
        user.TenantId.Should().Be(dto.TenantId);
        user.PasswordHash.Should().BeNull("a palavra-passe é escolhida pela pessoa no link do email");
        var roles = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().GetRolesAsync(user);
        roles.Should().Contain("Admin");
        (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == dto.TenantId && t.Name == "Cortex Automation")).Should().BeTrue();

        // O mesmo email não pode servir duas contas.
        (await client.PostAsJsonAsync("/api/plataforma/empresas", new NovaEmpresaRequest("Outra", email)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OutraEmpresa_NaoCriaEmpresas()
    {
        var client = await Login(RepairDeskApiFactory.SecondAdminEmail);

        var resp = await client.PostAsJsonAsync("/api/plataforma/empresas", new NovaEmpresaRequest("Intrusa", "x@test.local"));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Sprint 606: a mesma conta trabalha em duas empresas e troca entre elas.
    [Fact]
    public async Task MesmaConta_CriaEmpresa_TrocaEMantemNaSessao()
    {
        var client = await Login(RepairDeskApiFactory.AdminEmail);
        var criada = await client.PostAsJsonAsync("/api/plataforma/empresas", new NovaEmpresaRequest("Cortex Teste"));
        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
        var cortex = (await criada.Content.ReadFromJsonAsync<NovaEmpresaDto>())!;

        var lista = await client.GetFromJsonAsync<List<EmpresaDisponivelDto>>("/api/auth/empresas");
        lista!.Select(e => e.Id).Should().Contain(new[] { RepairDeskApiFactory.TenantId, cortex.TenantId });
        lista.Single(e => e.Principal).Id.Should().Be(RepairDeskApiFactory.TenantId);

        var ativar = await client.PostAsync($"/api/auth/empresas/{cortex.TenantId}/ativar", null);
        ativar.StatusCode.Should().Be(HttpStatusCode.OK, await ativar.Content.ReadAsStringAsync());
        var sessao = (await ativar.Content.ReadFromJsonAsync<AuthResponse>())!;
        sessao.User.TenantId.Should().Be(cortex.TenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);

        // O dono continua a existir para o Identity dentro da Cortex, e os dados são os da Cortex.
        (await client.GetFromJsonAsync<UserInfo>("/api/auth/me"))!.TenantId.Should().Be(cortex.TenantId);
        var conteudo = await client.GetAsync("/api/conteudo");
        conteudo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await conteudo.Content.ReadAsStringAsync()).Should().Be("[]");

        // Renovar a sessão (cookie) mantém a empresa ativa.
        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK, await refresh.Content.ReadAsStringAsync());
        (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!.User.TenantId.Should().Be(cortex.TenantId);

        // Outra empresa não entra na Cortex.
        var outro = await Login(RepairDeskApiFactory.SecondAdminEmail);
        (await outro.PostAsync($"/api/auth/empresas/{cortex.TenantId}/ativar", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> Login(string email)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
