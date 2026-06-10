using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Relatorios;

namespace RepairDesk.Tests.Relatorios;

/// <summary>
/// Sprint 541: o Relatório IVA deixa de assumir 23% embutido em TODOS os documentos. Cascata:
/// 1) totais exatos do Moloni (documents/getOne) — margem 0%, taxas 6/13/23, isenções;
/// 2) IVA por linha (Vendas têm IvaRate+Condicao por item) quando Moloni está inacessível;
/// 3) estimativa 23% só no resto (Reparações/Trabalhos offline), marcada IvaExato=false.
/// O teste crítico: venda em regime da margem NÃO pode contar duas vezes (23% do total + IVA
/// da margem) — era exatamente o que acontecia antes deste sprint.
/// </summary>
public class RelatorioIvaExatoTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"iva-exato-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(TenantId));

    private static async Task SeedTenantAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant { Id = TenantId, Name = "LopesTech", RegimeFiscal = RegimeFiscal.RegimeNormalIva });
        db.TenantBillingSettings.Add(new TenantBillingSettings
        {
            TenantId = TenantId,
            Provider = BillingProvider.Moloni,
            ApiKeyCipherText = "cifrado",
            CompanyId = 1,
        });
        await db.SaveChangesAsync();
    }

    private static RelatorioFiscalService NewService(AppDbContext db, IMoloniClient moloni) =>
        new(new RelatorioFiscalRepository(db),
            new TenantRepository(db),
            new FixedTenant(TenantId),
            new TenantBillingSettingsRepository(db),
            moloni,
            NullLogger<RelatorioFiscalService>.Instance);

    [Fact]
    public async Task VendaRegimeMargem_ComMoloniExato_NaoContaDuasVezes()
    {
        await using var db = NewDb();
        await SeedTenantAsync(db);

        var part = new Part { TenantId = TenantId, Nome = "iPhone 12 recond", Sku = "IP12", CustoUnitarioCents = 40000 };
        db.Parts.Add(part);
        db.Vendas.Add(new Venda
        {
            TenantId = TenantId, Numero = 1, Status = VendaStatus.Paga,
            InvoiceExternalId = "100", InvoiceNumber = "FR 2026/1",
            InvoiceEmittedAt = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            Items = new List<VendaItem>
            {
                // 500€ em regime da margem (doc Moloni sai a 0% IVA, M13)
                new() { TenantId = TenantId, Descricao = "iPhone 12", Quantidade = 1, PrecoUnitarioCents = 50000, IvaRate = 23m, Condicao = CondicaoArtigo.Recondicionado, Part = part },
            },
        });
        await db.SaveChangesAsync();

        // Moloni devolve o documento real: total 500€, base 500€ (0% IVA — regime da margem).
        var moloni = new StubMoloni { Fiscal = new MoloniDocumentFiscal(1, 50000, 50000) };
        var report = await NewService(db, moloni).GetIvaAsync(2026, 2);

        // IVA liquidado = SÓ o IVA da margem: (50000−40000) × 23/123 ≈ 1870.
        // ANTES deste sprint: 23/123 × 50000 (9350, errado) + 1870 = 11220 — dupla contagem.
        report.IvaRegimeMargemCents.Should().Be(1870);
        report.IvaLiquidadoCents.Should().Be(1870);
        report.Documentos.Single().IvaCents.Should().Be(0);
        report.Documentos.Single().IvaExato.Should().BeTrue();
    }

    [Fact]
    public async Task VendaMista_MoloniOffline_UsaIvaPorLinha()
    {
        await using var db = NewDb();
        await SeedTenantAsync(db);

        var part = new Part { TenantId = TenantId, Nome = "iPhone 12 recond", Sku = "IP12", CustoUnitarioCents = 40000 };
        db.Parts.Add(part);
        db.Vendas.Add(new Venda
        {
            TenantId = TenantId, Numero = 2, Status = VendaStatus.Paga,
            InvoiceExternalId = "101", InvoiceNumber = "FR 2026/2",
            InvoiceEmittedAt = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc),
            Items = new List<VendaItem>
            {
                // linha margem (contribui 0 para o IVA do doc; margem 10000 apurada à parte)
                new() { TenantId = TenantId, Descricao = "iPhone 12", Quantidade = 1, PrecoUnitarioCents = 50000, IvaRate = 23m, Condicao = CondicaoArtigo.Recondicionado, Part = part },
                // linha normal a 23%: 123€ → IVA embutido 23€
                new() { TenantId = TenantId, Descricao = "Capa", Quantidade = 1, PrecoUnitarioCents = 12300, IvaRate = 23m, Condicao = CondicaoArtigo.Novo },
                // linha a 6% (ex.: livro/manual): 106€ → IVA embutido 6€
                new() { TenantId = TenantId, Descricao = "Manual", Quantidade = 1, PrecoUnitarioCents = 10600, IvaRate = 6m, Condicao = CondicaoArtigo.Novo },
            },
        });
        await db.SaveChangesAsync();

        var moloni = new StubMoloni { Fiscal = null }; // Moloni inacessível → via local
        var report = await NewService(db, moloni).GetIvaAsync(2026, 2);

        var doc = report.Documentos.Single();
        doc.IvaCents.Should().Be(2300 + 600);   // por linha, taxas reais — NÃO 23% do total
        doc.IvaExato.Should().BeTrue();         // per-linha conta como exato (não é estimativa)
        report.IvaRegimeMargemCents.Should().Be(1870);
        report.IvaLiquidadoCents.Should().Be(2300 + 600 + 1870);
    }

    [Fact]
    public async Task Reparacao_MoloniOffline_MantemEstimativa23_Marcada()
    {
        await using var db = NewDb();
        await SeedTenantAsync(db);

        var clienteId = Guid.NewGuid();
        db.Clientes.Add(new Cliente { Id = clienteId, TenantId = TenantId, Nome = "Maria", Telefone = "910000000" });
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = TenantId, Numero = 1, ClienteId = clienteId,
            Equipamento = "iPhone X", Avaria = "Ecrã",
            PrecoFinalCents = 12300,
            InvoiceExternalId = "102", InvoiceNumber = "FS 2026/3",
            InvoiceEmittedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        var moloni = new StubMoloni { Fiscal = null };
        var report = await NewService(db, moloni).GetIvaAsync(2026, 2);

        var doc = report.Documentos.Single();
        doc.IvaCents.Should().Be(2300); // 123€ → estimativa 23% embutido (sem dados por linha)
        doc.IvaExato.Should().BeFalse();
    }

    [Fact]
    public async Task Reparacao_ComMoloniExato_UsaTotaisDoDocumento()
    {
        await using var db = NewDb();
        await SeedTenantAsync(db);

        var clienteId = Guid.NewGuid();
        db.Clientes.Add(new Cliente { Id = clienteId, TenantId = TenantId, Nome = "João", Telefone = "920000000" });
        db.Reparacoes.Add(new Reparacao
        {
            TenantId = TenantId, Numero = 2, ClienteId = clienteId,
            Equipamento = "MacBook", Avaria = "Bateria",
            PrecoFinalCents = 12300,
            InvoiceExternalId = "103", InvoiceNumber = "FT 2026/4",
            InvoiceEmittedAt = new DateTime(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        // Doc emitido com IVA 0% + isenção (o Mender já o permite na ficha): total=base=123€.
        var moloni = new StubMoloni { Fiscal = new MoloniDocumentFiscal(1, 12300, 12300) };
        var report = await NewService(db, moloni).GetIvaAsync(2026, 2);

        var doc = report.Documentos.Single();
        doc.IvaCents.Should().Be(0);    // exato do Moloni — não os 2300 da estimativa
        doc.BaseCents.Should().Be(12300);
        doc.IvaExato.Should().BeTrue();
    }

    /// <summary>Stub mínimo: só o GetDocumentFiscalAsync interessa; o resto não é chamado neste fluxo.</summary>
    private sealed class StubMoloni : IMoloniClient
    {
        public MoloniDocumentFiscal? Fiscal { get; set; }

        public Task<MoloniDocumentFiscal?> GetDocumentFiscalAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default)
            => Task.FromResult(Fiscal);
        public Task<int?> GetDocumentStatusAsync(TenantBillingSettings settings, int documentId, CancellationToken ct = default)
            => Task.FromResult(Fiscal?.Status);

        // Não usados neste fluxo:
        public Task TestConnectionAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BillingSerieDto>> GetSeriesAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> FindCustomerIdByVatAsync(TenantBillingSettings settings, string vat, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MoloniInvoiceResult> InsertInvoiceAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MoloniEstimateResult> InsertEstimateAsync(TenantBillingSettings settings, MoloniInvoiceDraft draft, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> GetEstimateStatusAsync(TenantBillingSettings settings, int estimateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MoloniInvoiceResult> ConvertEstimateToInvoiceAsync(TenantBillingSettings settings, int estimateId, BillingDocumentType? documentTypeOverride = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream> GetPdfStreamAsync(TenantBillingSettings settings, string documentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MoloniInvoiceResult> InsertCreditNoteAsync(TenantBillingSettings settings, MoloniCreditNoteDraft draft, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> CancelDocumentAsync(TenantBillingSettings settings, int documentId, string observation, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ConnectViaPasswordGrantAsync(TenantBillingSettings settings, string username, string password, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ExchangeAuthorizationCodeAsync(TenantBillingSettings settings, string code, string redirectUri, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniCompanyDto>> GetCompaniesAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniProductDto>> GetProductsAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniTaxDto>> GetTaxesAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniPaymentMethodDto>> GetPaymentMethodsAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniMaturityDateDto>> GetMaturityDatesAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniCustomerDto>> GetCustomersAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MoloniProductDto> InsertProductAsync(TenantBillingSettings settings, string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MoloniCustomerDto> InsertCustomerAsync(TenantBillingSettings settings, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateCustomerAsync(TenantBillingSettings settings, int customerId, string name, string vat, string? morada = null, string? codigoPostal = null, string? localidade = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniDocumentRow>> ListDocumentsAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoloniDocumentRow>> ListReceiptsAsync(TenantBillingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MoloniReceiptResult> InsertReceiptAsync(TenantBillingSettings settings, int customerId, int documentId, int valueCents, string? notes, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
