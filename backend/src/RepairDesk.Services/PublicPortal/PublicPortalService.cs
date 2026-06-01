using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.EquipmentFields;
using RepairDesk.Services.Push;
using RepairDesk.Services.TenantPreferences;

namespace RepairDesk.Services.PublicPortal;

public interface IPublicPortalService
{
    Task<PublicRepairDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<PublicRepairDto> AprovarOrcamentoAsync(string slug, bool aceitar, CancellationToken ct = default);
    Task<PublicGarantiaDto> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default);
    Task<AvaliacaoSubmittedDto> SubmeterAvaliacaoAsync(string repairSlug, int score, string? comentario, bool publicarTestemunho, CancellationToken ct = default);
    /// <summary>Sprint 480: cliente escreve do portal público. Cria comunicação Inbound + push staff.</summary>
    Task SubmeterMensagemAsync(string slug, string texto, CancellationToken ct = default);
    /// <summary>Sprint 493: cliente inicia pagamento MBWay da reparação pelo portal (IFTHENPAY).</summary>
    Task<PublicPagamentoDto> IniciarPagamentoMbWayAsync(string slug, string telefone, CancellationToken ct = default);
}

public class PublicPortalService : IPublicPortalService
{
    private readonly IReparacaoRepository _repo;
    private readonly ITenantRepository _tenants;
    private readonly IDiagnosticoRepository _diagnostico;
    private readonly IGarantiaRepository _garantias;
    private readonly IAvaliacaoRepository _avaliacoes;
    private readonly IReparacaoFotoRepository _fotos;
    private readonly IEquipmentFieldService _equipmentFields;
    private readonly IVendaRepository _vendas;
    private readonly ITenantPreferencesService _preferences;
    private readonly IReparacaoComunicacaoRepository _comunicacoes;
    private readonly IStaffPushQueue _push;
    private readonly Payments.IPaymentService _payments;
    private readonly Payments.Ifthenpay.IfthenpayOptions _ifthenpay;

    public PublicPortalService(
        IReparacaoRepository repo,
        ITenantRepository tenants,
        IDiagnosticoRepository diagnostico,
        IGarantiaRepository garantias,
        IAvaliacaoRepository avaliacoes,
        IReparacaoFotoRepository fotos,
        IEquipmentFieldService equipmentFields,
        IVendaRepository vendas,
        ITenantPreferencesService preferences,
        IReparacaoComunicacaoRepository comunicacoes,
        IStaffPushQueue push,
        Payments.IPaymentService payments,
        Payments.Ifthenpay.IfthenpayOptions ifthenpay)
    {
        _payments = payments;
        _ifthenpay = ifthenpay;
        _repo = repo;
        _tenants = tenants;
        _diagnostico = diagnostico;
        _garantias = garantias;
        _avaliacoes = avaliacoes;
        _fotos = fotos;
        _equipmentFields = equipmentFields;
        _vendas = vendas;
        _preferences = preferences;
        _comunicacoes = comunicacoes;
        _push = push;
    }

    public async Task<PublicGarantiaDto> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Garantia", slug);

        var g = await _garantias.FindBySlugAsync(slug, ct)
            ?? throw new NotFoundException("Garantia", slug);

        var tenant = await _tenants.FindByIdAsync(g.TenantId, ct);
        var agora = DateTime.UtcNow;
        var diasRestantes = (int)Math.Max(0, (g.DataFim - agora).TotalDays);
        var activa = !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;

        string equipamentoPublico;
        string origem;
        string? documentoRef;
        string? numeroFatura;
        IReadOnlyList<PublicGarantiaItemDto>? items;

        if (g.SourceType == GarantiaSourceType.Venda && g.Venda is not null)
        {
            var primeiro = g.Venda.Items.FirstOrDefault();
            equipamentoPublico = primeiro?.Descricao ?? "Artigos vendidos";
            origem = "Venda";
            documentoRef = $"Venda #{g.Venda.Numero:D5}";
            numeroFatura = g.Venda.InvoiceNumber;
            items = g.Venda.Items
                .Select(i => new PublicGarantiaItemDto(
                    i.Descricao,
                    i.Quantidade,
                    i.PrecoUnitarioCents,
                    i.TotalCents,
                    string.IsNullOrEmpty(i.Imei) ? null : ImeiValidator.Mask(i.Imei)))
                .ToList();
        }
        else
        {
            equipamentoPublico = g.Reparacao?.Equipamento ?? "Equipamento";
            origem = "Reparacao";
            documentoRef = g.Reparacao is not null ? $"Reparação #{g.Reparacao.Numero:D5}" : null;
            numeroFatura = null;
            items = null;
        }

        return new PublicGarantiaDto(
            Slug: g.Slug,
            EquipamentoPublico: equipamentoPublico,
            Loja: tenant?.LegalName ?? tenant?.Name ?? "Oficina",
            LogoUrl: tenant?.LogoUrl,
            DataInicio: g.DataInicio,
            DataFim: g.DataFim,
            DiasGarantia: g.DiasGarantia,
            Activa: activa,
            Anulada: g.Anulada,
            DiasRestantes: diasRestantes,
            Cobertura: g.Cobertura,
            Exclusoes: g.Exclusoes,
            Origem: origem,
            DocumentoReferencia: documentoRef,
            NumeroFatura: numeroFatura,
            Items: items,
            LojaEmail: tenant?.Email,
            LojaTelefone: tenant?.Phone);
    }

    public async Task<AvaliacaoSubmittedDto> SubmeterAvaliacaoAsync(string repairSlug, int score, string? comentario, bool publicarTestemunho, CancellationToken ct = default)
    {
        if (score < 1 || score > 5)
            throw new ValidationException("score_invalido", "Score deve ser entre 1 e 5.");
        if (string.IsNullOrWhiteSpace(repairSlug))
            throw new NotFoundException("Reparacao", repairSlug);

        var rep = await _repo.FindByPublicSlugWithTimelineAsync(repairSlug, ct)
            ?? throw new NotFoundException("Reparacao", repairSlug);

        if (rep.Estado != RepairStatus.Entregue)
            throw new ConflictException("nao_entregue", "Esta reparação ainda não foi entregue.");

        var existente = await _avaliacoes.FindByReparacaoAsync(rep.Id, ct);
        if (existente is not null)
            throw new ConflictException("ja_avaliado", "Esta reparação já foi avaliada.");

        var tenant = await _tenants.FindByIdAsync(rep.TenantId, ct);
        var prefs = await _preferences.GetForTenantAsync(rep.TenantId, ct);
        var googleReviewUrl = prefs.Portal.GoogleReviewUrl ?? tenant?.GoogleReviewUrl;
        var dirigirGoogle = score >= prefs.Portal.GoogleReviewMinScore && !string.IsNullOrWhiteSpace(googleReviewUrl);

        var avaliacao = new Avaliacao
        {
            TenantId = rep.TenantId,
            ReparacaoId = rep.Id,
            Score = score,
            Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(),
            PublicarTestemunho = publicarTestemunho,
            PedidoGoogleReview = dirigirGoogle,
        };
        await _avaliacoes.AddAsync(avaliacao, ct);
        await _avaliacoes.SaveAsync(ct);

        return new AvaliacaoSubmittedDto(score, avaliacao.Comentario, dirigirGoogle ? googleReviewUrl : null);
    }

    public async Task<PublicRepairDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Reparacao", slug);

        var rep = await _repo.FindByPublicSlugWithTimelineAsync(slug, ct)
            ?? throw new NotFoundException("Reparacao", slug);
        var prefs = await _preferences.GetForTenantAsync(rep.TenantId, ct);

        // Compliance: não revelar reparações > 2 anos
        if (rep.RecebidoEm() < DateTime.UtcNow.AddYears(-2))
            throw new NotFoundException("Reparacao", slug);

        var tenant = await _tenants.FindByIdAsync(rep.TenantId, ct);
        var diag = await _diagnostico.FindExecucaoByReparacaoAsync(rep.Id, ct);
        var garantia = await _garantias.FindByReparacaoAsync(rep.Id, ct);
        var avaliacao = await _avaliacoes.FindByReparacaoAsync(rep.Id, ct);
        var fotos = await _fotos.ListPublicByReparacaoIdAsync(rep.Id, ct);
        var campos = await _equipmentFields.GetValuesAsync(rep.Id, visibleInPortalOnly: true, ct);
        var cobertura = await ResolveCoberturaGarantiaAsync(rep, ct);
        var conversa = await LoadConversaAsync(rep.Id, ct);
        return ToDto(rep, tenant, diag, garantia, avaliacao is not null, fotos, campos, cobertura, prefs.Portal, conversa);
    }

    /// <summary>
    /// Sprint 482: fio de conversa do portal. Só comunicações tipo PortalCliente — cliente
    /// (Inbound) + respostas do staff (Outbound). Cronológico ascendente (chat-style).
    /// </summary>
    private async Task<IReadOnlyList<PublicConversaMsg>> LoadConversaAsync(Guid reparacaoId, CancellationToken ct)
    {
        var all = await _comunicacoes.ListByReparacaoAsync(reparacaoId, ct);
        return all
            .Where(c => c.Tipo == ComunicacaoTipo.PortalCliente)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new PublicConversaMsg(
                DeStaff: c.Direcao == ComunicacaoDirecao.Outbound,
                Texto: c.Texto,
                Em: c.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Sprint 88: indica ao cliente se esta reparação está coberta pela garantia da venda
    /// anterior do mesmo equipamento. Só expõe quando a garantia está activa.
    /// </summary>
    private async Task<PublicCoberturaGarantia?> ResolveCoberturaGarantiaAsync(Reparacao rep, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rep.Imei)) return null;
        var vendaRow = await _vendas.FindVendaByImeiAsync(rep.Imei, ct);
        if (vendaRow is null || vendaRow.Data >= rep.CreatedAt) return null;

        var garantiaVenda = await _garantias.FindByVendaAsync(vendaRow.VendaId, ct);
        var agora = DateTime.UtcNow;
        var activa = garantiaVenda is not null
            && !garantiaVenda.Anulada
            && agora >= garantiaVenda.DataInicio
            && agora <= garantiaVenda.DataFim;
        if (!activa || garantiaVenda is null) return null;

        return new PublicCoberturaGarantia(
            garantiaVenda.Slug,
            garantiaVenda.DataFim,
            (int)Math.Max(0, (garantiaVenda.DataFim - agora).TotalDays));
    }

    public async Task<PublicRepairDto> AprovarOrcamentoAsync(string slug, bool aceitar, CancellationToken ct = default)
    {
        var rep = await _repo.FindByPublicSlugWithTimelineAsync(slug, ct)
            ?? throw new NotFoundException("Reparacao", slug);
        var prefs = await _preferences.GetForTenantAsync(rep.TenantId, ct);

        if (!prefs.Portal.PermitirAprovarOrcamento)
            throw new ForbiddenException("orcamento_aprovacao_desactivada", "A aprovacao online de orcamentos esta desactivada nesta loja.");

        if (rep.OrcamentoCents is null)
            throw new ConflictException("sem_orcamento", "Esta reparação não tem orçamento para aprovar.");
        if (rep.OrcamentoAprovado)
            throw new ConflictException("ja_aprovado", "Este orçamento já foi aprovado anteriormente.");

        rep.OrcamentoAprovado = aceitar;
        if (!aceitar)
        {
            rep.Estado = Core.Enums.RepairStatus.Cancelado;
            rep.EstadoSince = DateTime.UtcNow;
        }
        await _repo.SaveAsync(ct);

        var tenant = await _tenants.FindByIdAsync(rep.TenantId, ct);
        var diag = await _diagnostico.FindExecucaoByReparacaoAsync(rep.Id, ct);
        var garantia = await _garantias.FindByReparacaoAsync(rep.Id, ct);
        var avaliacao = await _avaliacoes.FindByReparacaoAsync(rep.Id, ct);
        var fotos = await _fotos.ListPublicByReparacaoIdAsync(rep.Id, ct);
        var campos = await _equipmentFields.GetValuesAsync(rep.Id, visibleInPortalOnly: true, ct);
        var cobertura = await ResolveCoberturaGarantiaAsync(rep, ct);
        var conversa = await LoadConversaAsync(rep.Id, ct);
        return ToDto(rep, tenant, diag, garantia, avaliacao is not null, fotos, campos, cobertura, prefs.Portal, conversa);
    }

    public async Task SubmeterMensagemAsync(string slug, string texto, CancellationToken ct = default)
    {
        // Sprint 480: cliente envia mensagem via portal. Cria comunicação Inbound + push staff.
        // CreatedByUserId = Guid.Empty é sentinela "portal cliente" — UI distingue pelo tipo PortalCliente.
        var trimmed = (texto ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 2000)
            throw new ValidationException("texto_invalido", "Mensagem obrigatória (1 a 2000 caracteres).");

        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Reparacao", slug ?? "");

        var rep = await _repo.FindByPublicSlugWithTimelineAsync(slug, ct)
            ?? throw new NotFoundException("Reparacao", slug);

        // Compliance: não aceitar mensagens em reparações arquivadas (> 2 anos) — alinhado com GetBySlug.
        if (rep.RecebidoEm() < DateTime.UtcNow.AddYears(-2))
            throw new NotFoundException("Reparacao", slug);

        // Reparações em estado terminal (Entregue/Cancelado) não recebem nova conversa.
        if (rep.Estado is RepairStatus.Entregue or RepairStatus.Cancelado)
            throw new ConflictException("estado_fechado", "Esta reparação já foi fechada. Contacte-nos pelo telefone ou WhatsApp da loja.");

        var entry = new ReparacaoComunicacao
        {
            Id = Guid.NewGuid(),
            TenantId = rep.TenantId,
            ReparacaoId = rep.Id,
            ClienteId = rep.ClienteId,
            Tipo = ComunicacaoTipo.PortalCliente,
            Direcao = ComunicacaoDirecao.Inbound,
            Texto = trimmed,
            CreatedByUserId = Guid.Empty,
        };
        await _comunicacoes.AddAsync(entry, ct);
        await _comunicacoes.SaveAsync(ct);

        var nome = rep.Cliente?.Nome?.Split(' ').FirstOrDefault() ?? "Cliente";
        var preview = trimmed.Length > 80 ? trimmed[..80] + "…" : trimmed;
        await _push.EnqueueAsync(new StaffPushJob(
            rep.TenantId,
            $"💬 {nome} respondeu no portal",
            preview,
            $"/reparacoes/{rep.Id}",
            $"portal-msg-{rep.Id}"), ct);
    }

    public async Task<PublicPagamentoDto> IniciarPagamentoMbWayAsync(string slug, string telefone, CancellationToken ct = default)
    {
        // Sprint 493: pagamento MBWay da reparação pelo portal. Money-sensitive — validações estritas.
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Reparacao", slug ?? "");

        // Telefone PT: 9 dígitos começados por 9 (após limpar espaços/+351).
        var digits = new string((telefone ?? "").Where(char.IsDigit).ToArray());
        if (digits.StartsWith("351") && digits.Length == 12) digits = digits[3..];
        if (digits.Length != 9 || digits[0] != '9')
            throw new ValidationException("telefone_invalido", "Indica um número de telemóvel português válido (9 dígitos).");

        if (!_ifthenpay.IsConfigured || string.IsNullOrWhiteSpace(_ifthenpay.MBWayKey))
            throw new ConflictException("mbway_indisponivel", "Esta loja ainda não tem pagamento MBWay activo. Paga no balcão.");

        var rep = await _repo.FindByPublicSlugWithTimelineAsync(slug, ct)
            ?? throw new NotFoundException("Reparacao", slug);

        if (rep.RecebidoEm() < DateTime.UtcNow.AddYears(-2))
            throw new NotFoundException("Reparacao", slug);
        if (rep.EstadoPagamento == PaymentStatus.Pago)
            throw new ConflictException("ja_pago", "Esta reparação já está paga.");
        if (rep.Estado == RepairStatus.Cancelado)
            throw new ConflictException("cancelada", "Esta reparação foi cancelada.");

        var amount = rep.PrecoFinalCents ?? rep.OrcamentoCents ?? 0;
        if (amount <= 0)
            throw new ConflictException("sem_valor", "Ainda não há valor definido para pagar. Aguarda o orçamento da loja.");

        // Anti-spam de push MBWay: não reenviar se já há um pedido pendente recente (janela 4 min).
        var existentes = await _payments.GetByReparacaoAsync(rep.Id, ct);
        var agora = DateTime.UtcNow;
        var pendenteRecente = existentes.FirstOrDefault(p =>
            p.Status == PaymentStatus.NaoPago
            && p.Method == PaymentMethod.MBWay
            && p.ProviderRef != null
            && (p.ExpiresAt == null || p.ExpiresAt > agora));
        if (pendenteRecente is not null)
            return new PublicPagamentoDto("pendente", amount,
                "Já enviámos um pedido MBWay. Confirma na app (até 4 min) ou tenta novamente depois.",
                pendenteRecente.ExpiresAt);

        var tenant = await _tenants.FindByIdAsync(rep.TenantId, ct);
        var descricao = $"Reparação #{rep.Numero:D5}" + (tenant is not null ? $" · {tenant.LegalName ?? tenant.Name}" : "");

        var payment = await _payments.InitiateAsync(
            new PaymentInitiationRequest(
                TenantId: rep.TenantId,
                VendaId: null,
                Method: PaymentMethod.MBWay,
                AmountCents: amount,
                CustomerPhone: digits,
                CustomerEmail: rep.Cliente?.Email,
                Description: descricao,
                ReparacaoId: rep.Id),
            PaymentProvider.Ifthenpay, ct);

        if (string.IsNullOrWhiteSpace(payment.ProviderRef))
            throw new ConflictException("mbway_falhou", "Não foi possível iniciar o MBWay. Tenta novamente ou paga no balcão.");

        // Push staff: cliente iniciou pagamento (visibilidade no balcão).
        await _push.EnqueueAsync(new StaffPushJob(
            rep.TenantId,
            "💳 Pagamento MBWay iniciado",
            $"Reparação #{rep.Numero:D5} · {amount / 100m:F2}€ — a aguardar confirmação",
            $"/reparacoes/{rep.Id}",
            $"mbway-{rep.Id}"), ct);

        return new PublicPagamentoDto("pendente", amount,
            "Abre a app MBWay e confirma o pagamento (até 4 minutos).",
            payment.ExpiresAt);
    }

    private static PublicRepairDto ToDto(
        Reparacao rep,
        Tenant? tenant,
        DiagnosticoExecucao? diag,
        Garantia? garantia,
        bool jaAvaliado,
        IReadOnlyList<ReparacaoFoto> fotos,
        IReadOnlyList<EquipmentFieldValueDto> campos,
        PublicCoberturaGarantia? cobertura,
        PortalPrefs portal,
        IReadOnlyList<PublicConversaMsg> conversa)
    {
        var primeiroNome = rep.Cliente?.Nome?.Split(' ').FirstOrDefault() ?? "Cliente";
        var loja = new PublicLoja(
            tenant?.LegalName ?? tenant?.Name ?? "Oficina",
            tenant?.Phone,
            tenant?.Email,
            tenant?.Website,
            tenant?.LogoUrl);

        var timeline = portal.MostrarTimeline
            ? rep.Timeline
                .OrderBy(t => t.MudouEm)
                .Select(t => new PublicTimelineEntry(PublicEstadoMapper.From(t.EstadoTo), t.MudouEm))
                .ToList()
            : [];

        // Diagnóstico só exposto se está completado (caso contrário pode confundir cliente)
        int? healthScore = null;
        var destaques = new List<string>();
        if (portal.MostrarDiagnostico && diag is not null && diag.CompletadoEm.HasValue)
        {
            healthScore = diag.Score;
            destaques = diag.Items
                .Where(i => i.Resultado == DiagnosticoResultado.Avaria || i.Resultado == DiagnosticoResultado.Marginal)
                .OrderBy(i => i.Ordem)
                .Select(i => i.Resultado == DiagnosticoResultado.Marginal ? $"⚠️ {i.Label}" : $"❌ {i.Label}")
                .Take(8)
                .ToList();
        }

        return new PublicRepairDto(
            Slug: rep.PublicSlug!,
            EquipamentoPublico: rep.Equipamento,
            AvariaPublica: rep.Avaria,
            Diagnostico: portal.MostrarDiagnostico ? rep.Diagnostico : null,
            Estado: PublicEstadoMapper.From(rep.Estado),
            EstadoSince: rep.EstadoSince,
            RecebidoEm: rep.RecebidoEm(),
            EntregueEm: rep.EntregueEm,
            OrcamentoCents: portal.MostrarOrcamento ? rep.OrcamentoCents : null,
            OrcamentoAprovado: portal.MostrarOrcamento && rep.OrcamentoAprovado,
            TemPrecoFinal: portal.MostrarOrcamento && rep.PrecoFinalCents.HasValue,
            PrecoFinalCents: portal.MostrarOrcamento ? rep.PrecoFinalCents : null,
            Loja: loja,
            ClientePrimeiroNome: primeiroNome,
            Timeline: timeline,
            HealthScore: healthScore,
            DiagnosticoDestaques: destaques,
            GarantiaSlug: portal.MostrarGarantia ? garantia?.Slug : null,
            JaAvaliado: !portal.MostrarAvaliacao || jaAvaliado,
            Fotos: portal.MostrarFotos
                ? fotos.Select(f => new PublicFotoDto(f.Id, (int)f.Tipo, f.Legenda, f.CreatedAt)).ToList()
                : [],
            CamposEquipamento: campos
                .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => new PublicEquipmentFieldDto(c.Label, c.Value, c.Ordem))
                .ToList(),
            CoberturaGarantia: portal.MostrarGarantia ? cobertura : null,
            Conversa: conversa,
            // Sprint 487: ETA só faz sentido enquanto está em curso. Em Pronto/Entregue/Cancelado
            // a previsão deixa de ser útil (já chegou ao fim) e pode confundir.
            PrevistoEntregueEm: rep.Estado is RepairStatus.Entregue or RepairStatus.Cancelado or RepairStatus.Pronto
                ? null
                : rep.PrevistoEntregueEm);
    }
}

internal static class ReparacaoExtensions
{
    public static DateTime RecebidoEm(this Reparacao r) =>
        r.Timeline.OrderBy(t => t.MudouEm).FirstOrDefault()?.MudouEm ?? r.CreatedAt;
}
