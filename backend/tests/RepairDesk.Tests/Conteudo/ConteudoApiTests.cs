using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Conteudo;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Conteudo;

/// <summary>
/// Sprint 598 (Doc 95): regras com risco do módulo Conteúdo — ciclo de aprovação, a API key do
/// Claude nunca aprova, validação por formato/rede, auto-aprovação só para a promo de um artigo,
/// estado por rede e upload pelo proxy assinado (storage local nos testes).
/// </summary>
public class ConteudoApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public ConteudoApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private static readonly DateTime Amanha = DateTime.UtcNow.Date.AddDays(1).AddHours(10);

    [Fact]
    public async Task Create_Rascunho_AtribuiNumeroVisivel()
    {
        var client = await AdminClient();

        var a = await Criar(client, Texto("Rascunho A"));
        var b = await Criar(client, Texto("Rascunho B"));

        a.Estado.Should().Be(ConteudoEstado.Rascunho);
        a.Numero.Should().BeGreaterThan(0);
        b.Numero.Should().Be(a.Numero + 1);
    }

    [Fact]
    public async Task Submeter_SemHora_Recusa()
    {
        var client = await AdminClient();
        var p = await Criar(client, Texto("Sem hora") with { AgendadoPara = null });

        var resp = await client.PostAsync($"/api/conteudo/{p.Id}/submeter", null);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Texto_ForaDoThreads_Recusa()
    {
        var client = await AdminClient();
        var req = Texto("Texto no FB") with
        {
            Destinos = new[] { new ConteudoDestinoInput(RedeSocial.Facebook, null) },
            Submeter = true,
        };

        var resp = await client.PostAsJsonAsync("/api/conteudo", req);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Threads_LegendaAcimaDe500_Recusa()
    {
        var client = await AdminClient();
        var req = Texto("Longo") with { Legenda = new string('a', 501), Submeter = true };

        var resp = await client.PostAsJsonAsync("/api/conteudo", req);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_ComImagemEnviada_CicloCompletoDeAprovacao()
    {
        var client = await AdminClient();
        var img = await EnviarImagem(client);
        img.Estado.Should().Be(ConteudoFicheiroEstado.Pronto);
        img.Url.Should().NotBeNullOrWhiteSpace();

        var p = await Criar(client, PostFbIg("Promo capas", img.Id) with { Submeter = true });
        p.Estado.Should().Be(ConteudoEstado.PorAprovar);
        p.Ficheiros.Should().ContainSingle(f => f.Id == img.Id);
        p.Destinos.Select(d => d.Rede).Should().BeEquivalentTo(new[] { RedeSocial.Facebook, RedeSocial.Instagram });

        var aprovada = await Ok(await client.PostAsync($"/api/conteudo/{p.Id}/aprovar", null));
        aprovada.Estado.Should().Be(ConteudoEstado.Aprovada);
        aprovada.AprovadoEm.Should().NotBeNull();

        // Editar depois de aprovada obriga a nova aprovação.
        var editada = await Ok(await client.PutAsJsonAsync($"/api/conteudo/{p.Id}",
            PostFbIg("Promo capas v2", img.Id)));
        editada.Estado.Should().Be(ConteudoEstado.PorAprovar);
        editada.AprovadoEm.Should().BeNull();

        var rejeitada = await Ok(await client.PostAsJsonAsync($"/api/conteudo/{p.Id}/rejeitar",
            new RejeitarPublicacaoRequest("foto cortada")));
        rejeitada.Estado.Should().Be(ConteudoEstado.Rejeitada);
        rejeitada.MotivoRejeicao.Should().Be("foto cortada");
    }

    [Fact]
    public async Task Reel_ComImagem_Recusa()
    {
        var client = await AdminClient();
        var img = await EnviarImagem(client);
        var req = PostFbIg("Reel errado", img.Id) with { Formato = ConteudoFormato.Reel, Submeter = true };

        var resp = await client.PostAsJsonAsync("/api/conteudo", req);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Destinos_TodosAgendadosNaRede_PublicacaoFicaPublicada()
    {
        var client = await AdminClient();
        var img = await EnviarImagem(client);
        var p = await Criar(client, PostFbIg("Agendar fora", img.Id) with { Submeter = true });

        // Antes de aprovada não se regista publicação.
        var cedo = await client.PutAsJsonAsync($"/api/conteudo/{p.Id}/destinos/{(int)RedeSocial.Facebook}",
            new AtualizarDestinoRequest(ConteudoDestinoEstado.AgendadoNaRede, null, null, null));
        cedo.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await Ok(await client.PostAsync($"/api/conteudo/{p.Id}/aprovar", null));
        await Ok(await client.PutAsJsonAsync($"/api/conteudo/{p.Id}/destinos/{(int)RedeSocial.Facebook}",
            new AtualizarDestinoRequest(ConteudoDestinoEstado.AgendadoNaRede, null, null, null)));
        var final = await Ok(await client.PutAsJsonAsync($"/api/conteudo/{p.Id}/destinos/{(int)RedeSocial.Instagram}",
            new AtualizarDestinoRequest(ConteudoDestinoEstado.Publicado, "1789", "https://instagram.com/p/x", null)));

        final.Estado.Should().Be(ConteudoEstado.Publicada);
        final.PublicadaEm.Should().NotBeNull();

        // Publicada não se apaga nem se cancela.
        (await client.DeleteAsync($"/api/conteudo/{p.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsync($"/api/conteudo/{p.Id}/cancelar", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AutoAprovada_SoParaPromoDeArtigo()
    {
        var client = await AdminClient();
        var img = await EnviarImagem(client);

        var semArtigo = await client.PostAsJsonAsync("/api/conteudo", PostFbIg("Promo", img.Id) with { AutoAprovada = true });
        semArtigo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var promo = await Criar(client, PostFbIg("Promo powerbank", img.Id) with { AutoAprovada = true, ArtigoId = Guid.NewGuid() });
        promo.Estado.Should().Be(ConteudoEstado.Aprovada);
        promo.AutoAprovada.Should().BeTrue();
    }

    [Fact]
    public async Task ApiKey_CriaESubmete_MasNuncaAprova()
    {
        var admin = await AdminClient();
        var claude = await ApiKeyClient(admin, "conteudo");

        var resp = await claude.PostAsJsonAsync("/api/external/conteudo", Texto("Do Claude") with { Submeter = true });
        var p = await Ok(resp);
        p.Estado.Should().Be(ConteudoEstado.PorAprovar);
        p.Origem.Should().Be(ConteudoOrigem.Claude);

        (await claude.PostAsync($"/api/conteudo/{p.Id}/aprovar", null)).StatusCode
            .Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await claude.PostAsync($"/api/external/conteudo/{p.Id}/aprovar", null)).IsSuccessStatusCode.Should().BeFalse();

        var depois = await Ok(await admin.GetAsync($"/api/conteudo/{p.Id}"));
        depois.Estado.Should().Be(ConteudoEstado.PorAprovar);
    }

    [Fact]
    public async Task ApiKey_SemScopeConteudo_NaoEntra()
    {
        var admin = await AdminClient();
        var outra = await ApiKeyClient(admin, "read");

        var resp = await outra.GetAsync("/api/external/conteudo");

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Biblioteca_MostraSoFicheirosSoltos()
    {
        var client = await AdminClient();
        var solto = await EnviarImagem(client);
        var usado = await EnviarImagem(client);
        await Criar(client, PostFbIg("Usa um", usado.Id));

        var lista = await (await client.GetAsync("/api/conteudo/biblioteca")).Content.ReadFromJsonAsync<List<ConteudoFicheiroDto>>();

        lista!.Select(f => f.Id).Should().Contain(solto.Id).And.NotContain(usado.Id);
    }

    [Fact]
    public async Task Upload_TipoNaoSuportado_Recusa()
    {
        var client = await AdminClient();

        var resp = await client.PostAsJsonAsync("/api/conteudo/ficheiros/upload",
            new PedirUploadRequest("x.exe", "application/octet-stream", 10, null));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Binario_AssinaturaInvalida_Recusa()
    {
        var client = await AdminClient();
        var ticket = await PedirUpload(client);
        var adulterado = ticket.UploadUrl.Replace("sig=", "sig=x");

        var resp = await _factory.CreateClient().PutAsync(adulterado, Png());

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static GuardarPublicacaoRequest Texto(string titulo) => new(
        ConteudoLinha.Reparacoes, ConteudoFormato.Texto, $"[CNT] {titulo}", "Qual é o telemóvel mais antigo que ainda usas?",
        Amanha, null, null, null, new[] { new ConteudoDestinoInput(RedeSocial.Threads, null) });

    private static GuardarPublicacaoRequest PostFbIg(string titulo, Guid ficheiroId) => new(
        ConteudoLinha.Reparacoes, ConteudoFormato.Post, $"[CNT] {titulo}", "Capas desde 9,90 €",
        Amanha, null, null, new[] { ficheiroId },
        new[] { new ConteudoDestinoInput(RedeSocial.Facebook, null), new ConteudoDestinoInput(RedeSocial.Instagram, null) });

    private static async Task<ConteudoPublicacaoDto> Criar(HttpClient client, GuardarPublicacaoRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/conteudo", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<ConteudoPublicacaoDto>())!;
    }

    private static async Task<ConteudoPublicacaoDto> Ok(HttpResponseMessage resp)
    {
        resp.IsSuccessStatusCode.Should().BeTrue(await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<ConteudoPublicacaoDto>())!;
    }

    private static ByteArrayContent Png()
    {
        // PNG 1×1 válido.
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return content;
    }

    private static async Task<UploadTicketDto> PedirUpload(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/conteudo/ficheiros/upload",
            new PedirUploadRequest("slide.png", "image/png", 70, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<UploadTicketDto>())!;
    }

    private async Task<ConteudoFicheiroDto> EnviarImagem(HttpClient client)
    {
        var ticket = await PedirUpload(client);
        // O PUT vai sem JWT, como faria o browser — a autorização é a assinatura.
        var put = await _factory.CreateClient().PutAsync(ticket.UploadUrl, Png());
        put.StatusCode.Should().Be(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync());
        var conf = await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{ticket.FicheiroId}/confirmar",
            new ConfirmarUploadRequest(1080, 1350, null));
        conf.EnsureSuccessStatusCode();
        return (await conf.Content.ReadFromJsonAsync<ConteudoFicheiroDto>())!;
    }

    private async Task<HttpClient> AdminClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> ApiKeyClient(HttpClient admin, string scope)
    {
        var resp = await admin.PostAsJsonAsync("/api/service-keys",
            new CreateServiceApiKeyRequest($"[CNT] chave {scope} {Guid.NewGuid():N}", new[] { scope }));
        resp.EnsureSuccessStatusCode();
        var created = (await resp.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", created.PlainKey);
        return client;
    }
}
