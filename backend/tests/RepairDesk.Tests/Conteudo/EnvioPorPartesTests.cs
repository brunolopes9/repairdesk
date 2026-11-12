using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Conteudo;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Conteudo;

/// <summary>
/// Sprint 638: vídeos grandes do telemóvel sobem por partes — abre o envio, dá URLs aos lotes, só
/// completa com todas as partes, confirmar é idempotente e apagar a meio cancela o envio no R2.
/// </summary>
public class EnvioPorPartesTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly WebApplicationFactory<Program> _app;
    private readonly StoragePartesFalso _r2 = new();

    public EnvioPorPartesTests(RepairDeskApiFactory factory)
    {
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IDirectAccessStorage>(_r2)));
    }

    [Fact]
    public async Task VideoGrande_PorPartes_CompletaSoComTodas()
    {
        var client = await Admin();
        var bytes = 6L * 1024 * 1024 * 1024 + 5; // 6 GB: antes era recusado (limite 5 GB)
        var t = await Pedir(client, "ps5-limpeza.mov", bytes);
        t.Metodo.Should().Be("MULTIPART");
        t.TamanhoParte.Should().Be(ConteudoService.TamanhoParte);
        var total = (int)((bytes + ConteudoService.TamanhoParte - 1) / ConteudoService.TamanhoParte);
        t.Partes.Should().Be(total);

        var urls = await (await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{t.FicheiroId}/partes", new PedirPartesRequest(1, 100)))
            .Content.ReadFromJsonAsync<List<UrlParteDto>>();
        urls!.Select(u => u.Numero).Should().Equal(Enumerable.Range(1, 100));
        (await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{t.FicheiroId}/partes", new PedirPartesRequest(1, 101)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Falta uma parte → não completa.
        var quase = Enumerable.Range(1, total - 1).Select(n => new ParteEnviada(n, $"\"e{n}\"")).ToList();
        (await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{t.FicheiroId}/confirmar", new ConfirmarUploadRequest(null, null, null, quase)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        _r2.Completos.Should().BeEmpty();

        var todas = quase.Append(new ParteEnviada(total, $"\"e{total}\"")).ToList();
        var ok = await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{t.FicheiroId}/confirmar", new ConfirmarUploadRequest(1920, 1080, 1200m, todas));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        _r2.Completos.Should().ContainSingle().Which.Should().Be(total);

        // Repetir o confirmar (resposta perdida na rede) não volta a completar.
        (await client.PostAsJsonAsync($"/api/conteudo/ficheiros/{t.FicheiroId}/confirmar", new ConfirmarUploadRequest(null, null, null, todas)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        _r2.Completos.Should().ContainSingle();

        var lista = await client.GetFromJsonAsync<List<ConteudoFicheiroDto>>("/api/conteudo/biblioteca");
        lista!.Should().Contain(f => f.Id == t.FicheiroId && f.Estado == ConteudoFicheiroEstado.Pronto);
    }

    [Fact]
    public async Task Pequeno_UmSoPut_EApagarAMeioCancelaPartes()
    {
        var client = await Admin();
        (await Pedir(client, "foto.jpg", 3 * 1024 * 1024, "image/jpeg")).Metodo.Should().Be("PUT");

        var t = await Pedir(client, "longo.mov", 200L * 1024 * 1024);
        (await client.DeleteAsync($"/api/conteudo/ficheiros/{t.FicheiroId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _r2.Cancelados.Should().Contain(t.FicheiroId.ToString("N"));
    }

    private async Task<UploadTicketDto> Pedir(HttpClient client, string nome, long bytes, string tipo = "video/quicktime")
    {
        var r = await client.PostAsJsonAsync("/api/conteudo/ficheiros/upload", new PedirUploadRequest(nome, tipo, bytes, null));
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<UploadTicketDto>())!;
    }

    private async Task<HttpClient> Admin()
    {
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed class StoragePartesFalso : IDirectAccessStorage, IMultipartUploadStorage
    {
        private readonly ConcurrentDictionary<string, long> _objetos = new();
        public ConcurrentBag<int> Completos { get; } = new();
        public ConcurrentBag<string> Cancelados { get; } = new();

        public string CreateUploadUrl(string key, string contentType, TimeSpan validFor) => $"https://r2.falso/{key}?put";
        public string CreateDownloadUrl(string key, TimeSpan validFor, string? downloadFileName = null) => $"https://r2.falso/{key}";
        public Task<long?> GetSizeAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_objetos.TryGetValue(key, out var t) ? (long?)t : key.EndsWith(".jpg") ? 100 : null);
        public Task EnsureBrowserCorsAsync(IReadOnlyCollection<string> origins, CancellationToken ct = default) => Task.CompletedTask;

        public Task<string> StartMultipartUploadAsync(string key, string contentType, CancellationToken ct = default)
            => Task.FromResult("up-" + Guid.NewGuid().ToString("N"));
        public string CreatePartUploadUrl(string key, string uploadId, int partNumber, TimeSpan validFor)
            => $"https://r2.falso/{key}?uploadId={uploadId}&partNumber={partNumber}";
        public Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyList<MultipartPart> parts, CancellationToken ct = default)
        {
            Completos.Add(parts.Count);
            _objetos[key] = parts.Count * ConteudoService.TamanhoParte;
            return Task.CompletedTask;
        }
        public Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken ct = default)
        {
            Cancelados.Add(Path.GetFileNameWithoutExtension(key));
            return Task.CompletedTask;
        }
    }
}
