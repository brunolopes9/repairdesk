using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Controllers;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Fornecedores;

/// <summary>Sprint 579: fornecedores identificados pelo NIF — leitura nas faturas, unicidade e fusão de repetidos.</summary>
public class FornecedorNifTests : IClassFixture<RepairDeskApiFactory>
{
    // NIFs sintéticos com dígito de controlo válido.
    private const string NifProprio = "PT123456789";
    private readonly RepairDeskApiFactory _factory;
    public FornecedorNifTests(RepairDeskApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("PT500000000", "500000000")]
    [InlineData("de 811 234 567", "811234567")]
    [InlineData("NL861081961B01", "861081961B01")]
    [InlineData("500000000", "500000000")]
    public void Chave_IgnoraPrefixoDoPaisEFormatacao(string nif, string chave)
        => NifFornecedor.Chave(nif).Should().Be(chave);

    [Fact]
    public void Encontrar_PeloNifNoTexto_IgnorandoONifDoCliente()
    {
        var hetzner = new Fornecedor { Name = "Hetzner Online GmbH", Nif = "DE811234567" };
        var outro = new Fornecedor { Name = "Outro", Nif = "PT500000000" };
        var texto = "Hetzner Online GmbH · USt-IdNr. DE 811 234 567 · Cliente: Loja, NIF PT123456789";

        NifFornecedor.Encontrar([hetzner, outro], texto, NifProprio).Should().BeSameAs(hetzner);
        // Um fornecedor com o NIF do próprio negócio nunca é escolhido (é o cliente na fatura).
        NifFornecedor.Encontrar([new Fornecedor { Name = "Eu", Nif = "123456789" }], texto, NifProprio).Should().BeNull();
        // NIF dentro de um número maior (IBAN, referência) não conta.
        NifFornecedor.Encontrar([outro], "Ref 95000000001", NifProprio).Should().BeNull();
    }

    [Fact]
    public void DoEmitente_SoComUmCandidatoAlemDoCliente()
    {
        NifFornecedor.DoEmitente("POCINHAS SARL NIF : LU26920569 ... Cliente NIF 123456789", NifProprio).Should().Be("LU26920569");
        NifFornecedor.DoEmitente("Leroy Merlin · Contribuinte: 500000000 · Cliente PT123456789", NifProprio).Should().Be("PT500000000");
        // Dois emitentes possíveis (marketplace + vendedor) → não adivinha.
        NifFornecedor.DoEmitente("VAT DE811234567 sold by seller VAT LU26920569", NifProprio).Should().BeNull();
        // Palavras em maiúsculas não são NIFs.
        NifFornecedor.DoEmitente("DESCRIPTION QUANTITY", NifProprio).Should().BeNull();
    }

    [Fact]
    public async Task Nif_RepetidoNoutroFornecedor_Recusa()
    {
        var client = await AuthedClient();
        var nif = NifUnico();
        (await Criar(client, $"Forn {Guid.NewGuid():N}", "PT" + nif)).StatusCode.Should().Be(HttpStatusCode.OK);

        var repetido = await Criar(client, $"Forn {Guid.NewGuid():N}", nif);
        repetido.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await repetido.Content.ReadAsStringAsync()).Should().Contain("nif_ja_existe");
    }

    [Fact]
    public async Task Fundir_PassaComprasEDespesasParaODestino()
    {
        var client = await AuthedClient();
        var sufixo = Guid.NewGuid().ToString("N")[..6];
        var destino = (await (await Criar(client, $"Hetzner {sufixo}", null)).Content.ReadFromJsonAsync<FornecedorDto>())!;
        var origem = (await (await Criar(client, $"Hetzner Online GmbH {sufixo}", "DE" + NifUnico())).Content.ReadFromJsonAsync<FornecedorDto>())!;

        var compra = await client.PostAsJsonAsync("/api/compras", new CompraDocumentoWriteRequest(origem.Id, new DateTime(2026, 10, 4), "INV-" + sufixo, null, null, 0m, null, null, null,
            [new CompraLinhaWriteRequest(null, "Peça teste", 1, 10m, null, null, null)]));
        compra.StatusCode.Should().Be(HttpStatusCode.Created);
        var compraId = (await compra.Content.ReadFromJsonAsync<CompraDocumentoDto>())!.Id;
        var despesa = await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest("Servidor", DespesaCategoria.Software, 1449, new DateTime(2026, 10, 4), origem.Name, "0860" + sufixo, null));
        despesa.EnsureSuccessStatusCode();
        var despesaId = (await despesa.Content.ReadFromJsonAsync<DespesaDto>())!.Id;

        var resp = await client.PostAsJsonAsync($"/api/fornecedores/{destino.Id}/fundir", new FundirFornecedorRequest(origem.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var junto = (await resp.Content.ReadFromJsonAsync<FornecedorDto>())!;
        junto.Nif.Should().Be(origem.Nif, "o destino fica com o NIF que lhe faltava");

        var lista = (await client.GetFromJsonAsync<List<FornecedorDto>>("/api/fornecedores?includeInactive=true"))!;
        lista.Should().NotContain(f => f.Id == origem.Id);
        (await client.GetFromJsonAsync<CompraDocumentoDto>($"/api/compras/{compraId}"))!.FornecedorId.Should().Be(destino.Id);
        (await client.GetFromJsonAsync<DespesaDto>($"/api/despesas/{despesaId}"))!.Fornecedor.Should().Be(destino.Name);
    }

    [Fact]
    public async Task Fundir_NifsDiferentes_Recusa()
    {
        var client = await AuthedClient();
        var a = (await (await Criar(client, $"Forn {Guid.NewGuid():N}", "DE" + NifUnico())).Content.ReadFromJsonAsync<FornecedorDto>())!;
        var b = (await (await Criar(client, $"Forn {Guid.NewGuid():N}", "FR" + NifUnico())).Content.ReadFromJsonAsync<FornecedorDto>())!;

        var resp = await client.PostAsJsonAsync($"/api/fornecedores/{a.Id}/fundir", new FundirFornecedorRequest(b.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("nif_diferente");
    }

    // ---------- helpers ----------

    private static string NifUnico() => Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();

    private static Task<HttpResponseMessage> Criar(HttpClient client, string nome, string? nif)
        => client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest(nome, null, null, null, null, null, null, true, false, RegimeIvaFornecedor.UeAutoliquidacao, "DE", nif));

    private async Task<HttpClient> AuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
