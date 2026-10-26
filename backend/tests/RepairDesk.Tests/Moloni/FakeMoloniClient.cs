using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RepairDesk.Services.Moloni;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Moloni;

/// <summary>Factory com o Moloni ON simulado (nenhum teste fala com o Moloni real).</summary>
public class MoloniApiFactory : RepairDeskApiFactory
{
    public FakeMoloniClient Moloni { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IMoloniClient>();
            s.AddSingleton<IMoloniClient>(Moloni);
        });
        return base.CreateHost(builder);
    }
}

/// <summary>
/// Moloni ON a fingir: responde às operações que o Mender usa, calcula os totais como o Moloni (preço sem IVA
/// × qtd + IVA) e regista cada chamada para os testes verificarem a ordem (ex.: recibo anulado antes da fatura).
/// </summary>
public class FakeMoloniClient : IMoloniClient
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private int _seq = 1000;
    private readonly ConcurrentDictionary<int, decimal> _totais = new();

    public ConcurrentQueue<(string Operacao, JsonNode? Variaveis)> Chamadas { get; } = new();
    /// <summary>Se definido, o Moloni "calcula" este total nos rascunhos (para testar a diferença de totais).</summary>
    public decimal? TotalForcado { get; set; }

    public IEnumerable<string> Operacoes => Chamadas.Select(c => c.Operacao);

    public Task<JsonNode?> ExecutarAsync(string apiKey, string query, object? variaveis, string operacao, CancellationToken ct = default)
        => Task.FromResult(Responder(operacao, query, variaveis));

    public Task<JsonNode?> ExecutarEscalarAsync(string apiKey, string query, object? variaveis, string operacao, CancellationToken ct = default)
    {
        var r = Responder(operacao, query, variaveis);
        // Listas pedidas com paginação vêm embrulhadas como no Moloni: { errors, data, options }.
        if (query.Contains("options { pagination") && r is JsonArray arr)
            return Task.FromResult<JsonNode?>(new JsonObject { ["errors"] = new JsonArray(), ["data"] = arr, ["options"] = new JsonObject { ["pagination"] = new JsonObject { ["count"] = arr.Count } } });
        if (operacao.EndsWith("Delete") && r is JsonObject) return Task.FromResult(r);
        return Task.FromResult(r);
    }

    /// <summary>Documentos que as listas devolvem (documentId → linha).</summary>
    public List<object> Faturas { get; } = new();
    /// <summary>Clientes que existem "no Moloni" para a importação.</summary>
    public List<object> Clientes { get; } = new();
    /// <summary>Artigos que o Moloni recusa apagar (usados em documentos).</summary>
    public HashSet<int> ArtigosEmUso { get; } = new();

    public Task<byte[]> DescarregarAsync(string path, string token, CancellationToken ct = default)
        => Task.FromResult("%PDF-1.4 fake moloni"u8.ToArray());

    private JsonNode? Responder(string op, string query, object? variaveis)
    {
        var v = variaveis is null ? null : JsonSerializer.SerializeToNode(variaveis, Web);
        Chamadas.Enqueue((op, v));
        var d = v?["d"];
        int Id() => Interlocked.Increment(ref _seq);
        switch (op)
        {
            case "me": return Node(new { userCompanies = new[] { new { companyId = 2855 } } });
            case "documentSetsForDocument":
                return Node(new object[] { new { documentSetId = 3216, name = "SOFTWARE", isDefault = true }, new { documentSetId = 3413, name = "REPARACAO", isDefault = false } });
            case "taxes":
                return Node(new object[] { new { taxId = 12733, value = 23, type = 1 }, new { taxId = 12734, value = 13, type = 1 }, new { taxId = 12735, value = 6, type = 1 } });
            case "paymentMethods": return Node(new object[] { new { paymentMethodId = 8903, name = "Dinheiro" }, new { paymentMethodId = 8904, name = "Cartão" } });
            case "measurementUnits": return Node(new object[] { new { measurementUnitId = 14518, name = "Unidade", abbreviation = "un" } });
            case "productCategories": return Node(new object[] { new { productCategoryId = 10398, name = "Predefinida" } });
            case "customers":
                if (query.Contains("options { pagination")) return Node(Clientes.ToArray());
                return query.Contains("isDefault:") || query.Contains("field:isDefault") ? Node(new object[] { new { customerId = 63270 } }) : new JsonArray();
            case "customerNextNumber": return JsonValue.Create("9");
            case "customerCreate": return Node(new { customerId = Id() });
            case "customerUpdate": return Node(new { customerId = d?["customerId"]?.GetValue<int>() });
            case "products": return new JsonArray();
            case "productCreate" or "productUpdate":
                return Node(new { productId = d!["productId"]?.GetValue<int>() ?? Id(), reference = d["reference"]?.GetValue<string>(), name = d["name"]?.GetValue<string>(), type = d["type"]?.GetValue<int>() ?? 2, price = d["price"]?.GetValue<decimal>() ?? 0m, priceWithTaxes = 0m, taxes = new object[0], deletionBlockers = new string[0] });
            case "productDelete":
                return Node(new { status = true, deletedCount = v!["d"]!.AsArray().Any(x => ArtigosEmUso.Contains(x!.GetValue<int>())) ? 0 : 1, errors = new object[0] });
            case "invoices": return Node(Faturas.ToArray());
            case "invoiceReceipts" or "receipts" or "estimates": return new JsonArray();
            case "paymentMethodCreate": return Node(new { paymentMethodId = Id() });
            case "invoiceCreate" or "invoiceReceiptCreate" or "estimateCreate":
            {
                var total = TotalForcado ?? Total(d!["products"]!.AsArray());
                var id = Id();
                _totais[id] = total;
                return Node(new { documentId = id, totalValue = total });
            }
            case "invoiceUpdate" or "invoiceReceiptUpdate" or "estimateUpdate":
                return Node(new { documentId = d!["documentId"]!.GetValue<int>(), number = 7, documentSetName = "SOFTWARE", status = 1 });
            case "invoice":
            {
                var t = _totais.TryGetValue(v!["d"]!.GetValue<int>(), out var tt) ? tt : 50m;
                return Node(new { totalValue = t, documentSetId = 3216, customerId = 63270, remainingReconciledValue = t, nullified = false, status = 1 });
            }
            case "receiptCreate": return Node(new { documentId = Id(), number = 5, documentSetName = "SOFTWARE", totalValue = d!["totalValue"]!.GetValue<decimal>() });
            default:
                if (op.EndsWith("Nullify")) return Node(new { documentId = v!["d"]!.GetValue<int>(), nullified = true });
                if (op.EndsWith("Delete")) return Node(new { status = true, deletedCount = 1 });
                if (op.EndsWith("GetPDF")) return JsonValue.Create(true);
                if (op.EndsWith("GetPDFToken")) return Node(new { token = "tok", path = "/privateassets/x.pdf", filename = "x.pdf" });
                throw new InvalidOperationException("Operação Moloni não simulada: " + op);
        }
    }

    private static decimal Total(JsonArray produtos) => Math.Round(produtos.Sum(p =>
    {
        var linha = p!["price"]!.GetValue<decimal>() * p["qty"]!.GetValue<decimal>();
        if (p["discount"] is JsonValue desc) linha *= 1 - desc.GetValue<decimal>() / 100m;
        var iva = p["taxes"] is JsonArray taxes && taxes.Count > 0 ? taxes[0]!["value"]!.GetValue<decimal>() : 0m;
        return Math.Round(linha, 2, MidpointRounding.AwayFromZero) * (1 + iva / 100m);
    }), 2, MidpointRounding.AwayFromZero);

    private static JsonNode? Node(object o) => JsonSerializer.SerializeToNode(o, Web);
}
