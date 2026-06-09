using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Despesas;

namespace RepairDesk.Tests.Despesas;

/// <summary>
/// Sprint 540: converter uma "Despesa-Peças" (limbo) numa Part real. O ponto crítico é fiscal:
/// tem de ser um MOVE com efeito NULO no Relatório IVA — mesmo valor, mesmo período. Estes testes
/// usam o AppDbContext real (com o interceptor StampAuditFields, que reescreve CreatedAt em insert)
/// para provar que a dedução de IVA não duplica nem salta de trimestre.
/// </summary>
public class DespesaConvertToStockTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"convert-stock-{Guid.NewGuid():N}")
                .Options,
            new FixedTenant(Tenant));

    private static DespesaService NewService(AppDbContext db) =>
        // ConvertToStockAsync não usa os validators (só Create/Update), por isso passamos null!.
        new(new DespesaRepository(db), new PartRepository(db), null!, null!);

    [Fact]
    public async Task Converter_CriaPartComEntrada_PreservaPeriodo_eRemoveDespesa()
    {
        var dataCompra = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        await using var db = NewDb();
        var despesa = new Despesa
        {
            TenantId = Tenant, Descricao = "Ecrã Samsung A15", Categoria = DespesaCategoria.Pecas,
            ValorCents = 5000, Data = dataCompra, Fornecedor = "Tudo4Mobile",
        };
        db.Despesas.Add(despesa);
        await db.SaveChangesAsync();

        var result = await NewService(db).ConvertToStockAsync(despesa.Id, new ConvertDespesaToStockRequest(Quantidade: 1));

        result.CustoUnitarioCents.Should().Be(5000);
        result.Quantidade.Should().Be(1);

        var part = await db.Parts.SingleAsync(p => p.Id == result.PartId);
        part.Nome.Should().Be("Ecrã Samsung A15");
        part.QtdStock.Should().Be(1);
        part.CustoUnitarioCents.Should().Be(5000);
        part.Fornecedor.Should().Be("Tudo4Mobile");

        var mov = await db.PartMovimentos.SingleAsync(m => m.PartId == result.PartId);
        mov.Motivo.Should().Be(PartMovimentoMotivo.Entrada);
        mov.Quantidade.Should().Be(1);
        // O ponto-chave: o movimento fica no PERÍODO da compra original (não em "agora"), apesar
        // de o interceptor carimbar CreatedAt=now no insert. Senão a dedução saltaria de trimestre.
        mov.CreatedAt.Should().Be(dataCompra);

        // A despesa foi movida (soft-delete) — o filtro global exclui-a, não duplica a compra.
        (await db.Despesas.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Converter_TemEfeitoNuloNoRelatorioIVA()
    {
        var dataCompra = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var marInicio = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var marFim = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var db = NewDb();
        db.Despesas.Add(new Despesa
        {
            TenantId = Tenant, Descricao = "Peças A15", Categoria = DespesaCategoria.Pecas,
            ValorCents = 5000, Data = dataCompra,
        });
        await db.SaveChangesAsync();

        var fiscal = new RelatorioFiscalRepository(db);
        var antes = await fiscal.SumPecasCustoComIvaAsync(marInicio, marFim);
        antes.Should().Be(5000); // conta via despesa

        var despesaId = await db.Despesas.Select(d => d.Id).SingleAsync();
        await NewService(db).ConvertToStockAsync(despesaId, new ConvertDespesaToStockRequest(Quantidade: 1));

        var depois = await fiscal.SumPecasCustoComIvaAsync(marInicio, marFim);
        // Mesmo valor, mesmo trimestre: agora conta via PartMovimento Entrada. Se o período não
        // fosse preservado, o movimento cairia em Junho e Março passaria a 0 — este teste apanharia.
        depois.Should().Be(antes);
    }

    [Fact]
    public async Task Converter_PreservaReverseCharge()
    {
        await using var db = NewDb();
        db.Despesas.Add(new Despesa
        {
            TenantId = Tenant, Descricao = "Peças Utopya (FR)", Categoria = DespesaCategoria.Pecas,
            ValorCents = 8000, Data = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            ReverseCharge = true, // intra-UE: IVA não dedutível em PT
        });
        await db.SaveChangesAsync();

        var id = await db.Despesas.Select(d => d.Id).SingleAsync();
        var result = await NewService(db).ConvertToStockAsync(id, new ConvertDespesaToStockRequest(Quantidade: 1));

        var mov = await db.PartMovimentos.SingleAsync(m => m.PartId == result.PartId);
        mov.ReverseCharge.Should().BeTrue(); // o tratamento IVA da compra segue para o stock
    }

    [Fact]
    public async Task Converter_RecusaDespesaOpEx()
    {
        await using var db = NewDb();
        db.Despesas.Add(new Despesa
        {
            TenantId = Tenant, Descricao = "Licença software", Categoria = DespesaCategoria.Software,
            ValorCents = 1200, Data = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var id = await db.Despesas.Select(d => d.Id).SingleAsync();
        var act = () => NewService(db).ConvertToStockAsync(id, new ConvertDespesaToStockRequest());

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Code.Should().Be("despesa_nao_convertivel");
    }

    private sealed class FixedTenant : ITenantContext
    {
        private readonly Guid _id;
        public FixedTenant(Guid id) => _id = id;
        public Guid? TenantId => _id;
        public bool HasTenant => true;
    }
}
