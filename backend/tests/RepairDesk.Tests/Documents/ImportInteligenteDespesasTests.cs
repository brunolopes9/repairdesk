using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Documents;

/// <summary>
/// Sprint 543: import inteligente de Despesas — a categoria do fornecedor é conhecida (lista
/// KnownDespesaSuppliers: Anthropic→Software) ou aprendida na aprovação (last-wins), e o modal
/// pré-seleciona-a na fatura seguinte. Mata o ritual de escolher "Software" à mão todos os meses.
/// </summary>
public class ImportInteligenteDespesasTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Theory]
    [InlineData("Anthropic", DespesaCategoria.Software)]
    [InlineData("ANTHROPIC PBC", DespesaCategoria.Software)]
    [InlineData("Vodafone Portugal", DespesaCategoria.Comunicacoes)]
    [InlineData("CTT Expresso", DespesaCategoria.Transporte)]
    [InlineData("Galp Energia", DespesaCategoria.Combustivel)]
    public void KnownSuppliers_SugereCategoriaCerta(string nome, DespesaCategoria esperada)
        => KnownDespesaSuppliers.SuggestCategoria(nome).Should().Be(esperada);

    [Theory]
    [InlineData("Tudo4Mobile")]      // peças — não está na lista (classificador trata)
    [InlineData("Romeo Lda")]        // "meo" embutido NÃO pode disparar (match por palavra)
    [InlineData("Google")]           // ambíguo (Workspace vs Ads) — deliberadamente fora
    [InlineData(null)]
    [InlineData("")]
    public void KnownSuppliers_DesconhecidoOuAmbiguo_NaoSugere(string? nome)
        => KnownDespesaSuppliers.SuggestCategoria(nome).Should().BeNull();

    [Fact]
    public async Task AprovarComoDespesa_AprendeCategoriaDoFornecedor()
    {
        await using var db = NewDb();
        var fornecedor = new Fornecedor { TenantId = TenantId, Name = "Anthropic" };
        db.Fornecedores.Add(fornecedor);
        db.SupplierInvoiceImports.Add(new SupplierInvoiceImport
        {
            TenantId = TenantId,
            Fornecedor = fornecedor,
            FornecedorNameRaw = "Anthropic",
            PdfSha256 = new string('a', 64),
            PdfRelativePath = "2026/06/anthropic/fatura.pdf",
            ParsedTotalCents = 2150,
        });
        await db.SaveChangesAsync();

        var importId = await db.SupplierInvoiceImports.Select(x => x.Id).SingleAsync();
        var service = NewService(db);

        var dto = await service.ApproveAsync(importId, new ApproveSupplierInvoiceRequest(
            ValorCents: 2150,
            Descricao: "Anthropic · API Junho",
            Categoria: DespesaCategoria.Software,
            Data: null, Fornecedor: null, NumeroEncomenda: null, Notas: null));

        dto.Status.Should().Be("Approved");
        // A regra ficou aprendida — a próxima fatura da Anthropic vem pré-classificada.
        (await db.Fornecedores.SingleAsync(f => f.Id == fornecedor.Id))
            .DefaultDespesaCategoria.Should().Be(DespesaCategoria.Software);
        // E a despesa real foi criada com a categoria escolhida.
        (await db.Despesas.SingleAsync()).Categoria.Should().Be(DespesaCategoria.Software);
    }

    [Fact]
    public async Task DtoExpoeCategoriaAprendida_ParaPreSelecaoNoModal()
    {
        await using var db = NewDb();
        var fornecedor = new Fornecedor
        {
            TenantId = TenantId, Name = "Vodafone",
            DefaultDespesaCategoria = DespesaCategoria.Comunicacoes,
        };
        db.Fornecedores.Add(fornecedor);
        db.SupplierInvoiceImports.Add(new SupplierInvoiceImport
        {
            TenantId = TenantId,
            Fornecedor = fornecedor,
            PdfSha256 = new string('b', 64),
            PdfRelativePath = "2026/06/vodafone/fatura.pdf",
        });
        await db.SaveChangesAsync();

        var importId = await db.SupplierInvoiceImports.Select(x => x.Id).SingleAsync();
        var dto = await NewService(db).ApproveAsync(importId, new ApproveSupplierInvoiceRequest(
            ValorCents: 3500, Descricao: "Vodafone · Junho", Categoria: DespesaCategoria.Comunicacoes,
            Data: null, Fornecedor: null, NumeroEncomenda: null, Notas: null));

        // O DTO (ToDto) expõe a regra — é o que o modal usa para pré-selecionar a categoria.
        dto.FornecedorDefaultDespesaCategoria.Should().Be((int)DespesaCategoria.Comunicacoes);
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"import-intel-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(TenantId));

    private static SupplierInvoiceImportService NewService(AppDbContext db)
    {
        var despesas = new DespesaService(
            new DespesaRepository(db), new PartRepository(db),
            new CreateDespesaValidator(), new UpdateDespesaValidator());
        // ApproveAsync/GetAsync só tocam em repo+despesas+audit — as deps de parsing/storage não
        // entram neste caminho (null! deliberado; rebenta alto se algum refactor as passar a usar).
        return new SupplierInvoiceImportService(
            new SupplierInvoiceImportRepository(db),
            new FixedTenant(TenantId),
            new FornecedorRepository(db),
            storage: null!,
            despesas,
            skuMappings: null!,
            new PartRepository(db),
            fingerprinting: null!,
            llmParser: null!,
            new NoOpAudit(),
            NullLogger<SupplierInvoiceImportService>.Instance);
    }

    private sealed class NoOpAudit : IAuditLogger
    {
        public Task LogAsync(AuditAction action, string entityType, Guid? entityId, object? changes = null,
            Guid? tenantId = null, Guid? appUserId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
