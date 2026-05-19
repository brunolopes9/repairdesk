using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Billing.InvoiceXpress;

public sealed class InvoiceXpressClient : IInvoiceXpressClient
{
    private const string Invoices = "invoices";
    private const string SimplifiedInvoices = "simplified_invoices";
    private const string CreditNotes = "credit_notes";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ISecretProtector _secrets;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InvoiceXpressClient> _logger;

    public InvoiceXpressClient(
        HttpClient http,
        ISecretProtector secrets,
        IConfiguration configuration,
        ILogger<InvoiceXpressClient> logger)
    {
        _http = http;
        _secrets = secrets;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureInvoiceXpressBasics(settings);
        await SendJsonAsync(HttpMethod.Get, settings, "clients.json?page=1&per_page=1", payload: null, ct);
    }

    public async Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureInvoiceXpressBasics(settings);
        var root = await SendJsonAsync(HttpMethod.Get, settings, "sequences.json", payload: null, ct);

        return EnumerateItems(root, "sequences", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "id", "sequence_id");
                var name = GetStringAny(item, "name", "serie", "prefix") ?? $"Serie {id}";
                var code = GetStringAny(item, "prefix", "serie");
                return id > 0 ? new BillingSerieDto(id, name, code, GetActive(item)) : null;
            })
            .Where(x => x is not null)
            .Cast<BillingSerieDto>()
            .ToArray();
    }

    public async Task<IReadOnlyList<InvoiceXpressClientDto>> GetClientsAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureInvoiceXpressBasics(settings);
        var root = await SendJsonAsync(HttpMethod.Get, settings, "clients.json?page=1&per_page=100", payload: null, ct);

        return EnumerateItems(root, "clients", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "id", "client_id");
                var name = GetStringAny(item, "name") ?? $"Cliente {id}";
                var fiscalId = GetStringAny(item, "fiscal_id", "vat", "tax_number");
                return id > 0 ? new InvoiceXpressClientDto(id, name, fiscalId) : null;
            })
            .Where(x => x is not null)
            .Cast<InvoiceXpressClientDto>()
            .ToArray();
    }

    public async Task<IReadOnlyList<InvoiceXpressItemDto>> GetItemsAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureInvoiceXpressBasics(settings);
        var root = await SendJsonAsync(HttpMethod.Get, settings, "items.json?page=1&per_page=100", payload: null, ct);

        return EnumerateItems(root, "items", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "id", "item_id");
                var name = GetStringAny(item, "name") ?? $"Item {id}";
                return id > 0 ? new InvoiceXpressItemDto(id, name) : null;
            })
            .Where(x => x is not null)
            .Cast<InvoiceXpressItemDto>()
            .ToArray();
    }

    public async Task<IReadOnlyList<InvoiceXpressDocumentDto>> ListInvoicesAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureInvoiceXpressBasics(settings);
        var root = await SendJsonAsync(HttpMethod.Get, settings, "invoices.json?page=1&per_page=100", payload: null, ct);

        return EnumerateItems(root, "invoices", "documents", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "id", "document_id");
                var number = GetStringAny(item, "sequence_number", "inverted_sequence_number") ?? $"InvoiceXpress #{id}";
                var status = GetStringAny(item, "status") ?? "";
                var type = GetStringAny(item, "type") ?? Invoices;
                return id > 0 ? new InvoiceXpressDocumentDto(id, number, status, type) : null;
            })
            .Where(x => x is not null)
            .Cast<InvoiceXpressDocumentDto>()
            .ToArray();
    }

    public async Task<InvoiceXpressInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, InvoiceXpressInvoiceDraft draft, CancellationToken ct = default)
    {
        var items = draft.Items is { Count: > 0 }
            ? draft.Items
            : new[]
            {
                new InvoiceXpressInvoiceDraftItem(
                    draft.ItemName,
                    draft.Summary,
                    1,
                    draft.AmountCents,
                    0,
                    draft.VatPercent),
            };

        EnsureReadyToInvoice(settings, items.Max(i => i.VatPercent));

        var documentType = ResolveDocumentType(draft.DocumentTypeOverride ?? settings.DefaultDocumentType);
        var payload = BuildInvoicePayload(settings, draft.Client, draft.Reference, draft.Summary, items, ownerInvoiceId: null);

        var root = await SendJsonAsync(HttpMethod.Post, settings, $"{documentType}.json", payload, ct);
        return await BuildResultAsync(settings, documentType, root, ct);
    }

    public async Task<InvoiceXpressInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, InvoiceXpressCreditNoteDraft draft, CancellationToken ct = default)
    {
        EnsureReadyToInvoice(settings, draft.Items.FirstOrDefault()?.VatPercent ?? 23m);
        var original = ParseDocumentRef(draft.OriginalExternalId, settings.DefaultDocumentType);
        var payload = BuildInvoicePayload(settings, draft.Client, draft.Reference, draft.Reason, draft.Items, original.Id);

        var root = await SendJsonAsync(HttpMethod.Post, settings, $"{CreditNotes}.json", payload, ct);
        return await BuildResultAsync(settings, CreditNotes, root, ct);
    }

    public async Task<bool> CancelDocumentAsync(TenantBillingSettings settings, string externalId, string reason, CancellationToken ct = default)
    {
        EnsureInvoiceXpressBasics(settings);
        var document = ParseDocumentRef(externalId, settings.DefaultDocumentType);
        if (document.Id <= 0)
            throw new ValidationException("invoicexpress_document_id_invalid", "ID do documento InvoiceXpress invalido.");

        var payload = new
        {
            invoice = new
            {
                state = "canceled",
                message = string.IsNullOrWhiteSpace(reason) ? "Cancelado via RepairDesk" : reason,
            },
        };

        try
        {
            await SendJsonAsync(HttpMethod.Put, settings, $"{document.DocumentType}/{document.Id}/change-state.json", payload, ct);
            return true;
        }
        catch (BillingProviderException ex)
        {
            _logger.LogInformation(
                "InvoiceXpress cancel rejected for {DocumentType}:{DocumentId}: {Message}",
                document.DocumentType,
                document.Id,
                ex.Message);
            return false;
        }
    }

    public async Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string externalId, CancellationToken ct = default)
    {
        EnsureInvoiceXpressBasics(settings);
        var document = ParseDocumentRef(externalId, settings.DefaultDocumentType);
        var url = await GetPdfUrlAsync(settings, document.Id, ct);
        return await _http.GetStreamAsync(url, ct);
    }

    private async Task<InvoiceXpressInvoiceResult> BuildResultAsync(
        TenantBillingSettings settings,
        string documentType,
        JsonElement root,
        CancellationToken ct)
    {
        var document = ExtractDocument(root);
        var id = GetIntAny(document, "id", "document_id");
        if (id <= 0)
            throw new BillingProviderException("invoicexpress_missing_document_id", "A InvoiceXpress respondeu sem id do documento.");

        var number = GetStringAny(document, "sequence_number", "inverted_sequence_number") ?? $"InvoiceXpress #{id}";
        var pdfUrl = await GetPdfUrlAsync(settings, id, ct);
        return new InvoiceXpressInvoiceResult(
            $"{documentType}:{id.ToString(CultureInfo.InvariantCulture)}",
            number,
            pdfUrl,
            DateTime.UtcNow);
    }

    private async Task<string> GetPdfUrlAsync(TenantBillingSettings settings, int documentId, CancellationToken ct)
    {
        var root = await SendJsonAsync(HttpMethod.Get, settings, $"api/pdf/{documentId}.json", payload: null, ct);
        var output = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("output", out var prop)
            ? prop
            : root;
        var url = GetStringAny(output, "pdfUrl", "pdf_url", "url");
        if (string.IsNullOrWhiteSpace(url))
            throw new BillingProviderException("invoicexpress_pdf_missing", "A InvoiceXpress nao devolveu link PDF.");
        return url;
    }

    private static Dictionary<string, object?> BuildInvoicePayload(
        TenantBillingSettings settings,
        InvoiceXpressClientDraft client,
        string reference,
        string? observations,
        IReadOnlyList<InvoiceXpressInvoiceDraftItem> items,
        int? ownerInvoiceId)
    {
        var today = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var invoice = new Dictionary<string, object?>
        {
            ["date"] = today,
            ["due_date"] = today,
            ["reference"] = reference,
            ["observations"] = observations,
            ["client"] = BuildClient(client),
            ["items"] = items.Select(BuildItem).ToArray(),
        };

        if (settings.DefaultSerieId is > 0)
            invoice["sequence_id"] = settings.DefaultSerieId.Value.ToString(CultureInfo.InvariantCulture);

        if (items.Any(i => i.VatPercent <= 0))
            invoice["tax_exemption"] = string.IsNullOrWhiteSpace(settings.ExemptionReason) ? "M01" : settings.ExemptionReason;

        if (ownerInvoiceId is > 0)
            invoice["owner_invoice_id"] = ownerInvoiceId.Value;

        return new Dictionary<string, object?>
        {
            ["invoice"] = invoice,
            ["proprietary_uid"] = Guid.NewGuid().ToString("N"),
        };
    }

    private static Dictionary<string, object?> BuildClient(InvoiceXpressClientDraft client)
    {
        var data = new Dictionary<string, object?>
        {
            ["name"] = string.IsNullOrWhiteSpace(client.Name) ? "Consumidor Final" : client.Name.Trim(),
            ["country"] = "Portugal",
        };

        if (!string.IsNullOrWhiteSpace(client.Email)) data["email"] = client.Email.Trim();
        if (!string.IsNullOrWhiteSpace(client.FiscalId)) data["fiscal_id"] = client.FiscalId.Trim();
        if (!string.IsNullOrWhiteSpace(client.Phone)) data["phone"] = client.Phone.Trim();

        return data;
    }

    private static Dictionary<string, object?> BuildItem(InvoiceXpressInvoiceDraftItem item)
    {
        var quantity = Math.Max(1, item.Quantity);
        var grossUnit = item.UnitPriceCents / 100m;
        var netUnit = item.VatPercent > 0
            ? grossUnit / (1 + item.VatPercent / 100m)
            : grossUnit;
        var discountPercent = quantity * item.UnitPriceCents <= 0
            ? 0m
            : Math.Round(item.DiscountCents / (decimal)(quantity * item.UnitPriceCents) * 100m, 4);

        return new Dictionary<string, object?>
        {
            ["name"] = item.Name,
            ["description"] = item.Summary,
            ["unit_price"] = Math.Round(netUnit, 4),
            ["quantity"] = quantity,
            ["unit"] = "unidade",
            ["discount"] = discountPercent,
            ["tax"] = new Dictionary<string, object?>
            {
                ["name"] = BuildTaxName(item.VatPercent),
            },
        };
    }

    private async Task<JsonElement> SendJsonAsync(
        HttpMethod method,
        TenantBillingSettings settings,
        string path,
        object? payload,
        CancellationToken ct)
    {
        var uri = BuildUri(settings, path);
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.ParseAdd("application/json");
        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var message = ExtractError(content);
            throw new BillingProviderException(
                "invoicexpress_http_error",
                $"InvoiceXpress respondeu {(int)response.StatusCode}: {message}");
        }

        if (response.StatusCode == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(content))
        {
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private string BuildUri(TenantBillingSettings settings, string path)
    {
        var baseUrl = ResolveBaseUrl(settings).TrimEnd('/');
        var separator = path.Contains("?", StringComparison.Ordinal) ? "&" : "?";
        var apiKey = ResolveApiKey(settings);
        return $"{baseUrl}/{path.TrimStart('/')}{separator}api_key={Uri.EscapeDataString(apiKey)}";
    }

    private string ResolveApiKey(TenantBillingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKeyCipherText))
            throw new ValidationException("invoicexpress_api_key_missing", "Configura a API key da InvoiceXpress.");
        return _secrets.Unprotect(settings.ApiKeyCipherText);
    }

    private string ResolveBaseUrl(TenantBillingSettings settings)
    {
        var account = settings.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(account))
            throw new ValidationException("invoicexpress_account_missing", "Preenche o Account Name da InvoiceXpress.");

        var configured = settings.SandboxMode
            ? _configuration["Billing:InvoiceXpress:SandboxBaseUrl"]
            : _configuration["Billing:InvoiceXpress:BaseUrl"];

        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Replace("{account_name}", account, StringComparison.OrdinalIgnoreCase);

        return $"https://{account}.app.invoicexpress.com";
    }

    private static void EnsureInvoiceXpressBasics(TenantBillingSettings settings)
    {
        if (settings.Provider != BillingProvider.InvoiceXpress)
            throw new ValidationException("billing_provider_not_invoicexpress", "Configura InvoiceXpress como provider de faturacao.");
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new ValidationException("invoicexpress_account_missing", "Preenche o Account Name da InvoiceXpress.");
        if (string.IsNullOrWhiteSpace(settings.ApiKeyCipherText))
            throw new ValidationException("invoicexpress_api_key_missing", "Configura a API key da InvoiceXpress.");
    }

    private static void EnsureReadyToInvoice(TenantBillingSettings settings, decimal vatPercent)
    {
        EnsureInvoiceXpressBasics(settings);
        if (settings.DefaultSerieId is null or <= 0)
            throw new ValidationException("invoicexpress_serie_missing", "Configura a serie InvoiceXpress por defeito.");
        if (vatPercent <= 0 && string.IsNullOrWhiteSpace(settings.ExemptionReason))
            throw new ValidationException("invoicexpress_exemption_missing", "Configura o motivo de isencao InvoiceXpress.");
    }

    private static string ResolveDocumentType(BillingDocumentType documentType)
        => documentType == BillingDocumentType.Fatura ? Invoices : SimplifiedInvoices;

    private static InvoiceXpressDocumentRef ParseDocumentRef(string externalId, BillingDocumentType fallbackDocumentType)
    {
        var documentType = ResolveDocumentType(fallbackDocumentType);
        var idPart = externalId;
        var separator = externalId.IndexOf(':');
        if (separator > 0)
        {
            documentType = externalId[..separator];
            idPart = externalId[(separator + 1)..];
        }

        if (documentType is not (Invoices or SimplifiedInvoices or CreditNotes))
            throw new ValidationException("invoicexpress_document_type_invalid", "Tipo de documento InvoiceXpress invalido.");

        if (!int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new ValidationException("invoicexpress_document_id_invalid", "ID do documento InvoiceXpress invalido.");

        return new InvoiceXpressDocumentRef(documentType, id);
    }

    private static JsonElement ExtractDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return root;

        foreach (var propertyName in new[] { "invoice", "credit_note", "document", "simplified_invoice" })
        {
            if (root.TryGetProperty(propertyName, out var document) && document.ValueKind == JsonValueKind.Object)
                return document;
        }

        return root;
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root, params string[] arrayPropertyNames)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                yield return item;
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var propertyName in arrayPropertyNames)
        {
            if (root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                    yield return item;
                yield break;
            }
        }
    }

    private static string BuildTaxName(decimal vatPercent)
        => $"IVA{Math.Round(vatPercent, 0).ToString("0", CultureInfo.InvariantCulture)}";

    private static bool GetActive(JsonElement item)
    {
        foreach (var name in new[] { "active", "is_active", "current" })
        {
            if (item.TryGetProperty(name, out var prop))
                return ReadBool(prop);
        }

        return true;
    }

    private static int GetIntAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetInt(element, name);
            if (value != 0) return value;
        }

        return 0;
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop)) return 0;
        return ReadInt(prop);
    }

    private static int ReadInt(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)) return value;
        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return value;
        return 0;
    }

    private static bool ReadBool(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.True) return true;
        if (prop.ValueKind == JsonValueKind.False) return false;
        if (prop.ValueKind == JsonValueKind.Number) return ReadInt(prop) != 0;
        if (prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            return string.Equals(value, "1", StringComparison.Ordinal)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string? GetStringAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(element, name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var prop) && prop.ValueKind != JsonValueKind.Null
            ? prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString()
            : null;

    private static string ExtractError(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "sem detalhe";

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var direct = GetStringAny(root, "message", "error", "error_description");
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("errors", out var errors))
            {
                if (errors.ValueKind == JsonValueKind.Array)
                    return string.Join("; ", errors.EnumerateArray().Select(e => e.ToString()));
                return errors.ToString();
            }
        }
        catch (JsonException)
        {
            // Fall through to raw content.
        }

        return content.Length > 500 ? content[..500] : content;
    }

    private readonly record struct InvoiceXpressDocumentRef(string DocumentType, int Id);
}
