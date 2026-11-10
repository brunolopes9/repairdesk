using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Conteudo;
using RepairDesk.Services.Conteudo.Redes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Conteudo;

/// <summary>
/// Sprint 608 (Doc 95 Fase B3): YouTube Shorts — textos (título/descrição), erros da API, o
/// publicador nunca envia para um canal diferente do ligado, e o OAuth (cliente Google, URL de ligação).
/// </summary>
public class YouTubeTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public YouTubeTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public void Textos_TituloDaPrimeiraLinha_ComShorts_SemSinaisProibidos()
    {
        var (titulo, descricao) = YouTubePublicador.Textos("A tua PS5 aguenta o <GTA 6>?\n\nVê quanto espaço sobra.", curto: true);

        titulo.Should().Be("A tua PS5 aguenta o GTA 6? #Shorts");
        descricao.Should().NotContain("<").And.NotContain(">").And.Contain("Vê quanto espaço sobra.");
    }

    [Fact]
    public void Textos_TituloLongo_CortaEm100_ESemShortsQuandoNaoCabe()
    {
        var (titulo, _) = YouTubePublicador.Textos(new string('a', 300), curto: true);
        titulo.Length.Should().BeLessThanOrEqualTo(100);
        titulo.Should().EndWith("…");

        var (longo, _) = YouTubePublicador.Textos("Reparação de placa", curto: false);
        longo.Should().Be("Reparação de placa", "vídeos acima de 3 min não são Shorts");
    }

    [Fact]
    public void Textos_DescricaoNoMaximo5000Bytes()
    {
        var (_, descricao) = YouTubePublicador.Textos("título\n" + new string('é', 4000), curto: true);
        Encoding.UTF8.GetByteCount(descricao).Should().BeLessThanOrEqualTo(5000);
    }

    [Fact]
    public void Erros_QuotaETemporaria_401ETokenInvalido()
    {
        var quota = JsonDocument.Parse("""{"error":{"code":403,"message":"quota","errors":[{"reason":"quotaExceeded"}]}}""").RootElement;
        var q = Assert.Throws<RedeSocialException>(() => YouTubeClient.Verificar(quota, 403));
        q.Temporario.Should().BeTrue();

        var auth = JsonDocument.Parse("""{"error":{"code":401,"message":"Invalid Credentials"}}""").RootElement;
        Assert.Throws<RedeSocialException>(() => YouTubeClient.Verificar(auth, 401)).TokenInvalido.Should().BeTrue();
    }

    [Fact]
    public async Task Publicador_OutroCanal_NaoEnvia()
    {
        var http = new HttpFalso("UC-outro", "Bruno Lopes");
        var pub = new YouTubePublicador(new YouTubeClient(new HttpClient(http)));

        var ex = await Assert.ThrowsAsync<RedeSocialException>(() => pub.PublicarAsync(Pedido("UC-lopestech"), CancellationToken.None));

        ex.TokenInvalido.Should().BeTrue("liga-se outra vez e escolhe-se o canal certo");
        ex.Message.Should().Contain("Bruno Lopes");
        http.Uploads.Should().Be(0);
    }

    [Fact]
    public async Task Publicador_CanalCerto_EnviaEDevolveLinkDoShort()
    {
        var http = new HttpFalso("UC-lopestech", "LopesTech");
        var pub = new YouTubePublicador(new YouTubeClient(new HttpClient(http)));

        var r = await pub.PublicarAsync(Pedido("UC-lopestech"), CancellationToken.None);

        r.ExternalId.Should().Be("vid123");
        r.Permalink.Should().Be("https://www.youtube.com/shorts/vid123");
        http.Uploads.Should().Be(1);
        http.Metadados.Should().Contain("#Shorts").And.Contain("\"privacyStatus\":\"public\"").And.Contain("\"selfDeclaredMadeForKids\":false");
    }

    [Fact]
    public async Task Publicador_SoVideos()
    {
        var pub = new YouTubePublicador(new YouTubeClient(new HttpClient(new HttpFalso("x", "x"))));
        var pedido = Pedido("x") with { Formato = ConteudoFormato.Post, AbrirVideo = null };

        await Assert.ThrowsAsync<RedeSocialException>(() => pub.PublicarAsync(pedido, CancellationToken.None));
    }

    [Fact]
    public async Task Apps_ClienteGoogle_ValidaGuardaSemDevolverSecret_EUrlPedeAcessoOffline()
    {
        var client = await AdminClient();

        var mau = await client.PutAsJsonAsync("/api/redes/apps",
            new GuardarAppsRedesRequest(null, null, null, null, YouTubeClientId: "1234", YouTubeClientSecret: "GOCSPX-segredo-do-google-xyz"));
        mau.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var resp = await client.PutAsJsonAsync("/api/redes/apps",
            new GuardarAppsRedesRequest(null, null, null, null,
                YouTubeClientId: "164656-abc.apps.googleusercontent.com", YouTubeClientSecret: "GOCSPX-segredo-do-google-xyz"));
        var body = await resp.Content.ReadAsStringAsync();
        resp.IsSuccessStatusCode.Should().BeTrue(body);
        body.Should().NotContain("GOCSPX-segredo");
        var estado = (await resp.Content.ReadFromJsonAsync<RedesSociaisEstadoDto>())!;
        estado.YouTubeClientSecretDefinido.Should().BeTrue();
        estado.RedirectYouTube.Should().EndWith("/api/redes/youtube/callback");

        var url = (await client.GetFromJsonAsync<UrlLigarResposta>("/api/redes/youtube/url-ligar"))!.Url;
        url.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth?")
            .And.Contain("client_id=164656-abc.apps.googleusercontent.com")
            .And.Contain("access_type=offline")
            .And.Contain("select_account")
            .And.Contain(Uri.EscapeDataString("https://www.googleapis.com/auth/youtube.upload"))
            .And.Contain("state=");
    }

    [Fact]
    public async Task Publicacao_YouTubeSoAceitaReels()
    {
        var client = await AdminClient();
        var imagem = await EnviarImagem(client);
        var req = new GuardarPublicacaoRequest(ConteudoLinha.Reparacoes, ConteudoFormato.Post, "[YT] foto", "Uma foto",
            DateTime.UtcNow.AddDays(1), null, null, new List<Guid> { imagem }, new List<ConteudoDestinoInput> { new(RedeSocial.YouTube, null) }, Submeter: true);

        var resp = await client.PostAsJsonAsync("/api/conteudo", req);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("youtube_formato");
    }

    // ---------- helpers ----------

    private sealed record UrlLigarResposta(string Url);

    private static PedidoPublicacao Pedido(string canal) => new(
        ConteudoFormato.Reel, "A tua PS5 aguenta o GTA 6?\nVê no site.", Array.Empty<MediaPublica>(), canal, "token-yt",
        DuracaoSeg: 16m, AbrirVideo: _ => Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3, 4 })));

    /// <summary>Responde como a YouTube Data API: canal da conta, sessão de upload e vídeo criado.</summary>
    private sealed class HttpFalso(string canalId, string canalNome) : HttpMessageHandler
    {
        public int Uploads { get; private set; }
        public string Metadados { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var url = req.RequestUri!.ToString();
            if (url.StartsWith(YouTubeClient.Api + "/channels"))
                return Json(new { items = new[] { new { id = canalId, snippet = new { title = canalNome } } } });
            if (url.StartsWith(YouTubeClient.Upload) && req.Method == HttpMethod.Post)
            {
                Metadados = await req.Content!.ReadAsStringAsync(ct);
                var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
                r.Headers.Location = new Uri("https://upload.falso/sessao/1");
                return r;
            }
            if (url == "https://upload.falso/sessao/1" && req.Method == HttpMethod.Put)
            {
                Uploads++;
                return Json(new { id = "vid123" });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        }

        private static HttpResponseMessage Json(object o) => new(HttpStatusCode.OK) { Content = JsonContent.Create(o) };
    }

    private async Task<Guid> EnviarImagem(HttpClient client)
    {
        var ticket = (await (await client.PostAsJsonAsync("/api/conteudo/ficheiros/upload",
            new PedirUploadRequest("foto.png", "image/png", 70, null))).Content.ReadFromJsonAsync<UploadTicketDto>())!;
        var png = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await _factory.CreateClient().PutAsync(ticket.UploadUrl, png)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{ticket.FicheiroId}/confirmar",
            new ConfirmarUploadRequest(1080, 1350, null))).EnsureSuccessStatusCode();
        return ticket.FicheiroId;
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
}
