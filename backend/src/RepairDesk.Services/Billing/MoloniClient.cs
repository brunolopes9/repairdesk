using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Billing;

public class MoloniClient : IMoloniClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ISecretProtector _secrets;
    private readonly IConfiguration _configuration;

    public MoloniClient(HttpClient http, ISecretProtector secrets, IConfiguration configuration)
    {
        _http = http;
        _secrets = secrets;
        _configuration = configuration;
    }

    public async Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        await PostAsync<JsonElement>(settings, "companies/getOne", new { company_id = settings.CompanyId!.Value }, ct);
    }

    public async Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var result = await PostAsync<JsonElement>(settings, "documentSets/getAll", new { company_id = settings.CompanyId!.Value }, ct);
        if (result.ValueKind != JsonValueKind.Array) return Array.Empty<BillingSerieDto>();

        var series = new List<BillingSerieDto>();
        foreach (var item in result.EnumerateArray())
        {
            var id = GetInt(item, "document_set_id");
            if (id <= 0) continue;
            var name = GetString(item, "name") ?? $"Serie {id}";
            var active = GetInt(item, "active_by_default") == 1;
            series.Add(new BillingSerieDto(id, name, null, active));
        }
        return series;
    }

    public async Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        if (string.IsNullOrWhiteSpace(vat)) return null;

        var result = await PostAsync<JsonElement>(
            settings,
            "customers/getByVat",
            new { company_id = settings.CompanyId!.Value, vat = vat.Trim(), qty = 1 },
            ct);

        if (result.ValueKind != JsonValueKind.Array) return null;
        var first = result.EnumerateArray().FirstOrDefault();
        return first.ValueKind == JsonValueKind.Object ? GetInt(first, "customer_id") : null;
    }

    public async Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
    {
        EnsureReadyToInvoice(settings, draft.VatPercent);

        var today = DateTime.UtcNow.Date;
        var price = Math.Round(draft.AmountCents / 100m, 2);
        var product = new Dictionary<string, object?>
        {
            ["product_id"] = settings.DefaultProductId!.Value,
            ["name"] = draft.ItemName,
            ["summary"] = draft.Summary,
            ["qty"] = 1,
            ["price"] = price,
            ["discount"] = 0,
            ["order"] = 1,
        };

        if (draft.VatPercent > 0)
        {
            product["taxes"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["tax_id"] = settings.DefaultTaxId!.Value,
                    ["value"] = draft.VatPercent,
                    ["order"] = 1,
                    ["cumulative"] = 0,
                },
            };
        }
        else
        {
            product["exemption_reason"] = settings.ExemptionReason;
            product["taxes"] = Array.Empty<object>();
        }

        var payload = new Dictionary<string, object?>
        {
            ["company_id"] = settings.CompanyId!.Value,
            ["date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["expiration_date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["document_set_id"] = settings.DefaultSerieId!.Value,
            ["customer_id"] = draft.CustomerId,
            ["our_reference"] = draft.Reference,
            ["your_reference"] = draft.Reference,
            ["status"] = 1,
            ["products"] = new[] { product },
        };

        if (settings.DefaultMaturityDateId is { } maturityDateId)
            payload["maturity_date_id"] = maturityDateId;

        if (settings.DefaultDocumentType == BillingDocumentType.FaturaSimplificada
            && settings.DefaultPaymentMethodId is { } paymentMethodId)
        {
            payload["payments"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["payment_method_id"] = paymentMethodId,
                    ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ["value"] = price,
                    ["notes"] = draft.PaymentMethod,
                },
            };
        }

        var endpoint = settings.DefaultDocumentType == BillingDocumentType.FaturaSimplificada
            ? "simplifiedInvoices/insert"
            : "invoices/insert";

        var insert = await PostAsync<JsonElement>(settings, endpoint, payload, ct);
        var documentId = GetInt(insert, "document_id");
        if (documentId <= 0)
            throw new BillingProviderException("moloni_missing_document_id", "A Moloni respondeu sem document_id.");

        var document = await PostAsync<JsonElement>(
            settings,
            "documents/getOne",
            new { company_id = settings.CompanyId!.Value, document_id = documentId },
            ct);

        var pdf = await PostAsync<JsonElement>(
            settings,
            "documents/getPDFLink",
            new { company_id = settings.CompanyId!.Value, document_id = documentId, signed = 1 },
            ct);

        var number = BuildInvoiceNumber(document, documentId);
        return new MoloniInvoiceResult(
            documentId.ToString(CultureInfo.InvariantCulture),
            number,
            GetString(pdf, "url"),
            DateTime.UtcNow);
    }

    public async Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        if (!int.TryParse(documentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var moloniDocumentId))
            throw new ValidationException("invoice_id_invalid", "ID de fatura Moloni invalido.");

        var pdf = await PostAsync<JsonElement>(
            settings,
            "documents/getPDFLink",
            new { company_id = settings.CompanyId!.Value, document_id = moloniDocumentId, signed = 1 },
            ct);
        var url = GetString(pdf, "url");
        if (string.IsNullOrWhiteSpace(url))
            throw new BillingProviderException("moloni_pdf_missing", "A Moloni nao devolveu link PDF.");

        return await _http.GetStreamAsync(url, ct);
    }

    private async Task<T> PostAsync<T>(TenantBillingSettings settings, string endpoint, object payload, CancellationToken ct)
    {
        var accessToken = ResolveAccessToken(settings);
        var baseUrl = ResolveBaseUrl(settings);
        var uri = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}?access_token={Uri.EscapeDataString(accessToken)}&json=true&human_errors=true";

        using var response = await _http.PostAsJsonAsync(uri, payload, JsonOptions, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var msg = ExtractMoloniError(content);
            throw new BillingProviderException(
                "moloni_http_error",
                $"Moloni respondeu {(int)response.StatusCode}: {msg}");
        }

        if (string.IsNullOrWhiteSpace(content))
            throw new BillingProviderException("moloni_empty_response", "A Moloni devolveu uma resposta vazia.");

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement.Clone();
        if (IsInvalidMoloniResponse(root, out var errorMessage))
            throw new BillingProviderException("moloni_validation_error", errorMessage);

        return JsonSerializer.Deserialize<T>(root.GetRawText(), JsonOptions)
               ?? throw new BillingProviderException("moloni_invalid_response", "Resposta Moloni inesperada.");
    }

    private static bool IsInvalidMoloniResponse(JsonElement root, out string message)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("valid", out var valid)
            && ReadInt(valid) == 0)
        {
            message = ExtractHumanErrors(root) ?? "Pedido rejeitado pela Moloni.";
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
        {
            message = error.GetString() ?? "Erro Moloni.";
            if (root.TryGetProperty("error_description", out var description) && description.ValueKind == JsonValueKind.String)
                message = $"{message}: {description.GetString()}";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static string ExtractMoloniError(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            return ExtractHumanErrors(doc.RootElement)
                   ?? GetString(doc.RootElement, "error_description")
                   ?? GetString(doc.RootElement, "error")
                   ?? content;
        }
        catch (JsonException)
        {
            return content.Length > 500 ? content[..500] : content;
        }
    }

    private static string? ExtractHumanErrors(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            var arrayErrors = root.EnumerateArray()
                .Select(e => GetString(e, "description") ?? e.ToString())
                .Where(e => !string.IsNullOrWhiteSpace(e));
            return string.Join("; ", arrayErrors);
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("errors", out var objectErrors))
            return ExtractHumanErrors(objectErrors);

        return null;
    }

    private string ResolveAccessToken(TenantBillingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKeyCipherText))
            throw new ValidationException("moloni_access_token_missing", "Configura o access token/API key da Moloni.");
        return _secrets.Unprotect(settings.ApiKeyCipherText);
    }

    private string ResolveBaseUrl(TenantBillingSettings settings)
    {
        var sandbox = settings.SandboxMode || bool.TryParse(_configuration["MOLONI_SANDBOX"], out var envSandbox) && envSandbox;
        var configured = sandbox
            ? _configuration["Billing:Moloni:SandboxBaseUrl"]
            : _configuration["Billing:Moloni:BaseUrl"];

        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return sandbox ? "https://api-sandbox.moloni.pt/v1" : "https://api.moloni.pt/v1";
    }

    private static void EnsureMoloniBasics(TenantBillingSettings settings)
    {
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_moloni", "Configura Moloni como provider de faturacao.");
        if (settings.CompanyId is null or <= 0)
            throw new ValidationException("moloni_company_missing", "Configura o CompanyId da Moloni.");
    }

    private static void EnsureReadyToInvoice(TenantBillingSettings settings, decimal vatPercent)
    {
        EnsureMoloniBasics(settings);
        if (settings.DefaultSerieId is null or <= 0)
            throw new ValidationException("moloni_serie_missing", "Configura a serie Moloni por defeito.");
        if (settings.DefaultProductId is null or <= 0)
            throw new ValidationException("moloni_product_missing", "Configura o produto/servico Moloni por defeito.");
        if (vatPercent > 0 && settings.DefaultTaxId is null or <= 0)
            throw new ValidationException("moloni_tax_missing", "Configura o TaxId Moloni para documentos com IVA.");
        if (vatPercent <= 0 && string.IsNullOrWhiteSpace(settings.ExemptionReason))
            throw new ValidationException("moloni_exemption_missing", "Configura o motivo de isencao Moloni.");
    }

    private static string BuildInvoiceNumber(JsonElement document, int fallbackId)
    {
        var saftCode = document.TryGetProperty("document_type", out var docType)
            ? GetString(docType, "saft_code")
            : null;
        var year = GetInt(document, "year");
        var number = GetInt(document, "number");

        if (!string.IsNullOrWhiteSpace(saftCode) && year > 0 && number > 0)
            return $"{saftCode} {year}/{number}";

        return $"Moloni #{fallbackId}";
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

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var prop) && prop.ValueKind != JsonValueKind.Null
            ? prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString()
            : null;
}
