using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Dashboard;
using RepairDesk.Services.Documentos;

namespace RepairDesk.Tests.Dashboard;

/// <summary>
/// Sprint 549 (Doc 93 #6 — "Controlo de Tesouraria" do Moloni): faturado vs recebido por mês.
/// Faturado segue as regras do Extrato (anulados/RG/ORC fora, NC subtrai, valores c/ IVA);
/// recebido reusa a Tendência (receita paga). Uma FT em dívida conta como faturado e só
/// passa a recebido quando o pagamento for marcado.
/// </summary>
public class TesourariaTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public void ComputeFaturadoPorMes_AplicaRegrasDoExtrato()
    {
        var maio = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var junho = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);
        var docs = new List<DocumentoDto>
        {
            Doc("FT", maio, 10000),
            Doc("FS", maio, 2500),
            Doc("NC", maio, 1000),              // subtrai
            Doc("RG", maio, 10000),             // liquidação — não é faturação
            Doc("ORC", maio, 99999),            // proposta — fora
            Doc("FT", maio, 7777, "Anulado"),   // anulado — fora
            Doc("VD", junho, 4000),             // mês seguinte, bucket próprio
        };

        var porMes = DashboardService.ComputeFaturadoPorMes(docs);

        porMes[(2026, 5)].Should().Be(10000 + 2500 - 1000);
        porMes[(2026, 6)].Should().Be(4000);
        porMes.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTesouraria_JuntaFaturadoERecebidoPorMes()
    {
        await using var db = NewDb();

        // Recebido: 1 venda paga este mês (150,00 €). FT em dívida do mês passado NÃO conta
        // como recebido (reparação não paga) — só como faturado, via fake de documentos.
        var clienteId = Guid.NewGuid();
        db.Clientes.Add(new Cliente { Id = clienteId, TenantId = Tenant, Nome = "Ana", Telefone = "910000000" });
        db.Vendas.Add(new Venda
        {
            TenantId = Tenant, Numero = 1, Status = VendaStatus.Paga, Data = DateTime.UtcNow,
            Items = new List<VendaItem>
            {
                new() { TenantId = Tenant, Descricao = "Película", Quantidade = 1, PrecoUnitarioCents = 15000, IvaRate = 23m },
            },
        });
        await db.SaveChangesAsync();

        var agora = DateTime.UtcNow;
        var mesPassado = agora.AddMonths(-1);
        var docs = new List<DocumentoDto>
        {
            Doc("FT", new DateTime(mesPassado.Year, mesPassado.Month, 5, 0, 0, 0, DateTimeKind.Utc), 30000),
            Doc("FS", new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc), 15000),
        };

        var service = new DashboardService(
            new DashboardRepository(db),
            avaliacoes: null!,
            garantias: null!,
            new FakeDocumentos(new DocumentosListDto(docs, docs.Count, 0, 0, 0)));

        var result = await service.GetTesourariaAsync(6);

        result.Meses.Should().HaveCount(6);
        var atual = result.Meses[^1];
        atual.Ano.Should().Be(agora.Year);
        atual.Mes.Should().Be(agora.Month);
        atual.FaturadoCents.Should().Be(15000);
        atual.RecebidoCents.Should().Be(15000);

        var anterior = result.Meses[^2];
        anterior.FaturadoCents.Should().Be(30000); // FT em dívida: faturado…
        anterior.RecebidoCents.Should().Be(0);     // …mas nada recebido nesse mês
    }

    private static DocumentoDto Doc(string tipoCodigo, DateTime data, int totalCents, string estado = "Ativo") =>
        new(
            Guid.NewGuid(), "Venda", 1, tipoCodigo, tipoCodigo,
            $"{tipoCodigo} 2026/1", null, null, "Moloni", data,
            null, null, null, totalCents, 0, totalCents, estado);

    private sealed class FakeDocumentos : IDocumentoService
    {
        private readonly DocumentosListDto _lista;
        public FakeDocumentos(DocumentosListDto lista) => _lista = lista;

        public Task<DocumentosListDto> ListVendasAsync(DocumentosFiltro filtro, CancellationToken ct = default)
            => Task.FromResult(_lista);
        public Task<byte[]> ExportVendasCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<ReciboResultDto> EmitirReciboAsync(int documentId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"tesouraria-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(Tenant));

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
