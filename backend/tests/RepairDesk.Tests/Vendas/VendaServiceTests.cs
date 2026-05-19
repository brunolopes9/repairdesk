using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Vendas;
using DomainValidationException = RepairDesk.Core.Exceptions.ValidationException;

namespace RepairDesk.Tests.Vendas;

public class VendaServiceTests
{
    [Fact]
    public async Task CreateAsync_OverStock_ThrowsValidation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var part = new Part { TenantId = tenantId, Nome = "Capa", QtdStock = 1, CustoUnitarioCents = 500 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var act = () => service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 2, 1000, 0, 23)
        ], null));

        await act.Should().ThrowAsync<DomainValidationException>()
            .Where(e => e.Code == "stock_insuficiente");
    }

    [Fact]
    public async Task MarcarPagaAsync_DecrementsStockAndCreatesMovement()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var part = new Part { TenantId = tenantId, Nome = "Película", QtdStock = 3, CustoUnitarioCents = 400 };
        db.Parts.Add(part);
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId);
        var venda = await service.CreateAsync(new CreateVendaRequest(null, [
            new CreateVendaItemRequest(part.Id, null, 2, 1000, 0, 23)
        ], null));

        await service.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(PaymentMethod.MBWay));

        var updated = await db.Parts.SingleAsync(p => p.Id == part.Id);
        updated.QtdStock.Should().Be(1);
        db.PartMovimentos.Should().ContainSingle(m =>
            m.PartId == part.Id &&
            m.VendaId == venda.Id &&
            m.Quantidade == -2 &&
            m.Motivo == PartMovimentoMotivo.VendaCliente);
    }

    [Fact]
    public async Task EmitirFaturaAsync_ExistingInvoice_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var venda = new Venda
        {
            TenantId = tenantId,
            Numero = 1,
            Status = VendaStatus.Paga,
            TotalCents = 1000,
            IvaCents = 187,
            InvoiceExternalId = "123",
            InvoiceNumber = "FA 2026/123",
            InvoicePdfUrl = "https://moloni.test/fa.pdf",
            InvoiceEmittedAt = DateTime.UtcNow,
        };
        db.Vendas.Add(venda);
        await db.SaveChangesAsync();

        var billing = new FakeBillingProvider();
        var service = NewService(db, tenantId, billing);

        var invoice = await service.EmitirFaturaAsync(venda.Id);

        invoice.Number.Should().Be("FA 2026/123");
        billing.EmitVendaCalls.Should().Be(0);
    }

    private static AppDbContext NewDb(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vendas-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    private static VendaService NewService(AppDbContext db, Guid tenantId, IBillingProvider? billing = null)
        => new(
            new VendaRepository(db),
            new PartRepository(db),
            new ClienteRepository(db),
            new TestTenantContext(tenantId),
            new FakeBillingSettingsRepository(),
            billing ?? new FakeBillingProvider(),
            new FakeMoloniNoOp());

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => true;
    }

    private sealed class FakeBillingSettingsRepository : ITenantBillingSettingsRepository
    {
        public Task<TenantBillingSettings?> FindByTenantIdAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<TenantBillingSettings?>(null);
        public Task AddAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeBillingProvider : IBillingProvider
    {
        public int EmitVendaCalls { get; private set; }
        public Task<InvoiceDto> EmitReparacaoInvoiceAsync(Guid reparacaoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<InvoiceDto> EmitTrabalhoInvoiceAsync(Guid trabalhoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<InvoiceDto> EmitVendaInvoiceAsync(Guid vendaId, CancellationToken ct = default)
        {
            EmitVendaCalls++;
            return Task.FromResult(new InvoiceDto("FA 2026/1", null, DateTime.UtcNow));
        }
        public Task<Stream> GetPdfStreamAsync(string invoiceId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
    }

    /// <summary>No-op IMoloniClient para testes — só serve para satisfazer DI.</summary>
    private sealed class FakeMoloniNoOp : IMoloniClient
    {
        public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BillingSerieDto>)Array.Empty<BillingSerieDto>());
        public Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default)
            => Task.FromResult<int?>(null);
        public Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("1", "FA 2026/1", null, DateTime.UtcNow));
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream());
        public Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default)
            => Task.FromResult(new MoloniInvoiceResult("NC1", "NC 2026/1", null, DateTime.UtcNow));
        public Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<int?> GetDocumentStatusAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default)
            => Task.FromResult<int?>(1);
        public Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniCompanyDto>)Array.Empty<MoloniCompanyDto>());
        public Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniProductDto>)Array.Empty<MoloniProductDto>());
        public Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniTaxDto>)Array.Empty<MoloniTaxDto>());
        public Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniPaymentMethodDto>)Array.Empty<MoloniPaymentMethodDto>());
        public Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniMaturityDateDto>)Array.Empty<MoloniMaturityDateDto>());
        public Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MoloniCustomerDto>)Array.Empty<MoloniCustomerDto>());
        public Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default)
            => Task.FromResult(new MoloniProductDto(1, name, true));
        public Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, CancellationToken ct = default)
            => Task.FromResult(new MoloniCustomerDto(1, name, vat, true));
    }
}
