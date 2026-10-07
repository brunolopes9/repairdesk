using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Push;
using RepairDesk.Services.TenantPreferences;
using RepairDesk.Services.Vendas;

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

/// <summary>
/// Portal público do cliente (/r/{slug}) — Doc 94 Fase 4c: assenta na Venda do tipo Reparação.
/// Mostra estado, linha temporal, fotos públicas, orçamento (aceitar/recusar), garantia, conversa
/// com a loja, pagamento MB Way e avaliação. NUNCA expõe custos, lucro, IVA a pagar ou notas internas.
/// </summary>
public class PublicPortalService : IPublicPortalService
{
    private readonly IVendaRepository _vendas;
    private readonly IVendaService _vendaService;
    private readonly ITenantRepository _tenants;
    private readonly IGarantiaRepository _garantias;
    private readonly IAvaliacaoRepository _avaliacoes;
    private readonly IVendaFotoRepository _fotos;
    private readonly ITenantPreferencesService _preferences;
    private readonly IVendaComunicacaoRepository _comunicacoes;
    private readonly IStaffPushQueue _push;
    private readonly Payments.IPaymentService _payments;
    private readonly Payments.Ifthenpay.IfthenpayOptions _ifthenpay;

    public PublicPortalService(
        IVendaRepository vendas,
        IVendaService vendaService,
        ITenantRepository tenants,
        IGarantiaRepository garantias,
        IAvaliacaoRepository avaliacoes,
        IVendaFotoRepository fotos,
        ITenantPreferencesService preferences,
        IVendaComunicacaoRepository comunicacoes,
        IStaffPushQueue push,
        Payments.IPaymentService payments,
        Payments.Ifthenpay.IfthenpayOptions ifthenpay)
    {
        _vendas = vendas;
        _vendaService = vendaService;
        _tenants = tenants;
        _garantias = garantias;
        _avaliacoes = avaliacoes;
        _fotos = fotos;
        _preferences = preferences;
        _comunicacoes = comunicacoes;
        _push = push;
        _payments = payments;
        _ifthenpay = ifthenpay;
    }

    public async Task<PublicGarantiaDto> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Garantia", slug);

        var g = await _garantias.FindBySlugAsync(slug, ct) ?? throw new NotFoundException("Garantia", slug);
        var tenant = await _tenants.FindByIdAsync(g.TenantId, ct);
        var agora = DateTime.UtcNow;
        var diasRestantes = (int)Math.Max(0, (g.DataFim - agora).TotalDays);
        var activa = !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;

        var v = g.Venda;
        var reparacao = g.SourceType == GarantiaSourceType.Reparacao;
        var equipamentoPublico = reparacao
            ? v?.Equipamento ?? "Equipamento"
            : v?.Items.FirstOrDefault()?.Descricao ?? "Artigos vendidos";
        var items = !reparacao && v is not null
            ? v.Items.Select(i => new PublicGarantiaItemDto(
                    i.Descricao, i.Quantidade, i.PrecoUnitarioCents, i.TotalCents,
                    string.IsNullOrEmpty(i.Imei) ? null : ImeiValidator.Mask(i.Imei)))
                .ToList()
            : null;

        return new PublicGarantiaDto(
            Slug: g.Slug,
            EquipamentoPublico: equipamentoPublico,
            Loja: tenant?.LegalName ?? tenant?.Name ?? "Loja",
            LogoUrl: tenant?.LogoUrl,
            DataInicio: g.DataInicio,
            DataFim: g.DataFim,
            DiasGarantia: g.DiasGarantia,
            Activa: activa,
            Anulada: g.Anulada,
            DiasRestantes: diasRestantes,
            Cobertura: g.Cobertura,
            Exclusoes: g.Exclusoes,
            Origem: reparacao ? "Reparacao" : "Venda",
            DocumentoReferencia: v is null ? null : $"{(reparacao ? "Reparação" : "Venda")} #{v.Numero:D5}",
            NumeroFatura: v?.InvoiceNumber,
            Items: items,
            LojaEmail: tenant?.Email,
            LojaTelefone: tenant?.Phone);
    }

    public async Task<AvaliacaoSubmittedDto> SubmeterAvaliacaoAsync(string repairSlug, int score, string? comentario, bool publicarTestemunho, CancellationToken ct = default)
    {
        if (score is < 1 or > 5)
            throw new ValidationException("score_invalido", "Score deve ser entre 1 e 5.");
        var v = await FindAsync(repairSlug, ct);
        if (v.Estado != VendaEstado.Entregue)
            throw new ConflictException("nao_entregue", "Esta reparação ainda não foi entregue.");
        if (await _avaliacoes.FindByVendaAsync(v.Id, ct) is not null)
            throw new ConflictException("ja_avaliado", "Esta reparação já foi avaliada.");

        var tenant = await _tenants.FindByIdAsync(v.TenantId, ct);
        var prefs = await _preferences.GetForTenantAsync(v.TenantId, ct);
        var googleReviewUrl = prefs.Portal.GoogleReviewUrl ?? tenant?.GoogleReviewUrl;
        var dirigirGoogle = score >= prefs.Portal.GoogleReviewMinScore && !string.IsNullOrWhiteSpace(googleReviewUrl);

        var avaliacao = new Avaliacao
        {
            TenantId = v.TenantId,
            VendaId = v.Id,
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
        => await BuildDtoAsync(await FindAsync(slug, ct), ct);

    public async Task<PublicRepairDto> AprovarOrcamentoAsync(string slug, bool aceitar, CancellationToken ct = default)
    {
        var v = await FindAsync(slug, ct);
        var prefs = await _preferences.GetForTenantAsync(v.TenantId, ct);
        if (!prefs.Portal.PermitirAprovarOrcamento)
            throw new ForbiddenException("orcamento_aprovacao_desactivada", "A aprovação online de orçamentos está desativada nesta loja.");
        if (v.Estado != VendaEstado.Orcamento)
            throw new ConflictException("ja_aprovado", "Este orçamento já foi respondido.");
        if (v.TotalCents <= 0)
            throw new ConflictException("sem_orcamento", "Esta reparação ainda não tem orçamento para aprovar.");

        // Mesma regra de stock que no balcão: aceitar consome as peças; recusar cancela sem mexer.
        await _vendaService.MudarEstadoAsync(v.Id, new MudarEstadoVendaRequest(aceitar ? VendaEstado.EmCurso : VendaEstado.Cancelada), ct);

        var nome = v.Cliente?.Nome?.Split(' ').FirstOrDefault() ?? "Cliente";
        await _push.EnqueueAsync(new StaffPushJob(
            v.TenantId,
            aceitar ? $"✅ {nome} aceitou o orçamento" : $"❌ {nome} recusou o orçamento",
            $"Reparação #{v.Numero:D5} · {v.TotalCents / 100m:F2}€",
            $"/vendas/{v.Id}",
            $"orcamento-{v.Id}"), ct);

        return await BuildDtoAsync(await FindAsync(slug, ct), ct);
    }

    public async Task SubmeterMensagemAsync(string slug, string texto, CancellationToken ct = default)
    {
        var trimmed = (texto ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 2000)
            throw new ValidationException("texto_invalido", "Mensagem obrigatória (1 a 2000 caracteres).");

        var v = await FindAsync(slug, ct);
        if (v.Estado is VendaEstado.Entregue or VendaEstado.Cancelada)
            throw new ConflictException("estado_fechado", "Esta reparação já foi fechada. Contacte-nos pelo telefone ou WhatsApp da loja.");
        if (v.ClienteId is not { } clienteId)
            throw new ConflictException("sem_cliente", "Esta reparação não tem cliente associado.");

        // CreatedByUserId = Guid.Empty é a sentinela "portal cliente" — a UI distingue pelo tipo PortalCliente.
        await _comunicacoes.AddAsync(new VendaComunicacao
        {
            TenantId = v.TenantId,
            VendaId = v.Id,
            ClienteId = clienteId,
            Tipo = ComunicacaoTipo.PortalCliente,
            Direcao = ComunicacaoDirecao.Inbound,
            Texto = trimmed,
            CreatedByUserId = Guid.Empty,
        }, ct);
        await _comunicacoes.SaveAsync(ct);

        var nome = v.Cliente?.Nome?.Split(' ').FirstOrDefault() ?? "Cliente";
        var preview = trimmed.Length > 80 ? trimmed[..80] + "…" : trimmed;
        await _push.EnqueueAsync(new StaffPushJob(v.TenantId, $"💬 {nome} respondeu no portal", preview, $"/vendas/{v.Id}", $"portal-msg-{v.Id}"), ct);
    }

    public async Task<PublicPagamentoDto> IniciarPagamentoMbWayAsync(string slug, string telefone, CancellationToken ct = default)
    {
        // Money-sensitive — validações estritas. Telefone PT: 9 dígitos começados por 9.
        var digits = new string((telefone ?? "").Where(char.IsDigit).ToArray());
        if (digits.StartsWith("351") && digits.Length == 12) digits = digits[3..];
        if (digits.Length != 9 || digits[0] != '9')
            throw new ValidationException("telefone_invalido", "Indica um número de telemóvel português válido (9 dígitos).");
        if (!_ifthenpay.IsConfigured || string.IsNullOrWhiteSpace(_ifthenpay.MBWayKey))
            throw new ConflictException("mbway_indisponivel", "Esta loja ainda não tem pagamento MBWay ativo. Paga na loja.");

        var v = await FindAsync(slug, ct);
        if (v.Estado == VendaEstado.Cancelada)
            throw new ConflictException("cancelada", "Esta reparação foi cancelada.");
        if (v.Estado == VendaEstado.Orcamento)
            throw new ConflictException("orcamento_pendente", "Aceita primeiro o orçamento.");

        var pagamentos = await _payments.GetByVendaAsync(v.Id, ct);
        if (v.Estado == VendaEstado.Entregue || pagamentos.Any(p => p.Status == PaymentStatus.Pago))
            throw new ConflictException("ja_pago", "Esta reparação já está paga.");
        var amount = v.TotalCents;
        if (amount <= 0)
            throw new ConflictException("sem_valor", "Ainda não há valor definido para pagar.");

        // Anti-spam de push MBWay: não reenviar se já há um pedido pendente recente.
        var agora = DateTime.UtcNow;
        var pendente = pagamentos.FirstOrDefault(p =>
            p.Status == PaymentStatus.NaoPago && p.Method == PaymentMethod.MBWay
            && p.ProviderRef != null && (p.ExpiresAt == null || p.ExpiresAt > agora));
        if (pendente is not null)
            return new PublicPagamentoDto("pendente", amount,
                "Já enviámos um pedido MBWay. Confirma na app (até 4 min) ou tenta novamente depois.", pendente.ExpiresAt);

        var tenant = await _tenants.FindByIdAsync(v.TenantId, ct);
        var payment = await _payments.InitiateAsync(
            new PaymentInitiationRequest(
                TenantId: v.TenantId,
                VendaId: v.Id,
                Method: PaymentMethod.MBWay,
                AmountCents: amount,
                CustomerPhone: digits,
                CustomerEmail: v.Cliente?.Email,
                Description: $"Reparação #{v.Numero:D5}" + (tenant is not null ? $" · {tenant.LegalName ?? tenant.Name}" : "")),
            PaymentProvider.Ifthenpay, ct);
        if (string.IsNullOrWhiteSpace(payment.ProviderRef))
            throw new ConflictException("mbway_falhou", "Não foi possível iniciar o MBWay. Tenta novamente ou paga na loja.");

        await _push.EnqueueAsync(new StaffPushJob(
            v.TenantId, "💳 Pagamento MBWay iniciado",
            $"Reparação #{v.Numero:D5} · {amount / 100m:F2}€ — a aguardar confirmação",
            $"/vendas/{v.Id}", $"mbway-{v.Id}"), ct);

        return new PublicPagamentoDto("pendente", amount, "Abre a app MBWay e confirma o pagamento (até 4 minutos).", payment.ExpiresAt);
    }

    // ---------- helpers ----------

    private async Task<Venda> FindAsync(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 32)
            throw new NotFoundException("Reparacao", slug ?? "");
        var v = await _vendas.FindByPublicSlugAsync(slug.Trim(), ct) ?? throw new NotFoundException("Reparacao", slug);
        // Compliance: não revelar reparações com mais de 2 anos.
        if (v.Tipo != VendaTipo.Reparacao || v.CreatedAt < DateTime.UtcNow.AddYears(-2))
            throw new NotFoundException("Reparacao", slug);
        return v;
    }

    private async Task<PublicRepairDto> BuildDtoAsync(Venda v, CancellationToken ct)
    {
        var prefs = await _preferences.GetForTenantAsync(v.TenantId, ct);
        var portal = prefs.Portal;
        var tenant = await _tenants.FindByIdAsync(v.TenantId, ct);
        var garantia = await _garantias.FindByVendaAsync(v.Id, ct);
        var jaAvaliado = await _avaliacoes.FindByVendaAsync(v.Id, ct) is not null;
        var fotos = await _fotos.ListPublicByVendaIdAsync(v.Id, ct);
        var comunicacoes = await _comunicacoes.ListByVendaAsync(v.Id, ct);
        var pago = v.Estado == VendaEstado.Entregue
            || (await _payments.GetByVendaAsync(v.Id, ct)).Any(p => p.Status == PaymentStatus.Pago);

        var timeline = portal.MostrarTimeline
            ? v.Timeline.OrderBy(t => t.MudouEm).Select(t => new PublicTimelineEntry(PublicEstadoMapper.From(t.EstadoTo), t.MudouEm)).ToList()
            : [];
        var orcamentoVisivel = portal.MostrarOrcamento && v.TotalCents > 0;
        var precoFinal = orcamentoVisivel && v.Estado is VendaEstado.Pronta or VendaEstado.Entregue;

        return new PublicRepairDto(
            Slug: v.PublicSlug!,
            EquipamentoPublico: v.Equipamento ?? "Equipamento",
            AvariaPublica: v.Problema ?? "",
            Estado: PublicEstadoMapper.From(v.Estado),
            EstadoSince: v.Timeline.OrderBy(t => t.MudouEm).LastOrDefault()?.MudouEm ?? v.CreatedAt,
            RecebidoEm: v.CreatedAt,
            EntregueEm: v.Estado == VendaEstado.Entregue ? v.Data : null,
            OrcamentoCents: orcamentoVisivel ? v.TotalCents : null,
            OrcamentoAprovado: orcamentoVisivel && v.Estado is not (VendaEstado.Orcamento or VendaEstado.Cancelada),
            TemPrecoFinal: precoFinal,
            PrecoFinalCents: precoFinal ? v.TotalCents : null,
            Loja: new PublicLoja(tenant?.LegalName ?? tenant?.Name ?? "Loja", tenant?.Phone, tenant?.Email, tenant?.Website, tenant?.LogoUrl),
            ClientePrimeiroNome: v.Cliente?.Nome?.Split(' ').FirstOrDefault() ?? "Cliente",
            Timeline: timeline,
            GarantiaSlug: portal.MostrarGarantia ? garantia?.Slug : null,
            JaAvaliado: !portal.MostrarAvaliacao || jaAvaliado,
            Fotos: portal.MostrarFotos
                ? fotos.Select(f => new PublicFotoDto(f.Id, (int)f.Tipo, f.Legenda, f.CreatedAt)).ToList()
                : [],
            Conversa: comunicacoes
                .Where(c => c.Tipo == ComunicacaoTipo.PortalCliente)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new PublicConversaMsg(c.Direcao == ComunicacaoDirecao.Outbound, c.Texto, c.CreatedAt))
                .ToList(),
            // ETA só enquanto está em curso.
            PrevistoEntregueEm: v.Estado is VendaEstado.EmCurso or VendaEstado.AEsperaPeca ? v.PrevistoPara : null,
            Pago: portal.MostrarOrcamento && pago);
    }
}
