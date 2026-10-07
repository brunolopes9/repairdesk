using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
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
        db.VendaFotos.Add(new VendaFoto
        {
            TenantId = tenantId,
            VendaId = rep.Id,
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

        var saved = await db.VendaComunicacoes.SingleAsync();
        saved.VendaId.Should().Be(rep.Id);
        saved.ClienteId.Should().Be(rep.ClienteId!.Value);
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
        rep.Estado = VendaEstado.Entregue;
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
        db.VendaComunicacoes.Add(new VendaComunicacao
        {
            TenantId = tenantId, VendaId = rep.Id, ClienteId = rep.ClienteId!.Value,
            Tipo = ComunicacaoTipo.PortalCliente, Direcao = ComunicacaoDirecao.Outbound,
            Texto = "Amanhã ao fim do dia.", CreatedByUserId = Guid.NewGuid(),
        });
        // Nota interna de telefone — NÃO deve aparecer no portal.
        db.VendaComunicacoes.Add(new VendaComunicacao
        {
            TenantId = tenantId, VendaId = rep.Id, ClienteId = rep.ClienteId!.Value,
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
    public async Task PaymentService_ConfirmaPagamentoReparacao_AvisaLojaEPortalMostraPago()
    {
        // Núcleo do fluxo de dinheiro: webhook confirma → loja avisada e o portal mostra "pago".
        var tenantId = Guid.NewGuid();
        await using var db = NewDb(tenantId);
        var rep = await SeedRepairAsync(db, tenantId);
        db.Payments.Add(new RepairDesk.Core.Entities.Payment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, VendaId = rep.Id,
            Method = PaymentMethod.MBWay, Provider = PaymentProvider.Ifthenpay,
            AmountCents = 12000, Status = PaymentStatus.NaoPago, ProviderRef = "req-abc-123",
        });
        await db.SaveChangesAsync();

        var push = new CapturingPushQueue();
        var payments = new RepairDesk.Services.Payments.PaymentService(
            new PaymentRepository(db),
            Array.Empty<RepairDesk.Core.Abstractions.IPaymentProvider>(),
            new VendaRepository(db),
            push);

        await payments.ApplyStatusUpdateAsync("req-abc-123",
            new RepairDesk.Core.Abstractions.PaymentStatusSnapshot(PaymentStatus.Pago, DateTime.UtcNow, null));

        push.Jobs.Should().ContainSingle()
            .Which.Should().Match<RepairDesk.Services.Push.StaffPushJob>(j =>
                j.TenantId == tenantId && j.Body.Contains("120") && j.Body.Contains("MBWay"));
        (await NewService(db, tenantId, TenantPreferencesDefaults.Create()).GetBySlugAsync(rep.PublicSlug!)).Pago.Should().BeTrue();
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
            Id = Guid.NewGuid(), TenantId = tenantId, VendaId = rep.Id,
            Method = PaymentMethod.MBWay, Provider = PaymentProvider.Ifthenpay,
            AmountCents = 12000, Status = PaymentStatus.NaoPago, ProviderRef = "req-dup",
        });
        await db.SaveChangesAsync();

        var push = new CapturingPushQueue();
        var payments = new RepairDesk.Services.Payments.PaymentService(
            new PaymentRepository(db), Array.Empty<RepairDesk.Core.Abstractions.IPaymentProvider>(),
            new VendaRepository(db), push);

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

        rep.Estado = VendaEstado.Entregue;
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
        rep.Estado = VendaEstado.Entregue;
        await db.SaveChangesAsync();
        var prefs = TenantPreferencesDefaults.Create();
        prefs = prefs with { Portal = prefs.Portal with { MostrarOrcamento = false } };
        var service = NewService(db, tenantId, prefs);

        (await service.GetBySlugAsync(rep.PublicSlug!)).Pago.Should().BeFalse();
    }

    private static async Task<Venda> SeedRepairAsync(AppDbContext db, Guid tenantId)
    {
        var tenant = new Tenant { Id = tenantId, Name = "LopesTech" };
        var cliente = new Cliente { TenantId = tenantId, Nome = "Bruno Lopes", Telefone = "910000000" };
        var rep = new Venda
        {
            TenantId = tenantId,
            Tipo = VendaTipo.Reparacao,
            Estado = VendaEstado.Orcamento,
            Cliente = cliente,
            ClienteId = cliente.Id,
            Numero = 1,
            Equipamento = "iPhone 13",
            Problema = "Ecra partido",
            TotalCents = 12000,
            PublicSlug = $"slug{Guid.NewGuid():N}"[..12],
        };
        rep.Items.Add(new VendaItem { TenantId = tenantId, Descricao = "Ecrã + mão de obra", Quantidade = 1, PrecoUnitarioCents = 12000, IvaRate = 23m });
        rep.Timeline.Add(new VendaEstadoLog { TenantId = tenantId, EstadoTo = VendaEstado.Orcamento, MudouEm = DateTime.UtcNow });
        db.Tenants.Add(tenant);
        db.Clientes.Add(cliente);
        db.Vendas.Add(rep);
        await db.SaveChangesAsync();
        return rep;
    }

    private static PublicPortalService NewService(AppDbContext db, Guid tenantId, TenantPreferencesRoot prefs)
    {
        var vendas = new VendaRepository(db);
        return new PublicPortalService(
            vendas,
            null!, // IVendaService: só usado ao aceitar/recusar o orçamento (coberto nos testes de API)
            new TenantRepository(db),
            new GarantiaRepository(db),
            new AvaliacaoRepository(db),
            new VendaFotoRepository(db),
            new FakeTenantPreferencesService(prefs),
            new VendaComunicacaoRepository(db),
            new RepairDesk.Services.Push.StaffPushQueue(),
            new RepairDesk.Services.Payments.PaymentService(new PaymentRepository(db), Array.Empty<RepairDesk.Core.Abstractions.IPaymentProvider>(), vendas, new RepairDesk.Services.Push.StaffPushQueue()),
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
