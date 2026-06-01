using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RepairDesk.API.Cash;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Cash;

/// <summary>
/// Sprint 498: pagamento online de reparação entra na caixa/Z-report sem contexto HTTP
/// (chamado pelo webhook IFTHENPAY, anónimo). Verifica escopo por tenant, bucket MBWay
/// (não a gaveta) e idempotência.
/// </summary>
public class CashServiceTests
{
    [Fact]
    public async Task RecordReparacaoPaymentAsync_SemContexto_CriaMovimentoMBWayNoBucket()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb();
        var svc = NewService(db);

        await svc.RecordReparacaoPaymentAsync(tenantId, Guid.NewGuid(), 4500, PaymentMethod.MBWay);

        var movs = await db.CashMovements.IgnoreQueryFilters().Where(m => m.TenantId == tenantId).ToListAsync();
        movs.Should().ContainSingle();
        movs[0].Type.Should().Be(CashMovementType.PagamentoCliente);
        movs[0].PaymentMethod.Should().Be(PaymentMethod.MBWay);
        movs[0].AmountCents.Should().Be(4500);
        movs[0].ReparacaoId.Should().NotBeNull();

        var closing = await db.DailyClosings.IgnoreQueryFilters().FirstAsync(c => c.TenantId == tenantId);
        closing.MbwayCents.Should().Be(4500);
        // MBWay não é dinheiro na gaveta → não mexe o esperado em dinheiro.
        closing.ExpectedClosingCents.Should().Be(0);
    }

    [Fact]
    public async Task RecordReparacaoPaymentAsync_WebhookReentregue_NaoDuplica()
    {
        var tenantId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        await using var db = NewDb();
        var svc = NewService(db);

        await svc.RecordReparacaoPaymentAsync(tenantId, repId, 4500, PaymentMethod.MBWay);
        await svc.RecordReparacaoPaymentAsync(tenantId, repId, 4500, PaymentMethod.MBWay);

        (await db.CashMovements.IgnoreQueryFilters().CountAsync(m => m.ReparacaoId == repId)).Should().Be(1);
        var closing = await db.DailyClosings.IgnoreQueryFilters().FirstAsync(c => c.TenantId == tenantId);
        closing.MbwayCents.Should().Be(4500); // somado uma só vez
    }

    [Fact]
    public async Task RecordReparacaoPaymentAsync_EscopaPorTenant()
    {
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        await using var db = NewDb();
        var svc = NewService(db);

        await svc.RecordReparacaoPaymentAsync(t1, Guid.NewGuid(), 1000, PaymentMethod.MBWay);
        await svc.RecordReparacaoPaymentAsync(t2, Guid.NewGuid(), 2000, PaymentMethod.Multibanco);

        var c1 = await db.DailyClosings.IgnoreQueryFilters().FirstAsync(c => c.TenantId == t1);
        var c2 = await db.DailyClosings.IgnoreQueryFilters().FirstAsync(c => c.TenantId == t2);
        c1.MbwayCents.Should().Be(1000);
        c1.MultibancoCents.Should().Be(0);
        c2.MultibancoCents.Should().Be(2000);
        c2.MbwayCents.Should().Be(0);
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cash-{Guid.NewGuid():N}").Options,
            new NullTenantContext());

    private static CashService NewService(AppDbContext db) =>
        new(db, new NullTenantContext(), TimeProvider.System, new NoOpAudit(), new HttpContextAccessor());

    // Simula o contexto do webhook anónimo: sem tenant.
    private sealed class NullTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public bool HasTenant => false;
    }

    private sealed class NoOpAudit : IAuditLogger
    {
        public Task LogAsync(AuditAction action, string entityType, Guid? entityId, object? changes = null,
            Guid? tenantId = null, Guid? appUserId = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
