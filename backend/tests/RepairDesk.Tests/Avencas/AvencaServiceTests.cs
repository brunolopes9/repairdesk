using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RepairDesk.API.HostedServices;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Avencas;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Trabalhos;

namespace RepairDesk.Tests.Avencas;

/// <summary>
/// Sprint 546 (Doc 93 #1): avenças — faturação recorrente. A avença é uma fábrica de Trabalhos:
/// EmitirAsync cria o Trabalho do período + FT Moloni e avança a ProximaEmissao A PARTIR do
/// período (não de hoje — emitir atrasado não deriva a cadência). Período consumido quando o
/// Trabalho existe, mesmo que a emissão Moloni falhe (retry é na ficha do Trabalho — evita
/// trabalhos duplicados do mesmo mês).
/// </summary>
public class AvencaServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Emitir_CriaTrabalhoDoPeriodo_EmiteFT_AvancaProxima()
    {
        await using var db = NewDb();
        var clienteId = await SeedClienteAsync(db);
        var trabalhos = new FakeTrabalhos();
        var billing = new FakeBilling();
        var service = NewService(db, trabalhos, billing);

        var periodo = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var avenca = await service.CreateAsync(new SaveAvencaRequest(
            clienteId, "Manutenção website", 35000, 23m, JobCategory.Software, 1, periodo));

        var result = await service.EmitirAsync(avenca.Id);

        // Trabalho do período com o valor e cliente certos.
        trabalhos.Criado.Should().NotBeNull();
        trabalhos.Criado!.Titulo.Should().Be("Manutenção website — 06/2026");
        trabalhos.Criado.OrcamentoCents.Should().Be(35000);
        trabalhos.Criado.ClienteId.Should().Be(clienteId);
        // FT a crédito com a taxa da avença.
        billing.EmitiuTrabalhoId.Should().Be(result.TrabalhoId);
        billing.VatPercent.Should().Be(23m);
        billing.DocType.Should().Be(BillingDocumentType.Fatura);
        result.InvoiceNumber.Should().Be("FT 2026/9");
        // Próxima avança a partir do PERÍODO (06 → 07), não de hoje.
        result.Avenca.ProximaEmissao.Should().Be(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        result.Avenca.UltimoTrabalhoId.Should().Be(result.TrabalhoId);
    }

    [Fact]
    public async Task Emitir_Inativa_Recusa()
    {
        await using var db = NewDb();
        var clienteId = await SeedClienteAsync(db);
        var service = NewService(db, new FakeTrabalhos(), new FakeBilling());
        var avenca = await service.CreateAsync(new SaveAvencaRequest(
            clienteId, "Avença parada", 10000, 23m, JobCategory.Software, 1, DateTime.UtcNow.Date, Ativa: false));

        var act = () => service.EmitirAsync(avenca.Id);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Code.Should().Be("avenca_inativa");
    }

    [Fact]
    public async Task Emitir_MoloniFalha_TrabalhoFicaCriado_PeriodoConsumido()
    {
        await using var db = NewDb();
        var clienteId = await SeedClienteAsync(db);
        var trabalhos = new FakeTrabalhos();
        var billing = new FakeBilling { Falhar = true };
        var service = NewService(db, trabalhos, billing);
        var periodo = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var avenca = await service.CreateAsync(new SaveAvencaRequest(
            clienteId, "Manutenção app", 50000, 23m, JobCategory.Software, 1, periodo));

        var act = () => service.EmitirAsync(avenca.Id);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Code.Should().Be("avenca_emissao_parcial");

        // O Trabalho ficou criado e o período CONSUMIDO — retry da fatura é na ficha do Trabalho,
        // não na avença (senão duplicava o trabalho do mês).
        trabalhos.Criado.Should().NotBeNull();
        var persisted = await db.Avencas.SingleAsync(a => a.Id == avenca.Id);
        persisted.ProximaEmissao.Should().Be(periodo.AddMonths(1));
        persisted.UltimoTrabalhoId.Should().NotBeNull();
    }

    [Fact]
    public async Task CronListDevidas_SoAtivasComProximaVencida()
    {
        await using var db = NewDb();
        var clienteId = await SeedClienteAsync(db);
        var hoje = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        db.Avencas.AddRange(
            new Avenca { TenantId = TenantId, ClienteId = clienteId, Descricao = "Devida", ValorCents = 10000, ProximaEmissao = hoje.AddDays(-1) },
            new Avenca { TenantId = TenantId, ClienteId = clienteId, Descricao = "Hoje", ValorCents = 20000, ProximaEmissao = hoje },
            new Avenca { TenantId = TenantId, ClienteId = clienteId, Descricao = "Futura", ValorCents = 30000, ProximaEmissao = hoje.AddDays(5) },
            new Avenca { TenantId = TenantId, ClienteId = clienteId, Descricao = "Inativa", ValorCents = 40000, ProximaEmissao = hoje.AddDays(-9), Ativa = false });
        await db.SaveChangesAsync();

        var devidas = await AvencasHostedService.ListDevidasAsync(db, hoje);

        devidas.Select(d => d.Descricao).Should().BeEquivalentTo("Devida", "Hoje");
        devidas.Sum(d => d.ValorCents).Should().Be(30000);
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"avencas-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(TenantId));

    private static async Task<Guid> SeedClienteAsync(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Clientes.Add(new Cliente { Id = id, TenantId = TenantId, Nome = "Pátio Fidalgo", Telefone = "910000000" });
        await db.SaveChangesAsync();
        return id;
    }

    private static AvencaService NewService(AppDbContext db, FakeTrabalhos trabalhos, FakeBilling billing) =>
        new(new AvencaRepository(db), new ClienteRepository(db), trabalhos, billing,
            NullLogger<AvencaService>.Instance);

    private sealed class FakeTrabalhos : ITrabalhoService
    {
        public CreateTrabalhoRequest? Criado { get; private set; }
        public Guid TrabalhoId { get; } = Guid.NewGuid();

        public Task<TrabalhoDto> CreateAsync(CreateTrabalhoRequest req, CancellationToken ct = default)
        {
            Criado = req;
            return Task.FromResult(new TrabalhoDto(
                TrabalhoId, 1, null, req.Titulo, req.Descricao, req.Categoria, TrabalhoStatus.Orcamento,
                DateTime.UtcNow, null, null, req.OrcamentoCents, null, 0, req.Notas, PaymentStatus.NaoPago,
                0, 0, BillingProvider.None, null, null, null, null, null, null, null, null, null, null));
        }

        public Task<PagedResult<TrabalhoDto>> SearchAsync(string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TrabalhoDto>> ListPagasSemFaturaAsync(int limit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TrabalhoDto> GetAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TrabalhoDto> UpdateAsync(Guid id, UpdateTrabalhoRequest req, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TrabalhoDto> ReabrirAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TrabalhoDto> AnularFaturaAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TrabalhoDto> EmitirOrcamentoMoloniAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TrabalhoDto> ConverterOrcamentoEmFaturaAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeBilling : IBillingProvider
    {
        public bool Falhar { get; init; }
        public Guid? EmitiuTrabalhoId { get; private set; }
        public decimal? VatPercent { get; private set; }
        public BillingDocumentType? DocType { get; private set; }

        public Task<InvoiceDto> EmitTrabalhoInvoiceAsync(Guid trabalhoId, decimal? vatPercent, string? paymentMethod, BillingDocumentType? documentTypeOverride = null, CancellationToken ct = default)
        {
            if (Falhar) throw new BillingProviderException("moloni_down", "Moloni indisponível.");
            EmitiuTrabalhoId = trabalhoId;
            VatPercent = vatPercent;
            DocType = documentTypeOverride;
            return Task.FromResult(new InvoiceDto("FT 2026/9", null, DateTime.UtcNow));
        }

        public Task<InvoiceDto> EmitReparacaoInvoiceAsync(Guid reparacaoId, decimal? vatPercent, string? paymentMethod, bool discriminarMaoObra = true, BillingDocumentType? documentTypeOverride = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InvoiceDto> EmitVendaInvoiceAsync(Guid vendaId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream> GetPdfStreamAsync(string invoiceId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
