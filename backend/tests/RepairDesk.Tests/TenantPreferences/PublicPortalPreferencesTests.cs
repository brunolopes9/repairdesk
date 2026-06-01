using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.EquipmentFields;
using RepairDesk.Services.PublicPortal;
using RepairDesk.Services.TenantPreferences;

namespace RepairDesk.Tests.TenantPreferences;

public class PublicPortalPreferencesTests
{
    [Fact]
    public async Task GetBySlugAsync_MostrarFotosFalse_ReturnsNoPhotos()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        db.ReparacaoFotos.Add(new ReparacaoFoto
        {
            TenantId = tenantId,
            ReparacaoId = rep.Id,
            StorageKey = "photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Size = 100,
            VisivelNoPortal = true,
        });
        await db.SaveChangesAsync();
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { MostrarFotos = false } };
        var service = NewService(db, tenantId, prefs);

        var dto = await service.GetBySlugAsync(rep.PublicSlug!);

        dto.Fotos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBySlugAsync_MostrarOrcamentoFalse_HidesMoneyFields()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { MostrarOrcamento = false } };
        var service = NewService(db, tenantId, prefs);

        var dto = await service.GetBySlugAsync(rep.PublicSlug!);

        dto.OrcamentoCents.Should().BeNull();
        dto.PrecoFinalCents.Should().BeNull();
        dto.TemPrecoFinal.Should().BeFalse();
    }

    [Fact]
    public async Task AprovarOrcamentoAsync_WhenDisabled_ThrowsForbidden()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { PermitirAprovarOrcamento = false } };
        var service = NewService(db, tenantId, prefs);

        var act = () => service.AprovarOrcamentoAsync(rep.PublicSlug!, true);

        await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.ForbiddenException>()
            .Where(e => e.Code == "orcamento_aprovacao_desactivada");
    }

    // Sprint 480: mensagens enviadas pelo cliente via portal público.

    [Fact]
    public async Task SubmeterMensagemAsync_CriaComunicacaoInboundPortalCliente()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());

        await service.SubmeterMensagemAsync(rep.PublicSlug!, "Posso passar amanhã às 17h?");

        var saved = await db.ReparacaoComunicacoes.SingleAsync();
        saved.ReparacaoId.Should().Be(rep.Id);
        saved.ClienteId.Should().Be(rep.ClienteId);
        saved.Tipo.Should().Be(ComunicacaoTipo.PortalCliente);
        saved.Direcao.Should().Be(ComunicacaoDirecao.Inbound);
        saved.Texto.Should().Be("Posso passar amanhã às 17h?");
        // Sentinela "anónimo portal" — sem user identificado.
        saved.CreatedByUserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task SubmeterMensagemAsync_TextoVazio_ThrowsValidation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());

        var act = () => service.SubmeterMensagemAsync(rep.PublicSlug!, "   ");

        await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.ValidationException>()
            .Where(e => e.Code == "texto_invalido");
    }

    [Fact]
    public async Task SubmeterMensagemAsync_ReparacaoEntregue_ThrowsConflict()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        rep.Estado = RepairStatus.Entregue;
        rep.EntregueEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());

        var act = () => service.SubmeterMensagemAsync(rep.PublicSlug!, "Olá!");

        await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.ConflictException>()
            .Where(e => e.Code == "estado_fechado");
    }

    [Fact]
    public async Task SubmeterMensagemAsync_SlugInexistente_ThrowsNotFound()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        _ = await SeedRepairAsync(db, tenantId);
        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());

        var act = () => service.SubmeterMensagemAsync("naoexiste", "olá");

        await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task GetBySlugAsync_ExpoeConversaPortalCliente_NaoNotasInternas()
    {
        // Sprint 482: o fio de conversa do portal só expõe Tipo=PortalCliente.
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);

        // Mensagem do cliente (Inbound, PortalCliente) — deve aparecer.
        await service_SubmeterMensagem(db, tenantId, rep.PublicSlug!, "Quando fica pronto?");
        // Resposta staff (Outbound, PortalCliente) — deve aparecer.
        db.ReparacaoComunicacoes.Add(new ReparacaoComunicacao
        {
            TenantId = tenantId, ReparacaoId = rep.Id, ClienteId = rep.ClienteId,
            Tipo = ComunicacaoTipo.PortalCliente, Direcao = ComunicacaoDirecao.Outbound,
            Texto = "Amanhã ao fim do dia.", CreatedByUserId = Guid.NewGuid(),
        });
        // Nota interna de telefone — NÃO deve aparecer no portal.
        db.ReparacaoComunicacoes.Add(new ReparacaoComunicacao
        {
            TenantId = tenantId, ReparacaoId = rep.Id, ClienteId = rep.ClienteId,
            Tipo = ComunicacaoTipo.Telefone, Direcao = ComunicacaoDirecao.Interna,
            Texto = "Cliente parece chato, cobrar adiantado.", CreatedByUserId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());
        var dto = await service.GetBySlugAsync(rep.PublicSlug!);

        dto.Conversa.Should().HaveCount(2);
        dto.Conversa[0].Texto.Should().Be("Quando fica pronto?");
        dto.Conversa[0].DeStaff.Should().BeFalse();
        dto.Conversa[1].Texto.Should().Be("Amanhã ao fim do dia.");
        dto.Conversa[1].DeStaff.Should().BeTrue();
        dto.Conversa.Should().NotContain(m => m.Texto.Contains("cobrar adiantado"));
    }

    private static Task service_SubmeterMensagem(AppDbContext db, Guid tenantId, string slug, string texto)
        => NewService(db, tenantId, TenantPreferencesDefaults.Create()).SubmeterMensagemAsync(slug, texto);

    // Sprint 493: MBWay no portal cliente.

    [Fact]
    public async Task IniciarPagamentoMbWay_TelefoneInvalido_ThrowsValidation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());

        var act = () => service.IniciarPagamentoMbWayAsync(rep.PublicSlug!, "12345", default);

        await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.ValidationException>()
            .Where(e => e.Code == "telefone_invalido");
    }

    [Fact]
    public async Task IniciarPagamentoMbWay_SemIfthenpay_ThrowsConflict()
    {
        // NewService usa IfthenpayOptions() não configurado → MBWay indisponível.
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());

        var act = () => service.IniciarPagamentoMbWayAsync(rep.PublicSlug!, "912345678", default);

        await act.Should().ThrowAsync<RepairDesk.Core.Exceptions.ConflictException>()
            .Where(e => e.Code == "mbway_indisponivel");
    }

    [Fact]
    public async Task PaymentService_ConfirmaPagamentoReparacao_MarcaPaga()
    {
        // Núcleo do fluxo de dinheiro: webhook confirma → reparação fica Paga.
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        rep.EstadoPagamento.Should().NotBe(PaymentStatus.Pago);

        db.Payments.Add(new RepairDesk.Core.Entities.Payment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ReparacaoId = rep.Id,
            Method = PaymentMethod.MBWay, Provider = PaymentProvider.Ifthenpay,
            AmountCents = 12000, Status = PaymentStatus.NaoPago, ProviderRef = "req-abc-123",
        });
        await db.SaveChangesAsync();

        var push = new CapturingPushQueue();
        var payments = new RepairDesk.Services.Payments.PaymentService(
            new PaymentRepository(db),
            Array.Empty<RepairDesk.Core.Abstractions.IPaymentProvider>(),
            new ReparacaoRepository(db),
            push);

        await payments.ApplyStatusUpdateAsync("req-abc-123",
            new RepairDesk.Core.Abstractions.PaymentStatusSnapshot(PaymentStatus.Pago, DateTime.UtcNow, null));

        var fresh = await new ReparacaoRepository(db).FindByIdAsync(rep.Id);
        fresh!.EstadoPagamento.Should().Be(PaymentStatus.Pago);

        // Sprint 495: a loja é notificada quando o dinheiro entra.
        push.Jobs.Should().ContainSingle()
            .Which.Should().Match<RepairDesk.Services.Push.StaffPushJob>(j =>
                j.TenantId == tenantId && j.Body.Contains("120") && j.Body.Contains("MBWay"));
    }

    [Fact]
    public async Task PaymentService_ConfirmaPagamento_Idempotente_NaoDuplicaPush()
    {
        // Webhook reentregue: segunda confirmação não marca de novo nem duplica push.
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        db.Payments.Add(new RepairDesk.Core.Entities.Payment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ReparacaoId = rep.Id,
            Method = PaymentMethod.MBWay, Provider = PaymentProvider.Ifthenpay,
            AmountCents = 12000, Status = PaymentStatus.NaoPago, ProviderRef = "req-dup",
        });
        await db.SaveChangesAsync();

        var push = new CapturingPushQueue();
        var payments = new RepairDesk.Services.Payments.PaymentService(
            new PaymentRepository(db), Array.Empty<RepairDesk.Core.Abstractions.IPaymentProvider>(),
            new ReparacaoRepository(db), push);

        var snap = new RepairDesk.Core.Abstractions.PaymentStatusSnapshot(PaymentStatus.Pago, DateTime.UtcNow, null);
        await payments.ApplyStatusUpdateAsync("req-dup", snap);
        await payments.ApplyStatusUpdateAsync("req-dup", snap);

        push.Jobs.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_RefleteEstadoPagamento()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        var service = NewService(db, tenantId, TenantPreferencesDefaults.Create());

        (await service.GetBySlugAsync(rep.PublicSlug!)).Pago.Should().BeFalse();

        rep.EstadoPagamento = PaymentStatus.Pago;
        await db.SaveChangesAsync();

        (await service.GetBySlugAsync(rep.PublicSlug!)).Pago.Should().BeTrue();
    }

    [Fact]
    public async Task GetBySlugAsync_MostrarOrcamentoFalse_NaoRevelaPago()
    {
        // Sem orçamento visível, o estado de pagamento também fica oculto.
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        rep.EstadoPagamento = PaymentStatus.Pago;
        await db.SaveChangesAsync();
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { MostrarOrcamento = false } };
        var service = NewService(db, tenantId, prefs);

        (await service.GetBySlugAsync(rep.PublicSlug!)).Pago.Should().BeFalse();
    }

    private static async Task<Reparacao> SeedRepairAsync(AppDbContext db, Guid tenantId)
    {
        var tenant = new Tenant { Id = tenantId, Name = "LopesTech" };
        var cliente = new Cliente { TenantId = tenantId, Nome = "Bruno Lopes", Telefone = "910000000" };
        var rep = new Reparacao
        {
            TenantId = tenantId,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Numero = 1,
            Equipamento = "iPhone 13",
            Avaria = "Ecra partido",
            Diagnostico = "Trocar ecra",
            Estado = RepairStatus.Orcamento,
            EstadoSince = DateTime.UtcNow,
            OrcamentoCents = 12000,
            PrecoFinalCents = 12000,
            PublicSlug = $"slug{Guid.NewGuid():N}"[..12],
        };
        rep.Timeline.Add(new ReparacaoEstadoLog
        {
            TenantId = tenantId,
            Reparacao = rep,
            ReparacaoId = rep.Id,
            EstadoTo = RepairStatus.Orcamento,
            MudouEm = DateTime.UtcNow,
        });
        db.Tenants.Add(tenant);
        db.Clientes.Add(cliente);
        db.Reparacoes.Add(rep);
        await db.SaveChangesAsync();
        return rep;
    }

    private static PublicPortalService NewService(AppDbContext db, Guid tenantId, TenantPreferencesRoot prefs)
    {
        var tenantContext = new TestTenantContext(tenantId);
        var reparacoes = new ReparacaoRepository(db);
        return new PublicPortalService(
            reparacoes,
            new TenantRepository(db),
            new DiagnosticoRepository(db),
            new GarantiaRepository(db),
            new AvaliacaoRepository(db),
            new ReparacaoFotoRepository(db),
            new EquipmentFieldService(new EquipmentFieldRepository(db), reparacoes, tenantContext),
            new VendaRepository(db),
            new FakeTenantPreferencesService(prefs),
            new ReparacaoComunicacaoRepository(db),
            new RepairDesk.Services.Push.StaffPushQueue(),
            new RepairDesk.Services.Payments.PaymentService(new PaymentRepository(db), Array.Empty<RepairDesk.Core.Abstractions.IPaymentProvider>(), reparacoes, new RepairDesk.Services.Push.StaffPushQueue()),
            new RepairDesk.Services.Payments.Ifthenpay.IfthenpayOptions());
    }

    private static AppDbContext NewDb(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"portal-prefs-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, new TestTenantContext(tenantId));
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public bool HasTenant => true;
    }

    // Sprint 495: captura pushes enfileirados para asserção em testes.
    private sealed class CapturingPushQueue : RepairDesk.Services.Push.IStaffPushQueue
    {
        public List<RepairDesk.Services.Push.StaffPushJob> Jobs { get; } = [];
        public ValueTask EnqueueAsync(RepairDesk.Services.Push.StaffPushJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }
        public ValueTask<RepairDesk.Services.Push.StaffPushJob> DequeueAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeTenantPreferencesService(TenantPreferencesRoot prefs) : ITenantPreferencesService
    {
        public Task<TenantPreferencesRoot> GetAsync(CancellationToken ct = default) => Task.FromResult(prefs);
        public Task<TenantPreferencesRoot> GetForTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(prefs);
        public Task<TenantPreferencesRoot> UpdateAsync(TenantPreferencesRoot preferences, CancellationToken ct = default) => Task.FromResult(preferences);
        public Task<TenantPreferencesRoot> ResetGroupAsync(string group, CancellationToken ct = default) => Task.FromResult(prefs);
    }
}
