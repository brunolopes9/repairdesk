using System.Text;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Billing;

namespace RepairDesk.API.Infrastructure;

internal sealed class E2eMoloniClient : IMoloniClient
{
    private static int _nextDocumentId = 900000;

    public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BillingSerieDto>>([new BillingSerieDto(1, "Serie E2E", "E2E", true)]);

    public Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default)
        => Task.FromResult<int?>(settings.FallbackCustomerId ?? 50);

    public Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextDocumentId);
        var now = DateTime.UtcNow;
        return Task.FromResult(new MoloniInvoiceResult(
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"E2E/{now:yyyyMMdd}/{id}",
            $"/api/billing/e2e/{id}.pdf",
            now));
    }

    public Task<MoloniEstimateResult> InsertEstimateAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextDocumentId);
        var now = DateTime.UtcNow;
        return Task.FromResult(new MoloniEstimateResult(
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"E2E-ORC/{now:yyyyMMdd}/{id}",
            $"/api/billing/e2e/orcamento-{id}.pdf",
            now));
    }

    public Task<int?> GetEstimateStatusAsync(TenantBillingSettings settings, int estimateId, CancellationToken ct = default)
        => Task.FromResult<int?>(1);

    public Task<MoloniInvoiceResult> ConvertEstimateToInvoiceAsync(
        TenantBillingSettings settings,
        int estimateId,
        BillingDocumentType? documentTypeOverride = null,
        CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextDocumentId);
        var now = DateTime.UtcNow;
        return Task.FromResult(new MoloniInvoiceResult(
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"E2E-CONV/{now:yyyyMMdd}/{id}",
            $"/api/billing/e2e/convertida-{id}.pdf",
            now));
    }

    public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default)
    {
        Stream stream = new MemoryStream(Encoding.UTF8.GetBytes($"RepairDesk E2E fake PDF {documentId}"));
        return Task.FromResult(stream);
    }

    public Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextDocumentId);
        var now = DateTime.UtcNow;
        return Task.FromResult(new MoloniInvoiceResult(
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"E2E-NC/{now:yyyyMMdd}/{id}",
            $"/api/billing/e2e/nc-{id}.pdf",
            now));
    }

    public Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<int?> GetDocumentStatusAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default)
        => Task.FromResult<int?>(1);

    public Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default)
    {
        settings.ApiKeyCipherText ??= "e2e-api-key";
        settings.RefreshTokenCipherText ??= "e2e-refresh-token";
        return Task.CompletedTask;
    }

    public Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default)
    {
        settings.ApiKeyCipherText ??= "e2e-api-key";
        settings.RefreshTokenCipherText ??= "e2e-refresh-token";
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MoloniCompanyDto>>([new MoloniCompanyDto(1, "LopesTech E2E")]);

    public Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MoloniProductDto>>([new MoloniProductDto(10, "Servico de reparacao", true)]);

    public Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MoloniTaxDto>>([
            new MoloniTaxDto(23, "IVA 23%", 23m, true, null),
            new MoloniTaxDto(24, "Isento Art. 53", 0m, true, "M01")
        ]);

    public Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MoloniPaymentMethodDto>>([new MoloniPaymentMethodDto(30, "Numerario", true)]);

    public Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MoloniMaturityDateDto>>([new MoloniMaturityDateDto(40, "Pronto pagamento", true)]);

    public Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MoloniCustomerDto>>([new MoloniCustomerDto(50, "Consumidor Final", "999999990", true)]);

    public Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default)
        => Task.FromResult(new MoloniProductDto(10, name, true));

    public Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, CancellationToken ct = default)
        => Task.FromResult(new MoloniCustomerDto(50, name, vat, true));
}
