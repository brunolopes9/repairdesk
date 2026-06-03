using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly ITenantBillingSettingsRepository _repo;
    private readonly ILogger<MoloniClient> _logger;

    public MoloniClient(
        HttpClient http,
        ISecretProtector secrets,
        IConfiguration configuration,
        ITenantBillingSettingsRepository repo,
        ILogger<MoloniClient> logger)
    {
        _http = http;
        _secrets = secrets;
        _configuration = configuration;
        _repo = repo;
        _logger = logger;
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
        var draftItems = draft.Items is { Count: > 0 }
            ? draft.Items
            : new[]
            {
                new MoloniInvoiceDraftItem(
                    draft.ItemName,
                    draft.Summary,
                    1,
                    draft.AmountCents,
                    0,
                    draft.VatPercent),
            };

        EnsureReadyToInvoice(settings, draftItems.Max(i => i.VatPercent));

        var today = DateTime.UtcNow.Date;
        var taxIdByRate = await ResolveTaxIdsAsync(settings, draftItems, ct);
        var products = draftItems
            .Select((item, index) => BuildProduct(settings, item, index + 1, taxIdByRate))
            .ToArray();
        var documentType = draft.DocumentTypeOverride ?? settings.DefaultDocumentType;

        // Sprint 156b: calcular total recalculando à mesma precisão Moloni (2 casas) — evita
        // mismatch payments.value ↔ sum(products × IVA) que Moloni rejeita com "Database error".
        decimal totalWithVat = 0m;
        foreach (var p in products)
        {
            var netUnit = Convert.ToDecimal(p["price"]);
            var qty = Convert.ToDecimal(p["qty"]);
            var discount = Convert.ToDecimal(p["discount"]);
            decimal lineNetTotal = qty * netUnit * (1 - discount / 100m);
            // Para cada tax aplicada, adicionar valor.
            if (p["taxes"] is System.Collections.IEnumerable taxesEnum)
            {
                decimal taxAccum = 0m;
                foreach (var tax in taxesEnum)
                {
                    if (tax is IDictionary<string, object?> t && t.TryGetValue("value", out var v) && v is not null)
                    {
                        var vatPct = Convert.ToDecimal(v);
                        taxAccum += Math.Round(lineNetTotal * vatPct / 100m, 2);
                    }
                }
                totalWithVat += Math.Round(lineNetTotal, 2) + taxAccum;
            }
            else
            {
                totalWithVat += Math.Round(lineNetTotal, 2);
            }
        }
        totalWithVat = Math.Round(totalWithVat, 2);

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
            ["products"] = products,
        };

        if (settings.DefaultMaturityDateId is { } maturityDateId)
            payload["maturity_date_id"] = maturityDateId;

        if (documentType == BillingDocumentType.FaturaSimplificada
            && settings.DefaultPaymentMethodId is { } paymentMethodId)
        {
            // Sprint 156b: payments.value = total recalculado da soma dos products × IVA
            // (não do Sum(UnitPriceCents)). Garante consistência total↔payments — Moloni
            // rejeita mismatch com "Database error - try again later".
            payload["payments"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["payment_method_id"] = paymentMethodId,
                    ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ["value"] = totalWithVat,
                    ["notes"] = draft.PaymentMethod ?? "",
                },
            };
        }

        var endpoint = documentType == BillingDocumentType.FaturaSimplificada
            ? "simplifiedInvoices/insert"
            : "invoices/insert";

        var insert = await PostInvoiceWithRetryAsync(settings, endpoint, payload, ct);
        var documentId = GetInt(insert, "document_id");
        if (documentId <= 0)
        {
            // Sprint 144b: log + propaga ao frontend com snippet da resposta para diagnose.
            // Sprint 506: logar TAMBÉM o payload literal — sem isto ficamos a adivinhar a estrutura.
            var rawJson = insert.GetRawText();
            _logger.LogError(
                "Moloni {Endpoint} respondeu sem document_id. PAYLOAD: {Payload} | JSON cru: {Json}",
                endpoint, JsonSerializer.Serialize(payload, JsonOptions), rawJson);
            throw new BillingProviderException(
                "moloni_missing_document_id",
                $"A Moloni respondeu sem document_id. Resposta: {(rawJson.Length > 300 ? rawJson[..300] + "…" : rawJson)}");
        }

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

        var number = BuildDocumentNumber(document, documentId);
        return new MoloniInvoiceResult(
            documentId.ToString(CultureInfo.InvariantCulture),
            number,
            GetString(pdf, "url"),
            DateTime.UtcNow);
    }

    public async Task<MoloniEstimateResult> InsertEstimateAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
    {
        var draftItems = draft.Items is { Count: > 0 }
            ? draft.Items
            : new[]
            {
                new MoloniInvoiceDraftItem(
                    draft.ItemName,
                    draft.Summary,
                    1,
                    draft.AmountCents,
                    0,
                    draft.VatPercent),
            };

        EnsureReadyToInvoice(settings, draftItems.Max(i => i.VatPercent));

        var today = DateTime.UtcNow.Date;
        var expiration = today.AddDays(30);
        var taxIdByRate = await ResolveTaxIdsAsync(settings, draftItems, ct);
        var payload = new Dictionary<string, object?>
        {
            ["company_id"] = settings.CompanyId!.Value,
            ["date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["expiration_date"] = expiration.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["document_set_id"] = settings.DefaultSerieId!.Value,
            ["customer_id"] = draft.CustomerId,
            ["our_reference"] = draft.Reference,
            ["your_reference"] = draft.Reference,
            ["status"] = 1,
            ["products"] = draftItems.Select((item, index) => BuildProduct(settings, item, index + 1, taxIdByRate)).ToArray(),
        };

        if (settings.DefaultMaturityDateId is { } maturityDateId)
            payload["maturity_date_id"] = maturityDateId;

        var insert = await PostAsync<JsonElement>(settings, "estimates/insert", payload, ct);
        var documentId = GetInt(insert, "document_id");
        if (documentId <= 0)
            throw new BillingProviderException("moloni_missing_estimate_id", "A Moloni respondeu sem document_id ao emitir orçamento.");

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

        var number = BuildDocumentNumber(document, documentId);
        return new MoloniEstimateResult(
            documentId.ToString(CultureInfo.InvariantCulture),
            number,
            GetString(pdf, "url"),
            DateTime.UtcNow);
    }

    public Task<int?> GetEstimateStatusAsync(TenantBillingSettings settings, int estimateId, CancellationToken ct = default)
        => GetDocumentStatusAsync(settings, estimateId, ct);

    public async Task<MoloniInvoiceResult> ConvertEstimateToInvoiceAsync(
        TenantBillingSettings settings,
        int estimateId,
        BillingDocumentType? documentTypeOverride = null,
        CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        if (estimateId <= 0)
            throw new ValidationException("moloni_estimate_id_invalid", "ID do orçamento Moloni inválido.");
        if (settings.DefaultSerieId is null or <= 0)
            throw new ValidationException("moloni_serie_missing", "Configura a série Moloni por defeito.");

        var today = DateTime.UtcNow.Date;
        var documentType = documentTypeOverride ?? settings.DefaultDocumentType;
        var payload = new Dictionary<string, object?>
        {
            ["company_id"] = settings.CompanyId!.Value,
            ["document_id"] = estimateId,
            ["document_set_id"] = settings.DefaultSerieId!.Value,
            ["date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["expiration_date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["status"] = 1,
            ["document_type"] = documentType == BillingDocumentType.FaturaSimplificada ? "simplified_invoice" : "invoice",
        };

        if (settings.DefaultMaturityDateId is { } maturityDateId)
            payload["maturity_date_id"] = maturityDateId;

        var converted = await PostAsync<JsonElement>(settings, "documentsToInvoice", payload, ct);
        var documentId = GetIntAny(converted, "document_id", "invoice_id", "new_document_id");
        if (documentId <= 0)
            throw new BillingProviderException("moloni_convert_missing_document_id", "A Moloni converteu o orçamento sem devolver document_id da fatura.");

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

        var number = BuildDocumentNumber(document, documentId);
        return new MoloniInvoiceResult(
            documentId.ToString(CultureInfo.InvariantCulture),
            number,
            GetString(pdf, "url"),
            DateTime.UtcNow);
    }

    private static Dictionary<string, object?> BuildProduct(
        TenantBillingSettings settings,
        MoloniInvoiceDraftItem item,
        int order,
        IReadOnlyDictionary<decimal, int> taxIdByRate)
    {
        var grossUnit = item.UnitPriceCents / 100m;
        var netUnit = item.VatPercent > 0
            ? grossUnit / (1 + item.VatPercent / 100m)
            : grossUnit;
        var discountPercent = item.Quantity * item.UnitPriceCents <= 0
            ? 0m
            : Math.Round(item.DiscountCents / (decimal)(item.Quantity * item.UnitPriceCents) * 100m, 4);

        var product = new Dictionary<string, object?>
        {
            ["product_id"] = settings.DefaultProductId!.Value,
            ["name"] = item.Name,
            ["summary"] = item.Summary,
            ["qty"] = item.Quantity,
            // Sprint 156b: arredondar a 2 casas (mesma precisão que Moloni internamente).
            // 4 casas causam mismatch entre subtotal calculado pela API e payments.value.
            ["price"] = Math.Round(netUnit, 2),
            ["discount"] = discountPercent,
            ["order"] = order,
        };

        if (item.VatPercent > 0)
        {
            // Sprint 505: o Moloni usa a percentagem GUARDADA do imposto referenciado por tax_id
            // (ignora o nosso "value"). Usamos o tax_id resolvido pela taxa real da conta; só se a
            // resolução falhar é que caímos no DefaultTaxId (que pode estar stale → net_value=0).
            var resolvedTaxId = taxIdByRate.TryGetValue(item.VatPercent, out var tid) && tid > 0
                ? tid
                : (settings.DefaultTaxId ?? 0);
            product["taxes"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["tax_id"] = resolvedTaxId,
                    ["value"] = item.VatPercent,
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

        return product;
    }

    /// <summary>
    /// Sprint 505: resolve o tax_id correcto para cada taxa de IVA do documento. O Moloni,
    /// ao receber um tax_id, usa a percentagem GUARDADA desse imposto e ignora o "value" que
    /// enviamos; se o DefaultTaxId estiver stale/errado (aponta para imposto com valor 0 ou
    /// não-IVA), o documento rebenta com "Field 'net_value' must be float, greater than 0".
    /// Procuramos o imposto da conta cuja percentagem bate certo com a taxa da linha. Fallback
    /// ao DefaultTaxId só quando nenhum imposto bate. Loga os impostos disponíveis para diagnose.
    /// </summary>
    private async Task<IReadOnlyDictionary<decimal, int>> ResolveTaxIdsAsync(
        TenantBillingSettings settings,
        IEnumerable<MoloniInvoiceDraftItem> items,
        CancellationToken ct)
    {
        var rates = items.Where(i => i.VatPercent > 0).Select(i => i.VatPercent).Distinct().ToList();
        var map = new Dictionary<decimal, int>();
        if (rates.Count == 0) return map;

        IReadOnlyList<MoloniTaxDto> taxes;
        try
        {
            taxes = await GetTaxesAsync(settings, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Moloni taxes/getAll falhou ao resolver tax_id; uso DefaultTaxId={Default}", settings.DefaultTaxId);
            taxes = Array.Empty<MoloniTaxDto>();
        }

        foreach (var rate in rates)
        {
            var match = taxes.FirstOrDefault(t => t.IsActive && Math.Abs(t.Value - rate) < 0.01m)
                     ?? taxes.FirstOrDefault(t => Math.Abs(t.Value - rate) < 0.01m);
            var id = match?.Id ?? settings.DefaultTaxId ?? 0;
            map[rate] = id;
            _logger.LogInformation(
                "Moloni tax_id resolve: taxa={Rate}% -> tax_id={TaxId} (match={MatchId}, default={Default}, taxesConta=[{Taxes}])",
                rate, id, match?.Id, settings.DefaultTaxId,
                string.Join(", ", taxes.Select(t => $"{t.Id}:{t.Value}%{(t.IsActive ? "" : "(inativo)")}")));
        }
        return map;
    }

    public async Task<int?> GetDocumentStatusAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default)
    {
        if (settings.Provider != BillingProvider.Moloni) return null;
        if (settings.CompanyId is null or <= 0) return null;
        if (documentId <= 0) return null;

        try
        {
            var doc = await PostAsync<JsonElement>(
                settings,
                "documents/getOne",
                new { company_id = settings.CompanyId!.Value, document_id = documentId },
                ct);

            if (doc.ValueKind != JsonValueKind.Object) return null;
            if (!doc.TryGetProperty("status", out var statusProp)) return null;
            return ReadInt(statusProp);
        }
        catch (Exception ex)
        {
            // Moloni 503/timeout/auth-fail — devolve null para o caller manter estado local
            _logger.LogWarning("Moloni GetDocumentStatus({DocId}) falhou: {Msg}", documentId, ex.Message);
            return null;
        }
    }

    public async Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default)
    {
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_moloni", "Configura Moloni como provider.");
        if (settings.CompanyId is null or <= 0)
            throw new ValidationException("moloni_company_missing", "Configura o CompanyId Moloni.");
        if (documentId <= 0)
            throw new ValidationException("moloni_document_id_invalid", "ID do documento Moloni inválido.");

        try
        {
            var result = await PostAsync<JsonElement>(
                settings,
                "documents/documentCancel",
                new
                {
                    company_id = settings.CompanyId!.Value,
                    document_id = documentId,
                    observation = string.IsNullOrWhiteSpace(observation) ? "Cancelado via RepairDesk" : observation,
                },
                ct);

            // Moloni devolve { valid: 1, message } se ok
            var valid = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("valid", out var v) && ReadInt(v) == 1;
            _logger.LogInformation("Moloni documentCancel docId={DocId} -> {Result}", documentId, valid ? "OK" : "rejected");
            return valid;
        }
        catch (BillingProviderException ex)
        {
            // Moloni rejeita se: nao fechado, pendente AT, gerou outros docs, etc.
            // Caller deve fallback para InsertCreditNoteAsync.
            _logger.LogInformation("Moloni documentCancel falhou para docId={DocId}: {Msg}", documentId, ex.Message);
            return false;
        }
    }

    public async Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default)
    {
        EnsureReadyToInvoice(settings, draft.Items.FirstOrDefault()?.VatPercent ?? 23m);
        if (draft.OriginalDocumentId <= 0)
            throw new ValidationException("nc_sem_original", "Nota de Credito precisa de referencia a fatura original.");

        var today = DateTime.UtcNow.Date;
        var taxIdByRate = await ResolveTaxIdsAsync(settings, draft.Items, ct);
        var products = draft.Items.Select((item, index) => BuildProduct(settings, item, index + 1, taxIdByRate)).ToArray();

        // creditNotes/insert exige relacao com documento original via 'related_documents'
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
            ["notes"] = draft.Motivo,
            ["products"] = products,
            ["related_documents"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["associated_id"] = draft.OriginalDocumentId,
                    ["value"] = draft.Items.Sum(i => Math.Max(0, i.Quantity * i.UnitPriceCents - i.DiscountCents)) / 100m,
                },
            },
        };

        var insert = await PostAsync<JsonElement>(settings, "creditNotes/insert", payload, ct);
        var documentId = GetInt(insert, "document_id");
        if (documentId <= 0)
            throw new BillingProviderException("moloni_missing_document_id", "A Moloni respondeu sem document_id ao emitir Nota de Credito.");

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

        var number = BuildDocumentNumber(document, documentId);
        _logger.LogInformation("Moloni Credit Note {Number} (id={Id}) emitida para tenant {TenantId}", number, documentId, settings.TenantId);
        return new MoloniInvoiceResult(documentId.ToString(CultureInfo.InvariantCulture), number, GetString(pdf, "url"), DateTime.UtcNow);
    }

    public async Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default)
    {
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_moloni", "Configura Moloni como provider antes de ligar.");
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new ValidationException("moloni_client_id_missing", "Preenche o Developer ID Moloni antes de ligar.");
        if (string.IsNullOrWhiteSpace(settings.ClientSecretCipherText))
            throw new ValidationException("moloni_client_secret_missing", "Preenche o Client Secret Moloni antes de ligar.");
        if (string.IsNullOrWhiteSpace(username))
            throw new ValidationException("moloni_username_required", "Email Moloni obrigatorio.");
        if (string.IsNullOrWhiteSpace(password))
            throw new ValidationException("moloni_password_required", "Password Moloni obrigatoria.");

        var clientSecret = _secrets.Unprotect(settings.ClientSecretCipherText);
        var baseUrl = ResolveBaseUrl(settings).TrimEnd('/');

        var uri =
            $"{baseUrl}/grant/?grant_type=password" +
            $"&client_id={Uri.EscapeDataString(settings.ClientId)}" +
            $"&client_secret={Uri.EscapeDataString(clientSecret)}" +
            $"&username={Uri.EscapeDataString(username)}" +
            $"&password={Uri.EscapeDataString(password)}";

        using var response = await _http.GetAsync(uri, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var msg = ExtractMoloniError(content);
            _logger.LogWarning("Moloni password grant rejected for tenant {TenantId}: {Status} {Body}", settings.TenantId, (int)response.StatusCode, msg);
            throw new BillingProviderException(
                "moloni_connect_failed",
                $"Moloni rejeitou as credenciais (HTTP {(int)response.StatusCode}): {msg}");
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var accessToken = GetString(root, "access_token");
        var refreshToken = GetString(root, "refresh_token");

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            throw new BillingProviderException("moloni_connect_invalid_response", "Resposta de autenticacao Moloni invalida.");

        settings.ApiKeyCipherText = _secrets.Protect(accessToken);
        settings.RefreshTokenCipherText = _secrets.Protect(refreshToken);

        await _repo.SaveAsync(ct);
        _logger.LogInformation("Moloni connected via password grant for tenant {TenantId}", settings.TenantId);
    }

    public async Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default)
    {
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_moloni", "Configura Moloni como provider antes de ligar.");
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new ValidationException("moloni_client_id_missing", "Preenche o Developer ID Moloni antes de ligar.");
        if (string.IsNullOrWhiteSpace(settings.ClientSecretCipherText))
            throw new ValidationException("moloni_client_secret_missing", "Preenche o Client Secret Moloni antes de ligar.");
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("moloni_code_missing", "A Moloni nao devolveu codigo de autorizacao.");
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new ValidationException("moloni_redirect_uri_missing", "Redirect URI Moloni nao configurado.");

        var clientSecret = _secrets.Unprotect(settings.ClientSecretCipherText);
        var baseUrl = ResolveBaseUrl(settings).TrimEnd('/');

        var uri =
            $"{baseUrl}/grant/?grant_type=authorization_code" +
            $"&client_id={Uri.EscapeDataString(settings.ClientId)}" +
            $"&client_secret={Uri.EscapeDataString(clientSecret)}" +
            $"&code={Uri.EscapeDataString(code)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

        using var response = await _http.GetAsync(uri, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var msg = ExtractMoloniError(content);
            _logger.LogWarning("Moloni authorization code exchange rejected for tenant {TenantId}: {Status} {Body}", settings.TenantId, (int)response.StatusCode, msg);
            throw new BillingProviderException(
                "moloni_oauth_exchange_failed",
                $"Moloni rejeitou a autorizacao (HTTP {(int)response.StatusCode}): {msg}");
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var accessToken = GetString(root, "access_token");
        var refreshToken = GetString(root, "refresh_token");

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            throw new BillingProviderException("moloni_oauth_invalid_response", "Resposta de autenticacao Moloni invalida.");

        settings.ApiKeyCipherText = _secrets.Protect(accessToken);
        settings.RefreshTokenCipherText = _secrets.Protect(refreshToken);

        await _repo.SaveAsync(ct);
        _logger.LogInformation("Moloni connected via authorization_code for tenant {TenantId}", settings.TenantId);
    }

    public async Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_moloni", "Configura Moloni como provider de faturacao.");
        if (string.IsNullOrWhiteSpace(settings.ApiKeyCipherText))
            throw new ValidationException("moloni_not_connected", "Liga a conta Moloni antes de listar empresas.");

        var result = await PostAsync<JsonElement>(settings, "companies/getAll", new { }, ct);
        if (result.ValueKind != JsonValueKind.Array) return Array.Empty<MoloniCompanyDto>();

        var companies = new List<MoloniCompanyDto>();
        foreach (var item in result.EnumerateArray())
        {
            var id = GetInt(item, "company_id");
            if (id <= 0) continue;
            var name = GetString(item, "name") ?? $"Empresa {id}";
            companies.Add(new MoloniCompanyDto(id, name));
        }
        return companies;
    }

    public async Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var result = await PostAsync<JsonElement>(
            settings,
            "products/getAll",
            new { company_id = settings.CompanyId!.Value, qty = 500, page = 1 },
            ct);

        return EnumerateItems(result, "products", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "product_id", "id");
                var name = GetStringAny(item, "name", "title") ?? $"Produto {id}";
                return id > 0 ? new MoloniProductDto(id, name, GetActive(item)) : null;
            })
            .Where(x => x is not null)
            .Cast<MoloniProductDto>()
            .ToArray();
    }

    public async Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var result = await PostAsync<JsonElement>(
            settings,
            "taxes/getAll",
            new { company_id = settings.CompanyId!.Value, qty = 500, page = 1 },
            ct);

        return EnumerateItems(result, "taxes", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "tax_id", "id");
                var name = GetStringAny(item, "name", "title") ?? $"IVA {id}";
                var value = GetDecimalAny(item, "value", "percentage", "tax_value");
                var exemptionCode = GetStringAny(item, "exemption_reason", "exemption_reason_code", "saft_exemption_reason");
                return id > 0 ? new MoloniTaxDto(id, name, value, GetActive(item), exemptionCode) : null;
            })
            .Where(x => x is not null)
            .Cast<MoloniTaxDto>()
            .ToArray();
    }

    public async Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var result = await PostAsync<JsonElement>(
            settings,
            "paymentMethods/getAll",
            new { company_id = settings.CompanyId!.Value, qty = 500, page = 1 },
            ct);

        return EnumerateItems(result, "payment_methods", "paymentMethods", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "payment_method_id", "id");
                var name = GetStringAny(item, "name", "title") ?? $"Método {id}";
                return id > 0 ? new MoloniPaymentMethodDto(id, name, GetActive(item)) : null;
            })
            .Where(x => x is not null)
            .Cast<MoloniPaymentMethodDto>()
            .ToArray();
    }

    public async Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var result = await PostAsync<JsonElement>(
            settings,
            "maturityDates/getAll",
            new { company_id = settings.CompanyId!.Value, qty = 500, page = 1 },
            ct);

        return EnumerateItems(result, "maturity_dates", "maturityDates", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "maturity_date_id", "id");
                var name = GetStringAny(item, "name", "title") ?? $"Prazo {id}";
                return id > 0 ? new MoloniMaturityDateDto(id, name, GetActive(item)) : null;
            })
            .Where(x => x is not null)
            .Cast<MoloniMaturityDateDto>()
            .ToArray();
    }

    public async Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var result = await PostAsync<JsonElement>(
            settings,
            "customers/getAll",
            new { company_id = settings.CompanyId!.Value, qty = 500, page = 1 },
            ct);

        return EnumerateItems(result, "customers", "data")
            .Select(item =>
            {
                var id = GetIntAny(item, "customer_id", "id");
                var name = GetStringAny(item, "name", "title") ?? $"Cliente {id}";
                var vat = GetStringAny(item, "vat", "nif", "tax_number");
                return id > 0 ? new MoloniCustomerDto(id, name, vat, GetActive(item)) : null;
            })
            .Where(x => x is not null)
            .Cast<MoloniCustomerDto>()
            .ToArray();
    }

    public async Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var payload = new Dictionary<string, object?>
        {
            ["company_id"] = settings.CompanyId!.Value,
            ["name"] = name,
            ["reference"] = "REPAIR-SERVICE",
            ["summary"] = name,
            ["price"] = 0m,
            ["type"] = 1,
            ["has_stock"] = 0,
            ["visible"] = 1,
        };

        var result = await PostAsync<JsonElement>(settings, "products/insert", payload, ct);
        var id = GetIntAny(result, "product_id", "id");
        if (id <= 0)
            throw new BillingProviderException("moloni_product_insert_missing_id", "A Moloni criou produto sem devolver product_id.");

        return new MoloniProductDto(id, name, true);
    }

    public async Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        var payload = await BuildCustomerPayloadAsync(settings, name, vat, morada, codigoPostal, localidade, ct);

        var result = await PostAsync<JsonElement>(settings, "customers/insert", payload, ct);
        var id = GetIntAny(result, "customer_id", "id");
        if (id <= 0)
        {
            // Sprint 502: o insert pode "falhar" porque o VAT já existe (a Moloni rejeita NIF
            // duplicado) OU porque a resposta não trouxe o id. Re-procurar por VAT é idempotente
            // e auto-cura o caso comum (cliente criado numa tentativa anterior) sem duplicar.
            var rawJson = result.GetRawText();
            _logger.LogWarning("Moloni customers/insert sem customer_id (vat={Vat}). JSON cru: {Json}", vat, rawJson);
            var existing = await FindCustomerIdByVatAsync(settings, vat, ct);
            if (existing is > 0)
                return new MoloniCustomerDto(existing.Value, name, vat, true);
            throw new BillingProviderException(
                "moloni_customer_insert_missing_id",
                $"A Moloni não devolveu customer_id ao criar o cliente '{name}' (NIF {vat}). Resposta: {(rawJson.Length > 1000 ? rawJson[..1000] + "…" : rawJson)}");
        }

        return new MoloniCustomerDto(id, name, vat, true);
    }

    /// <summary>
    /// Sprint 510: actualiza nome/morada (+ cluster) de um cliente Moloni existente. Best-effort —
    /// usado para sincronizar a morada do Mender e corrigir clientes criados antes com o placeholder
    /// "Consumidor final". Devolve false se a Moloni não confirmar (o caller não bloqueia a emissão).
    /// </summary>
    public async Task<bool> UpdateCustomerAsync(TenantBillingSettings settings, int customerId, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default)
    {
        EnsureMoloniBasics(settings);
        if (customerId <= 0) return false;
        var payload = await BuildCustomerPayloadAsync(settings, name, vat, morada, codigoPostal, localidade, ct);
        payload["customer_id"] = customerId;
        var result = await PostAsync<JsonElement>(settings, "customers/update", payload, ct);
        var id = GetIntAny(result, "customer_id", "id");
        return id > 0
            || (result.ValueKind == JsonValueKind.Object
                && result.TryGetProperty("valid", out var v)
                && v.ValueKind == JsonValueKind.Number
                && v.GetInt32() == 1);
    }

    /// <summary>
    /// Sprint 510: payload partilhado por customers/insert e customers/update. Usa a morada REAL
    /// do cliente — nunca "Consumidor final" (isso é para anónimos e saía mal nas faturas com NIF).
    /// Inclui o cluster de campos obrigatórios (S503/S504) que algumas contas Moloni exigem.
    /// </summary>
    private async Task<Dictionary<string, object?>> BuildCustomerPayloadAsync(
        TenantBillingSettings settings, string name, string vat,
        string? morada, string? codigoPostal, string? localidade, CancellationToken ct)
    {
        var maturityDateId = settings.DefaultMaturityDateId ?? 0;
        if (maturityDateId <= 0)
        {
            var dates = await GetMaturityDatesAsync(settings, ct);
            maturityDateId = (dates.FirstOrDefault(d => d.IsActive) ?? dates.FirstOrDefault())?.Id ?? 0;
        }

        var address = string.IsNullOrWhiteSpace(morada) ? "Desconhecida" : morada.Trim();
        var city = string.IsNullOrWhiteSpace(localidade) ? "Desconhecida" : localidade.Trim();

        var payload = new Dictionary<string, object?>
        {
            ["company_id"] = settings.CompanyId!.Value,
            ["name"] = name,
            ["vat"] = vat,
            ["number"] = vat,
            ["language_id"] = 1,
            ["country_id"] = 1,
            ["address"] = address,
            ["city"] = city,
            ["salesman_id"] = 0,
            ["payment_day"] = 0,
            ["discount"] = 0,
            ["credit_limit"] = 0,
            ["delivery_method_id"] = 0,
        };
        // Moloni só valida o código postal PT se preenchido (4-3 dígitos). Enviamos apenas quando
        // válido — assim a morada não fica com o "0000-000" feio quando não há CP.
        var zip = NormalizePtZip(codigoPostal);
        if (zip is not null) payload["zip_code"] = zip;
        if (maturityDateId > 0) payload["maturity_date_id"] = maturityDateId;
        if (settings.DefaultPaymentMethodId is { } pmId && pmId > 0) payload["payment_method_id"] = pmId;
        return payload;
    }

    private static string? NormalizePtZip(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip)) return null;
        var z = zip.Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(z, @"^\d{4}-\d{3}$") ? z : null;
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

    private Task<T> PostAsync<T>(TenantBillingSettings settings, string endpoint, object payload, CancellationToken ct)
        => PostAsync<T>(settings, endpoint, payload, allowRefresh: true, ct);

    private async Task<T> PostAsync<T>(TenantBillingSettings settings, string endpoint, object payload, bool allowRefresh, CancellationToken ct)
    {
        var accessToken = ResolveAccessToken(settings);
        var baseUrl = ResolveBaseUrl(settings);
        // Moloni docs mostram URLs com trailing slash antes do `?` (e.g. companies/getAll/?...).
        // Garantir que o endpoint acaba em `/` antes do querystring para evitar 403 espurios.
        var path = endpoint.TrimStart('/').TrimEnd('/') + "/";
        var uri = $"{baseUrl.TrimEnd('/')}/{path}?access_token={Uri.EscapeDataString(accessToken)}&json=true&human_errors=true";

        // Moloni docs sao explicitas: com json=true na querystring, o body eh JSON mas o
        // Content-Type tem de ser application/x-www-form-urlencoded (sim, weird).
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        using var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // Log do body completo para conseguirmos diagnosticar erros Moloni
            _logger.LogWarning(
                "Moloni {Endpoint} retornou {Status}. Body: {Body}",
                endpoint,
                (int)response.StatusCode,
                content?.Length > 800 ? content[..800] : content);
        }

        // Sprint 217: content pode ser null aqui (ReadAsStringAsync devolveu null). Normaliza.
        var safeContent = content ?? string.Empty;
        if (allowRefresh && IsAuthFailure(response.StatusCode, safeContent) && CanRefresh(settings))
        {
            _logger.LogInformation("Moloni access token rejected; attempting refresh for tenant {TenantId}", settings.TenantId);
            await RefreshAccessTokenAsync(settings, ct);
            return await PostAsync<T>(settings, endpoint, payload, allowRefresh: false, ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            var msg = ExtractMoloniError(safeContent);
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

    private static bool CanRefresh(TenantBillingSettings settings)
        => !string.IsNullOrWhiteSpace(settings.ClientId)
           && !string.IsNullOrWhiteSpace(settings.ClientSecretCipherText)
           && !string.IsNullOrWhiteSpace(settings.RefreshTokenCipherText);

    private static bool IsAuthFailure(HttpStatusCode status, string content)
    {
        if (status == HttpStatusCode.Unauthorized) return true;
        if (string.IsNullOrWhiteSpace(content)) return false;

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                var code = error.GetString() ?? string.Empty;
                if (code.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    code.Equals("invalid_grant", StringComparison.OrdinalIgnoreCase) ||
                    code.Equals("invalid_client", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (JsonException)
        {
            // not JSON — fallthrough
        }

        return false;
    }

    private async Task RefreshAccessTokenAsync(TenantBillingSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new ValidationException("moloni_client_id_missing", "Developer ID Moloni nao configurado.");
        if (string.IsNullOrWhiteSpace(settings.ClientSecretCipherText))
            throw new ValidationException("moloni_client_secret_missing", "Client Secret Moloni nao configurado.");
        if (string.IsNullOrWhiteSpace(settings.RefreshTokenCipherText))
            throw new ValidationException("moloni_refresh_token_missing", "Refresh token Moloni nao configurado. Re-autentica em Definicoes > Faturacao.");

        var clientSecret = _secrets.Unprotect(settings.ClientSecretCipherText);
        var refreshToken = _secrets.Unprotect(settings.RefreshTokenCipherText);
        var baseUrl = ResolveBaseUrl(settings).TrimEnd('/');

        var refreshUri =
            $"{baseUrl}/grant/?grant_type=refresh_token" +
            $"&client_id={Uri.EscapeDataString(settings.ClientId)}" +
            $"&client_secret={Uri.EscapeDataString(clientSecret)}" +
            $"&refresh_token={Uri.EscapeDataString(refreshToken)}";

        using var response = await _http.GetAsync(refreshUri, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var msg = ExtractMoloniError(content);
            _logger.LogWarning("Moloni refresh token rejected: {Status} {Body}", (int)response.StatusCode, msg);
            throw new BillingProviderException(
                "moloni_refresh_failed",
                $"Refresh token Moloni rejeitado (HTTP {(int)response.StatusCode}). Re-autentica em Definicoes > Faturacao: {msg}");
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var newAccessToken = GetString(root, "access_token");
        var newRefreshToken = GetString(root, "refresh_token");

        if (string.IsNullOrWhiteSpace(newAccessToken) || string.IsNullOrWhiteSpace(newRefreshToken))
            throw new BillingProviderException("moloni_refresh_invalid_response", "Resposta de refresh Moloni invalida.");

        settings.ApiKeyCipherText = _secrets.Protect(newAccessToken);
        settings.RefreshTokenCipherText = _secrets.Protect(newRefreshToken);

        await _repo.SaveAsync(ct);
        _logger.LogInformation("Moloni access token refreshed for tenant {TenantId}", settings.TenantId);
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
        if (vatPercent > 0 && (settings.DefaultTaxId is null or <= 0))
            throw new ValidationException("moloni_tax_missing", "Configura o TaxId Moloni para documentos com IVA.");
        if (vatPercent <= 0 && string.IsNullOrWhiteSpace(settings.ExemptionReason))
            throw new ValidationException("moloni_exemption_missing", "Configura o motivo de isencao Moloni.");
    }

    private static string BuildDocumentNumber(JsonElement document, int fallbackId)
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

    /// <summary>
    /// Sprint 144c: emissão de documento com retry.
    /// Sprint 156 REVISÃO: descobrimos que "Database error" do Moloni NÃO é outage —
    /// é o shape padrão de erro de validação ([{"code":"...","description":"..."}]).
    /// Provavelmente algum *_id da config está inválido (product, tax, customer, document_set).
    /// Mantemos o retry (cobre outage real raro) mas logamos o payload COMPLETO para diagnose,
    /// e expomos mensagem accionável ao Bruno apontando para o diagnose endpoint.
    /// </summary>
    private async Task<JsonElement> PostInvoiceWithRetryAsync(
        TenantBillingSettings settings, string endpoint, object payload, CancellationToken ct)
    {
        var delays = new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
        JsonElement last = default;
        for (var attempt = 0; attempt <= delays.Length; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogWarning("Moloni {Endpoint} retry {Attempt}/{Max} após erro", endpoint, attempt, delays.Length);
                await Task.Delay(delays[attempt - 1], ct);
            }
            last = await PostAsync<JsonElement>(settings, endpoint, payload, ct);
            if (!IsMoloniTransientError(last)) return last;
        }

        // Sprint 156: log do PAYLOAD COMPLETO para diagnose. Sem isto, ficamos cegos.
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var responseJson = last.GetRawText();
        _logger.LogError(
            "Moloni {Endpoint} falhou após {Retries} retries. PAYLOAD: {Payload} | RESPONSE: {Response}",
            endpoint, delays.Length, payloadJson, responseJson);

        var errMsg = ExtractMoloniErrorMessage(last) ?? "erro desconhecido";
        throw new BillingProviderException(
            "moloni_validation_error",
            $"Moloni rejeitou o documento: \"{errMsg}\". Vai a Definições → Faturação → \"Diagnosticar Moloni\" para identificar qual ID da config está inválido.");
    }

    private static bool IsMoloniTransientError(JsonElement response)
    {
        // Moloni soft-fail: HTTP 200 + corpo [{code:..., description:...}] sem document_id.
        if (response.ValueKind != JsonValueKind.Array || response.GetArrayLength() == 0) return false;
        var first = response[0];
        if (first.ValueKind != JsonValueKind.Object) return false;
        if (first.TryGetProperty("document_id", out _)) return false;  // tem documento → não é erro
        if (first.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
        {
            var s = code.GetString() ?? "";
            return s.Contains("Database error", StringComparison.OrdinalIgnoreCase)
                || s.Contains("try again", StringComparison.OrdinalIgnoreCase)
                || s.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static string? ExtractMoloniErrorMessage(JsonElement response)
    {
        var target = UnwrapArrayIfNeeded(response);
        if (target.ValueKind != JsonValueKind.Object) return null;
        if (target.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String)
            return d.GetString();
        if (target.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
            return c.GetString();
        return null;
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

    private static decimal GetDecimalAny(JsonElement element, params string[] names)
    {
        var target = UnwrapArrayIfNeeded(element);
        if (target.ValueKind != JsonValueKind.Object) return 0m;
        foreach (var name in names)
        {
            if (!target.TryGetProperty(name, out var prop)) continue;
            var value = ReadDecimal(prop);
            if (value != 0m) return value;
        }

        return 0m;
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

    private static bool GetActive(JsonElement item)
    {
        foreach (var name in new[] { "active", "is_active", "visible", "enabled" })
        {
            if (item.TryGetProperty(name, out var prop))
                return ReadBool(prop);
        }

        return true;
    }

    private static int GetInt(JsonElement element, string name)
    {
        // Moloni às vezes retorna a resposta encapsulada em array — desempacotar.
        var target = UnwrapArrayIfNeeded(element);
        if (target.ValueKind != JsonValueKind.Object) return 0;
        if (!target.TryGetProperty(name, out var prop)) return 0;
        return ReadInt(prop);
    }

    /// <summary>
    /// Sprint 144: alguns endpoints Moloni (simplifiedInvoices/insert, etc) devolvem a resposta
    /// dentro de um array de 1 elemento em vez de um objecto plano. Desempacotar para que
    /// GetInt/GetString/GetIntAny continuem a funcionar uniformemente.
    /// </summary>
    private static JsonElement UnwrapArrayIfNeeded(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
            return element[0];
        return element;
    }

    private static int ReadInt(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)) return value;
        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return value;
        return 0;
    }

    private static decimal ReadDecimal(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var value)) return value;
        if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return value;
        return 0m;
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

    private static string? GetString(JsonElement element, string name)
    {
        // Sprint 144: array-aware como GetInt.
        var target = UnwrapArrayIfNeeded(element);
        if (target.ValueKind != JsonValueKind.Object) return null;
        return target.TryGetProperty(name, out var prop) && prop.ValueKind != JsonValueKind.Null
            ? prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString()
            : null;
    }
}
