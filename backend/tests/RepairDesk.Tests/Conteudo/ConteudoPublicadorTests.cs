using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Conteudo;
using RepairDesk.Services.Conteudo.Redes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Conteudo;

/// <summary>
/// Sprint 599 (Doc 95 Fase B): o publicador automático com redes falsas — interruptor desligado
/// não publica, publica à hora, nunca republica (ExternalId / AgendadoNaRede), backoff em falhas
/// temporárias, falha definitiva e token inválido. Mais o state assinado do OAuth.
/// </summary>
public class ConteudoPublicadorTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly WebApplicationFactory<Program> _app;
    private readonly RedeFalsa _fb = new(RedeSocial.Facebook);
    private readonly RedeFalsa _ig = new(RedeSocial.Instagram);
    private readonly RedeFalsa _th = new(RedeSocial.Threads);

    public ConteudoPublicadorTests(RepairDeskApiFactory factory)
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IPublicadorRede>();
            s.AddSingleton<IPublicadorRede>(_fb);
            s.AddSingleton<IPublicadorRede>(_ig);
            s.AddSingleton<IPublicadorRede>(_th);
            s.AddSingleton<IDirectAccessStorage, StorageFalso>();
        }));
    }

    [Fact]
    public async Task AutoPublicarDesligado_NaoPublica()
    {
        var client = await AdminClient();
        await Preparar(autoPublicar: false);
        var p = await CriarAprovada(client, RedeSocial.Facebook);

        await Correr();

        _fb.Chamadas.Should().NotContain(p.Titulo);
        (await Ler(client, p.Id)).Estado.Should().Be(ConteudoEstado.Aprovada);
    }

    [Fact]
    public async Task Ligado_PublicaNasRedes_EFicaPublicada()
    {
        var client = await AdminClient();
        await Preparar(autoPublicar: true);
        var p = await CriarAprovada(client, RedeSocial.Facebook, RedeSocial.Instagram);

        await Correr();
        await Correr(); // segunda volta não pode republicar

        _fb.Chamadas.Count(t => t == p.Titulo).Should().Be(1);
        _ig.Chamadas.Count(t => t == p.Titulo).Should().Be(1);
        var depois = await Ler(client, p.Id);
        depois.Estado.Should().Be(ConteudoEstado.Publicada);
        depois.Destinos.Should().OnlyContain(d => d.Estado == ConteudoDestinoEstado.Publicado && d.ExternalId != null);
    }

    [Fact]
    public async Task AgendadoNaRede_NaoRepublica_ETikTokFicaManual()
    {
        var client = await AdminClient();
        await Preparar(autoPublicar: true);
        var p = await CriarAprovada(client, RedeSocial.Facebook, RedeSocial.Instagram, RedeSocial.TikTok, formato: ConteudoFormato.Carrossel);
        (await client.PutAsJsonAsync($"/api/conteudo/{p.Id}/destinos/{(int)RedeSocial.Facebook}",
            new AtualizarDestinoRequest(ConteudoDestinoEstado.AgendadoNaRede, null, null, null))).EnsureSuccessStatusCode();

        await Correr();

        _fb.Chamadas.Should().NotContain(p.Titulo);
        _ig.Chamadas.Should().Contain(p.Titulo);
        var depois = await Ler(client, p.Id);
        depois.Destinos.Single(d => d.Rede == RedeSocial.TikTok).Estado.Should().Be(ConteudoDestinoEstado.Pendente);
        depois.Estado.Should().Be(ConteudoEstado.Aprovada, "o TikTok ainda está por publicar à mão");
    }

    [Fact]
    public async Task FalhaTemporaria_RepeteMaisTarde_FalhaDefinitivaAvisa()
    {
        var client = await AdminClient();
        await Preparar(autoPublicar: true);
        var p = await CriarAprovada(client, RedeSocial.Facebook, RedeSocial.Instagram);
        _fb.Falhar[p.Titulo] = new RedeSocialException("limite de pedidos", 4, temporario: true);
        _ig.Falhar[p.Titulo] = new RedeSocialException("formato de vídeo inválido", 100);

        await Correr();

        var depois = await Ler(client, p.Id);
        var fb = depois.Destinos.Single(d => d.Rede == RedeSocial.Facebook);
        fb.Estado.Should().Be(ConteudoDestinoEstado.Pendente);
        fb.Tentativas.Should().Be(1);
        fb.UltimoErro.Should().Contain("limite");
        var ig = depois.Destinos.Single(d => d.Rede == RedeSocial.Instagram);
        ig.Estado.Should().Be(ConteudoDestinoEstado.Falhou);
        depois.Estado.Should().Be(ConteudoEstado.ComFalhas);

        // Dentro do backoff não volta a tentar.
        _fb.Falhar.Remove(p.Titulo);
        await Correr();
        _fb.Chamadas.Count(t => t == p.Titulo).Should().Be(1);
    }

    [Fact]
    public async Task TokenInvalido_MarcaLigacaoComoInvalida()
    {
        var client = await AdminClient();
        await Preparar(autoPublicar: true);
        var p = await CriarAprovada(client, RedeSocial.Threads, formato: ConteudoFormato.Texto);
        _th.Falhar[p.Titulo] = new RedeSocialException("Error validating access token", 190);

        await Correr();

        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lig = await db.RedeSocialLigacoes.IgnoreQueryFilters().SingleAsync(l => l.TenantId == RepairDeskApiFactory.TenantId && l.Rede == RedeSocial.Threads);
        lig.Valida.Should().BeFalse();
        await Preparar(autoPublicar: true); // repõe para os outros testes
    }

    [Fact]
    public async Task Callback_StateAdulterado_VoltaComErro()
    {
        var anon = _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await anon.GetAsync("/api/redes/meta/callback?code=abc&state=xxx.yyy");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().StartWith("/definicoes/redes-sociais?erro=");
    }

    [Fact]
    public async Task Apps_SecretNuncaVoltaAoFrontend()
    {
        var client = await AdminClient();

        var resp = await client.PutAsJsonAsync("/api/redes/apps",
            new GuardarAppsRedesRequest("1234567890", "segredo-muito-secreto-123", null, null, "555666777"));

        var body = await resp.Content.ReadAsStringAsync();
        resp.IsSuccessStatusCode.Should().BeTrue(body);
        body.Should().NotContain("segredo-muito-secreto");
        var estado = (await resp.Content.ReadFromJsonAsync<RedesSociaisEstadoDto>())!;
        estado.MetaAppSecretDefinido.Should().BeTrue();
        estado.RedirectMeta.Should().EndWith("/api/redes/meta/callback");

        var url = await client.GetFromJsonAsync<UrlLigarResposta>("/api/redes/meta/url-ligar");
        url!.Url.Should().Contain("client_id=1234567890").And.Contain("state=").And.Contain("config_id=555666777").And.NotContain("scope=");
    }

    // ---------- helpers ----------

    private sealed record UrlLigarResposta(string Url);

    private async Task Correr()
    {
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IConteudoPublicadorService>().PublicarPendentesAsync();
    }

    /// <summary>Liga FB/IG/Threads (tokens falsos cifrados) e define o interruptor.</summary>
    private async Task Preparar(bool autoPublicar)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == RepairDeskApiFactory.TenantId);
        tenant.ConteudoAutoPublicar = autoPublicar;
        foreach (var rede in new[] { RedeSocial.Facebook, RedeSocial.Instagram, RedeSocial.Threads })
        {
            var l = await db.RedeSocialLigacoes.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Rede == rede);
            if (l is null)
                db.RedeSocialLigacoes.Add(new RedeSocialLigacao
                {
                    TenantId = tenant.Id, Rede = rede, ContaId = $"conta-{rede}", ContaNome = $"Lopes {rede}",
                    TokenCipherText = secrets.Protect($"token-{rede}"),
                });
            else
                l.Valida = true;
        }
        await db.SaveChangesAsync();
    }

    private async Task<ConteudoPublicacaoDto> CriarAprovada(HttpClient client, params RedeSocial[] redes)
        => await CriarAprovada(client, redes, ConteudoFormato.Post);

    private async Task<ConteudoPublicacaoDto> CriarAprovada(HttpClient client, RedeSocial r1, RedeSocial r2, RedeSocial r3, ConteudoFormato formato)
        => await CriarAprovada(client, new[] { r1, r2, r3 }, formato);

    private async Task<ConteudoPublicacaoDto> CriarAprovada(HttpClient client, RedeSocial r1, ConteudoFormato formato)
        => await CriarAprovada(client, new[] { r1 }, formato);

    private async Task<ConteudoPublicacaoDto> CriarAprovada(HttpClient client, RedeSocial[] redes, ConteudoFormato formato)
    {
        var ficheiros = new List<Guid>();
        var n = formato switch { ConteudoFormato.Texto => 0, ConteudoFormato.Carrossel => 2, _ => 1 };
        for (var i = 0; i < n; i++) ficheiros.Add(await EnviarImagem(client));
        var titulo = $"[PUB] {formato} {Guid.NewGuid():N}";
        var req = new GuardarPublicacaoRequest(ConteudoLinha.Reparacoes, formato, titulo, titulo,
            DateTime.UtcNow.AddMinutes(-1), null, null, ficheiros,
            redes.Select(r => new ConteudoDestinoInput(r, null)).ToList(), Submeter: true);
        var criada = await client.PostAsJsonAsync("/api/conteudo", req);
        criada.EnsureSuccessStatusCode();
        var p = (await criada.Content.ReadFromJsonAsync<ConteudoPublicacaoDto>())!;
        (await client.PostAsync($"/api/conteudo/{p.Id}/aprovar", null)).EnsureSuccessStatusCode();
        return p;
    }

    private async Task<ConteudoPublicacaoDto> Ler(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<ConteudoPublicacaoDto>($"/api/conteudo/{id}"))!;

    /// <summary>Com o storage falso o upload pede um URL «pré-assinado» fictício; regista-se o ficheiro direto na BD.</summary>
    private async Task<Guid> EnviarImagem(HttpClient client)
    {
        var t = await (await client.PostAsJsonAsync("/api/conteudo/ficheiros/upload",
            new PedirUploadRequest("slide.png", "image/png", 70, null))).Content.ReadFromJsonAsync<UploadTicketDto>();
        (await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{t!.FicheiroId}/confirmar",
            new ConfirmarUploadRequest(1080, 1350, null))).EnsureSuccessStatusCode();
        return t.FicheiroId;
    }

    private async Task<HttpClient> AdminClient()
    {
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed class RedeFalsa(RedeSocial rede) : IPublicadorRede
    {
        public RedeSocial Rede => rede;
        public List<string> Chamadas { get; } = new();
        public Dictionary<string, RedeSocialException> Falhar { get; } = new();

        public Task<Publicado> PublicarAsync(PedidoPublicacao p, CancellationToken ct)
        {
            var titulo = p.Legenda ?? "";
            lock (Chamadas) Chamadas.Add(titulo);
            if (Falhar.TryGetValue(titulo, out var ex)) throw ex;
            p.Token.Should().Be($"token-{rede}", "o token chega decifrado ao publicador");
            return Task.FromResult(new Publicado($"{rede}-{Guid.NewGuid():N}", $"https://exemplo/{rede}"));
        }
    }

    private sealed class StorageFalso : IDirectAccessStorage
    {
        public string CreateUploadUrl(string key, string contentType, TimeSpan validFor) => $"https://r2.falso/{key}?put";
        public string CreateDownloadUrl(string key, TimeSpan validFor, string? downloadFileName = null) => $"https://r2.falso/{key}";
        public Task<long?> GetSizeAsync(string key, CancellationToken ct = default) => Task.FromResult<long?>(70);
        public Task EnsureBrowserCorsAsync(IReadOnlyCollection<string> origins, CancellationToken ct = default) => Task.CompletedTask;
    }
}
