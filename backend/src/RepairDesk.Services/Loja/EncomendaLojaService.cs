using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Artigos;
using RepairDesk.Services.Push;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Services.Loja;

// ---- Pedidos e respostas -------------------------------------------------------------------------

public sealed record CheckoutLinhaRequest(string Slug, int Quantidade);

/// <summary><c>Website</c> é o campo-armadilha (escondido) — só os bots o preenchem.</summary>
public sealed record CheckoutRequest(
    IReadOnlyList<CheckoutLinhaRequest> Linhas, string Nome, string Email, string Telefone, string? Nif,
    string Morada, string CodigoPostal, string Localidade, string? Notas, bool AceitaTermos, string? Website = null);

public sealed record CheckoutResultado(string Referencia, string UrlPagamento);

public sealed record EncomendaPublicaLinhaDto(string Titulo, string Slug, int Quantidade, decimal PrecoUnitario);

/// <summary>O que o cliente vê na página de seguimento — sem morada completa nem dados de pagamento.</summary>
public sealed record EncomendaPublicaDto(
    string Referencia, EncomendaLojaEstado Estado, string EstadoTexto, DateTime CriadaEm, string Nome,
    IReadOnlyList<EncomendaPublicaLinhaDto> Linhas, decimal Subtotal, decimal Portes, decimal Total,
    string Transportadora, string? NumeroSeguimento, DateTime? PagaEm, DateTime? EnviadaEm, DateTime? EntregueEm);

public sealed record EncomendaLojaLinhaDto(Guid ProdutoLojaId, Guid? ArtigoId, string Slug, string Titulo, int Quantidade, decimal PrecoUnitario);

/// <summary>Vista do dono.</summary>
public sealed record EncomendaLojaDto(
    Guid Id, int Numero, string Referencia, EncomendaLojaEstado Estado, DateTime CriadaEm,
    string Nome, string Email, string Telefone, string? Nif, string Morada, string CodigoPostal, string Localidade, string? Notas,
    string Transportadora, decimal Subtotal, decimal Portes, decimal Total,
    DateTime? PagaEm, DateTime? EnviadaEm, DateTime? EntregueEm, DateTime? CanceladaEm, string? NumeroSeguimento,
    Guid? VendaId, string? Problema, IReadOnlyList<EncomendaLojaLinhaDto> Linhas);

public sealed record MudarEstadoEncomendaRequest(EncomendaLojaEstado Estado, string? NumeroSeguimento = null);

public interface IEncomendaLojaService
{
    /// <summary>Público: valida o carrinho com os preços do servidor, cria a encomenda e a página de pagamento do Stripe.</summary>
    Task<CheckoutResultado> CheckoutAsync(CheckoutRequest req, CancellationToken ct = default, Guid? clienteLojaId = null);
    /// <summary>Webhook: pagamento confirmado. Idempotente (o Stripe pode repetir o evento).</summary>
    Task ConfirmarPagamentoAsync(EncomendaLoja encomenda, string? paymentIntentId, CancellationToken ct = default);
    /// <summary>Webhook: a sessão expirou ou o pagamento assíncrono (Multibanco) falhou.</summary>
    Task ExpirarAsync(EncomendaLoja encomenda, CancellationToken ct = default);
    /// <summary>Público: estado para a página de seguimento (precisa do token do link).</summary>
    Task<EncomendaPublicaDto> EstadoPublicoAsync(string referencia, string token, CancellationToken ct = default);

    Task<IReadOnlyList<EncomendaLojaDto>> ListAsync(CancellationToken ct = default);
    Task<EncomendaLojaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<EncomendaLojaDto> MudarEstadoAsync(Guid id, MudarEstadoEncomendaRequest req, CancellationToken ct = default);
}

/// <summary>
/// Sprint 656: encomendas da loja do lopestech.pt. O browser só manda slugs e quantidades — preço, portes e
/// disponibilidade são sempre do servidor. Paga → venda «Pronta» no Mender (o stock sai) para o dono faturar e enviar.
/// </summary>
public sealed class EncomendaLojaService : IEncomendaLojaService
{
    // 13/11/2026: o Bruno passou o envio para os CTT, 5 € por encomenda.
    public const string Transportadora = "CTT";
    public const long PortesCents = 500;
    public const int MaxLinhas = 20;
    public const int MaxQuantidade = 10;
    private const string AlfabetoReferencia = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem 0/O, 1/I

    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex CodigoPostalRx = new(@"^\d{4}-\d{3}$", RegexOptions.Compiled);

    private readonly IEncomendaLojaRepository _repo;
    private readonly ILojaService _loja;
    private readonly IArtigoService _artigos;
    private readonly IVendaService _vendas;
    private readonly IStripeCheckout _stripe;
    private readonly StripeOptions _opt;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;
    private readonly IEmailSender _email;
    private readonly IStaffPushQueue _push;
    private readonly ILogger<EncomendaLojaService> _log;

    public EncomendaLojaService(IEncomendaLojaRepository repo, ILojaService loja, IArtigoService artigos, IVendaService vendas,
        IStripeCheckout stripe, IOptions<StripeOptions> opt, ITenantContext tenant, IAuditLogger audit, IEmailSender email,
        IStaffPushQueue push, ILogger<EncomendaLojaService> log)
    {
        _repo = repo;
        _loja = loja;
        _artigos = artigos;
        _vendas = vendas;
        _stripe = stripe;
        _opt = opt.Value;
        _tenant = tenant;
        _audit = audit;
        _email = email;
        _push = push;
        _log = log;
    }

    // ---- Checkout ------------------------------------------------------------------------------------

    public async Task<CheckoutResultado> CheckoutAsync(CheckoutRequest req, CancellationToken ct = default, Guid? clienteLojaId = null)
    {
        var tenantId = _tenant.TenantId ?? throw new ValidationException("sem_empresa", "Loja desconhecida.");
        if (!_stripe.Configurado)
            throw new ValidationException("pagamentos_inativos", "Os pagamentos online ainda não estão ativos. Fala connosco pelo WhatsApp 920 182 332.");

        var dados = ValidarCliente(req);
        var pedidas = (req.Linhas ?? Array.Empty<CheckoutLinhaRequest>())
            .Where(l => !string.IsNullOrWhiteSpace(l.Slug))
            .GroupBy(l => l.Slug.Trim())
            .Select(g => (Slug: g.Key, Quantidade: g.Sum(l => l.Quantidade)))
            .ToList();
        if (pedidas.Count == 0) throw new ValidationException("carrinho_vazio", "O carrinho está vazio.");
        if (pedidas.Count > MaxLinhas) throw new ValidationException("carrinho_grande", $"No máximo {MaxLinhas} produtos diferentes por encomenda.");

        var produtos = (await _loja.ProdutosParaVendaAsync(pedidas.Select(p => p.Slug).ToList(), ct)).ToDictionary(p => p.Slug);
        var linhas = new List<EncomendaLojaLinha>();
        foreach (var (slug, qtd) in pedidas)
        {
            if (!produtos.TryGetValue(slug, out var p))
                throw new ValidationException("produto_indisponivel", "Um dos produtos do carrinho já não está à venda. Atualiza a página.");
            if (qtd is < 1 or > MaxQuantidade)
                throw new ValidationException("quantidade_invalida", $"«{p.Titulo}»: quantidade entre 1 e {MaxQuantidade}.");
            if (p.ArtigoId is null || p.Disponibilidade != LojaDisponibilidade.EmStock)
                throw new ValidationException("produto_sem_stock", $"«{p.Titulo}» não está em stock para envio imediato.");
            if (p.Unidades is { } un && qtd > un)
                throw new ValidationException("stock_insuficiente", $"«{p.Titulo}»: só temos {un} em stock.");
            linhas.Add(new EncomendaLojaLinha
            {
                TenantId = tenantId, ProdutoLojaId = p.Id, ArtigoId = p.ArtigoId, Slug = p.Slug, Titulo = p.Titulo,
                Quantidade = qtd, PrecoUnitarioCents = p.PrecoCents,
            });
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var subtotal = linhas.Sum(l => l.PrecoUnitarioCents * l.Quantidade);
        var e = new EncomendaLoja
        {
            TenantId = tenantId,
            Referencia = await NovaReferenciaAsync(ct),
            TokenHash = Hash(token),
            Estado = EncomendaLojaEstado.AguardaPagamento,
            Nome = dados.Nome, Email = dados.Email, Telefone = dados.Telefone, Nif = dados.Nif,
            Morada = dados.Morada, CodigoPostal = dados.CodigoPostal, Localidade = dados.Localidade, Notas = dados.Notas,
            Transportadora = Transportadora, PortesCents = PortesCents, SubtotalCents = subtotal, TotalCents = subtotal + PortesCents,
            Linhas = linhas,
            ClienteLojaId = clienteLojaId,
        };
        foreach (var l in linhas) l.EncomendaId = e.Id;
        await _repo.AddAsync(e, ct);
        await _repo.SaveAsync(ct);

        var site = _opt.SiteBase.TrimEnd('/');
        var seguimento = $"{site}/loja/encomenda?ref={e.Referencia}&t={token}";
        var stripeLinhas = linhas.Select(l => new StripeLinha(l.Titulo, l.PrecoUnitarioCents, l.Quantidade)).ToList();
        stripeLinhas.Add(new StripeLinha($"Envio {Transportadora} (24 h)", PortesCents, 1));
        StripeSessao sessao;
        try
        {
            sessao = await _stripe.CriarSessaoAsync(new StripeSessaoPedido(e.Id, tenantId, e.Referencia, e.Email, stripeLinhas,
                seguimento + "&pago=1", $"{site}/loja/finalizar?cancelado=1"), ct);
        }
        catch (DomainException)
        {
            e.Estado = EncomendaLojaEstado.Cancelada;
            e.CanceladaEm = DateTime.UtcNow;
            e.Problema = "Não foi possível abrir a página de pagamento.";
            await _repo.SaveAsync(ct);
            throw;
        }
        e.StripeSessionId = sessao.Id;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(EncomendaLoja), e.Id,
            new { e.Referencia, total = e.TotalCents, linhas = linhas.Count }, tenantId, null, ct);
        return new CheckoutResultado(e.Referencia, sessao.Url);
    }

    private sealed record DadosCliente(string Nome, string Email, string Telefone, string? Nif, string Morada, string CodigoPostal, string Localidade, string? Notas);

    private static DadosCliente ValidarCliente(CheckoutRequest r)
    {
        if (!r.AceitaTermos) throw new ValidationException("termos", "Tens de aceitar os termos e a política de devolução.");
        var nome = (r.Nome ?? "").Trim();
        if (nome.Length is < 3 or > 120) throw new ValidationException("nome_invalido", "Escreve o teu nome completo.");
        var email = (r.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length > 160 || !EmailRx.IsMatch(email)) throw new ValidationException("email_invalido", "O email não parece válido.");
        var tel = new string((r.Telefone ?? "").Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (tel.Count(char.IsDigit) is < 9 or > 15) throw new ValidationException("telefone_invalido", "O telefone não parece válido.");
        string? nif = null;
        if (!string.IsNullOrWhiteSpace(r.Nif))
        {
            nif = NifValidator.Normalize(r.Nif);
            if (!NifValidator.IsValid(nif)) throw new ValidationException("nif_invalido", "O NIF não é válido (deixa vazio se não queres fatura com NIF).");
        }
        var morada = (r.Morada ?? "").Trim();
        if (morada.Length is < 5 or > 200) throw new ValidationException("morada_invalida", "Escreve a morada de entrega.");
        var cp = (r.CodigoPostal ?? "").Trim();
        if (!CodigoPostalRx.IsMatch(cp)) throw new ValidationException("cp_invalido", "Código postal no formato 0000-000 (só enviamos para Portugal).");
        var loc = (r.Localidade ?? "").Trim();
        if (loc.Length is < 2 or > 80) throw new ValidationException("localidade_invalida", "Escreve a localidade.");
        var notas = string.IsNullOrWhiteSpace(r.Notas) ? null : r.Notas.Trim();
        if (notas is { Length: > 500 }) notas = notas[..500];
        return new DadosCliente(nome, email, tel, nif, morada, cp, loc, notas);
    }

    private async Task<string> NovaReferenciaAsync(CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var sb = new StringBuilder("LT-");
            foreach (var b in RandomNumberGenerator.GetBytes(6)) sb.Append(AlfabetoReferencia[b % AlfabetoReferencia.Length]);
            var r = sb.ToString();
            if (!await _repo.ReferenciaExisteAsync(r, ct)) return r;
        }
        throw new ConflictException("referencia", "Não foi possível criar a encomenda. Tenta outra vez.");
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    // ---- Webhook -------------------------------------------------------------------------------------

    public async Task ConfirmarPagamentoAsync(EncomendaLoja e, string? paymentIntentId, CancellationToken ct = default)
    {
        if (e.Estado is not (EncomendaLojaEstado.AguardaPagamento or EncomendaLojaEstado.Expirada or EncomendaLojaEstado.Cancelada))
            return; // já tratada (o Stripe repete eventos)
        if (e.PagaEm is not null) return;

        e.PagaEm = DateTime.UtcNow;
        e.StripePaymentIntentId = paymentIntentId ?? e.StripePaymentIntentId;
        if (e.Estado != EncomendaLojaEstado.AguardaPagamento)
            e.Problema = "Pagamento recebido depois de a encomenda ter expirado/sido cancelada — confirma o stock ou reembolsa.";
        e.Estado = EncomendaLojaEstado.Paga;
        e.CanceladaEm = null;

        try
        {
            var linhas = (await _artigos.LinhasVendaAsync(e.Linhas
                .Where(l => l.ArtigoId is not null)
                .Select(l => new BalcaoLinhaRequest(l.ArtigoId!.Value, l.Quantidade, (int)l.PrecoUnitarioCents)).ToList(), ct)).ToList();
            linhas.Add(new VendaLinhaWriteRequest(null, null, $"Portes de envio ({Transportadora})", 1, (int)PortesCents));
            var notas = $"Encomenda online {e.Referencia} — {e.Nome}, {e.Telefone}, {e.Email}"
                        + (e.Nif is null ? "" : $", NIF {e.Nif}")
                        + $". Enviar para: {e.Morada}, {e.CodigoPostal} {e.Localidade}." + (e.Notas is null ? "" : $" Nota: {e.Notas}");
            var venda = await _vendas.CreateAsync(new VendaWriteRequest(VendaTipo.Produto, null, null, null,
                notas.Length > 1000 ? notas[..1000] : notas, linhas, VendaEstado.Pronta, PaymentMethod.Cartao), ct);
            e.VendaId = venda.Id;
        }
        catch (DomainException ex)
        {
            // Pago mas sem stock (vendido ao balcão entretanto, artigo apagado…) — fica registado para o dono resolver.
            e.Problema = $"Pago, mas a venda não foi criada: {ex.Message}";
            _log.LogWarning("Encomenda {Ref} paga sem venda: {Erro}", e.Referencia, ex.Message);
        }
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(EncomendaLoja), e.Id, new { e.Referencia, estado = "Paga", e.VendaId, e.Problema }, e.TenantId, null, ct);

        await Avisar(e, $"🛒 Encomenda online {e.Referencia} paga", $"{e.Nome} · {e.TotalCents / 100m:0.00} € · {e.Linhas.Sum(l => l.Quantidade)} artigo(s)"
            + (e.Problema is null ? "" : " · ⚠ ver problema"), ct);
        await EmailCliente(e, $"Recebemos a tua encomenda {e.Referencia}",
            $"Olá {PrimeiroNome(e.Nome)}, recebemos o pagamento da tua encomenda {e.Referencia} ({e.TotalCents / 100m:0.00} €). " +
            $"Vamos prepará-la e enviar pela {Transportadora} — entrega em 24 h úteis depois do envio. Avisamos quando sair.", ct);
    }

    public async Task ExpirarAsync(EncomendaLoja e, CancellationToken ct = default)
    {
        if (e.Estado != EncomendaLojaEstado.AguardaPagamento) return;
        e.Estado = EncomendaLojaEstado.Expirada;
        await _repo.SaveAsync(ct);
    }

    // ---- Seguimento público ----------------------------------------------------------------------------

    public async Task<EncomendaPublicaDto> EstadoPublicoAsync(string referencia, string token, CancellationToken ct = default)
    {
        var e = string.IsNullOrWhiteSpace(referencia) || string.IsNullOrWhiteSpace(token) ? null
            : await _repo.FindByReferenciaAsync(referencia.Trim().ToUpperInvariant(), ct);
        if (e is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(e.TokenHash), Encoding.ASCII.GetBytes(Hash(token.Trim()))))
            throw new NotFoundException("EncomendaLoja", referencia);
        return new EncomendaPublicaDto(e.Referencia, e.Estado, EstadoTexto(e.Estado), e.CreatedAt, PrimeiroNome(e.Nome),
            e.Linhas.Select(l => new EncomendaPublicaLinhaDto(l.Titulo, l.Slug, l.Quantidade, l.PrecoUnitarioCents / 100m)).ToList(),
            e.SubtotalCents / 100m, e.PortesCents / 100m, e.TotalCents / 100m, e.Transportadora, e.NumeroSeguimento,
            e.PagaEm, e.EnviadaEm, e.EntregueEm);
    }

    public static string EstadoTexto(EncomendaLojaEstado e) => e switch
    {
        EncomendaLojaEstado.AguardaPagamento => "A aguardar pagamento",
        EncomendaLojaEstado.Paga => "Paga — a preparar",
        EncomendaLojaEstado.EmPreparacao => "Em preparação",
        EncomendaLojaEstado.Enviada => "Enviada",
        EncomendaLojaEstado.Entregue => "Entregue",
        EncomendaLojaEstado.Cancelada => "Cancelada",
        _ => "Pagamento não concluído",
    };

    // ---- Dono ----------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<EncomendaLojaDto>> ListAsync(CancellationToken ct = default)
        => (await _repo.ListAsync(ct)).Select(ToDto).ToList();

    public async Task<EncomendaLojaDto> GetAsync(Guid id, CancellationToken ct = default) => ToDto(await Load(id, ct));

    public async Task<EncomendaLojaDto> MudarEstadoAsync(Guid id, MudarEstadoEncomendaRequest req, CancellationToken ct = default)
    {
        var e = await Load(id, ct);
        var de = e.Estado;
        var para = req.Estado;
        if (de == para && para != EncomendaLojaEstado.Enviada) return ToDto(e);

        var permitido = (de, para) switch
        {
            (EncomendaLojaEstado.Paga, EncomendaLojaEstado.EmPreparacao) => true,
            (EncomendaLojaEstado.Paga or EncomendaLojaEstado.EmPreparacao or EncomendaLojaEstado.Enviada, EncomendaLojaEstado.Enviada) => true,
            (EncomendaLojaEstado.Enviada, EncomendaLojaEstado.Entregue) => true,
            (EncomendaLojaEstado.AguardaPagamento or EncomendaLojaEstado.Paga or EncomendaLojaEstado.EmPreparacao or EncomendaLojaEstado.Expirada,
                EncomendaLojaEstado.Cancelada) => true,
            _ => false,
        };
        if (!permitido)
            throw new ConflictException("transicao_invalida", $"Não se passa de «{EstadoTexto(de)}» para «{EstadoTexto(para)}».");

        switch (para)
        {
            case EncomendaLojaEstado.Enviada:
                var nr = req.NumeroSeguimento?.Trim();
                if (nr is { Length: > 80 }) throw new ValidationException("seguimento_longo", "Nº de seguimento até 80 caracteres.");
                e.NumeroSeguimento = string.IsNullOrEmpty(nr) ? e.NumeroSeguimento : nr;
                e.EnviadaEm ??= DateTime.UtcNow;
                break;
            case EncomendaLojaEstado.Entregue:
                e.EntregueEm = DateTime.UtcNow;
                if (e.VendaId is { } v) await _vendas.MudarEstadoAsync(v, new MudarEstadoVendaRequest(VendaEstado.Entregue, PaymentMethod.Cartao), ct);
                break;
            case EncomendaLojaEstado.Cancelada:
                e.CanceladaEm = DateTime.UtcNow;
                // Devolve o stock. O reembolso do pagamento faz-se no painel do Stripe.
                if (e.VendaId is { } vc) await _vendas.MudarEstadoAsync(vc, new MudarEstadoVendaRequest(VendaEstado.Cancelada), ct);
                break;
        }
        e.Estado = para;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(EncomendaLoja), e.Id, new { e.Referencia, de, para, e.NumeroSeguimento }, e.TenantId, null, ct);

        if (para == EncomendaLojaEstado.Enviada && de != EncomendaLojaEstado.Enviada)
            await EmailCliente(e, $"A tua encomenda {e.Referencia} foi enviada",
                $"Olá {PrimeiroNome(e.Nome)}, a tua encomenda {e.Referencia} seguiu pela {Transportadora}"
                + (e.NumeroSeguimento is null ? "" : $" (nº de seguimento {e.NumeroSeguimento})") + ". Deve chegar no próximo dia útil.", ct);
        return ToDto(e);
    }

    // ---- Auxiliares ----------------------------------------------------------------------------------

    private async Task<EncomendaLoja> Load(Guid id, CancellationToken ct)
        => await _repo.FindAsync(id, ct) ?? throw new NotFoundException("EncomendaLoja", id);

    private static string PrimeiroNome(string nome) => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    private async Task Avisar(EncomendaLoja e, string titulo, string corpo, CancellationToken ct)
    {
        try { await _push.EnqueueAsync(new StaffPushJob(e.TenantId, titulo, corpo, "/loja/encomendas", $"encomenda-{e.Id:N}"), ct); }
        catch (Exception ex) { _log.LogWarning(ex, "Aviso da encomenda {Ref} não enviado", e.Referencia); }
    }

    /// <summary>Email ao cliente — nunca falha a operação (o seguimento continua disponível no link).</summary>
    private async Task EmailCliente(EncomendaLoja e, string assunto, string texto, CancellationToken ct)
    {
        if (!_email.IsConfigured) return;
        try
        {
            var html = $"<p>{System.Net.WebUtility.HtmlEncode(texto)}</p><p>Obrigado,<br>Bruno · LopesTech · 920 182 332</p>";
            await _email.SendAsync(new EmailMessage(e.Email, assunto, html, texto + "\n\nObrigado,\nBruno · LopesTech · 920 182 332"), ct);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Email da encomenda {Ref} não enviado", e.Referencia); }
    }

    private static EncomendaLojaDto ToDto(EncomendaLoja e) => new(
        e.Id, e.Numero, e.Referencia, e.Estado, e.CreatedAt, e.Nome, e.Email, e.Telefone, e.Nif, e.Morada, e.CodigoPostal, e.Localidade, e.Notas,
        e.Transportadora, e.SubtotalCents / 100m, e.PortesCents / 100m, e.TotalCents / 100m,
        e.PagaEm, e.EnviadaEm, e.EntregueEm, e.CanceladaEm, e.NumeroSeguimento, e.VendaId, e.Problema,
        e.Linhas.Select(l => new EncomendaLojaLinhaDto(l.ProdutoLojaId, l.ArtigoId, l.Slug, l.Titulo, l.Quantidade, l.PrecoUnitarioCents / 100m)).ToList());
}
