using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

public sealed record AlertaStockRequest(string Slug, string Email, string? Website = null);

public interface IAlertaStockService
{
    /// <summary>Regista o pedido de aviso (produto publicado e esgotado). Repetir o mesmo email não duplica.</summary>
    Task RegistarAsync(AlertaStockRequest req, CancellationToken ct = default);
    /// <summary>Envia um email a quem pediu aviso dos produtos que voltaram a ter stock e apaga esses pedidos.</summary>
    Task<int> EnviarAsync(CatalogoDto catalogo, CancellationToken ct = default);
    /// <summary>Quantas pessoas estão à espera de cada produto (para o Mender mostrar).</summary>
    Task<IReadOnlyDictionary<string, int>> ContagensAsync(CancellationToken ct = default);
}

/// <summary>
/// Sprint 682: «Avisa-me quando houver». O pedido guarda só o email e o produto; ao voltar o stock vai um email e a
/// linha é apagada. Corre sempre que o site lê o catálogo — que acontece no instante em que a loja muda (Sprint 674).
/// </summary>
public sealed class AlertaStockService : IAlertaStockService
{
    private static readonly Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    /// <summary>Limite de pedidos pendentes por produto (trava abusos do formulário público).</summary>
    private const int MaxPorProduto = 500;

    private readonly IAlertaStockRepository _repo;
    private readonly ILojaService _loja;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly StripeOptions _opt;
    private readonly ILogger<AlertaStockService> _log;

    public AlertaStockService(IAlertaStockRepository repo, ILojaService loja, ITenantContext tenant, IEmailSender email,
        IOptions<StripeOptions> opt, ILogger<AlertaStockService> log)
    {
        _repo = repo;
        _loja = loja;
        _tenant = tenant;
        _email = email;
        _opt = opt.Value;
        _log = log;
    }

    public async Task RegistarAsync(AlertaStockRequest req, CancellationToken ct = default)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length > 160 || !EmailRx.IsMatch(email)) throw new ValidationException("email_invalido", "O email não parece válido.");
        var slug = (req.Slug ?? "").Trim();
        var tenantId = _tenant.TenantId ?? throw new ValidationException("sem_empresa", "Loja desconhecida.");
        var produto = (await _loja.CatalogoAsync(ct)).Produtos.FirstOrDefault(p => p.Slug == slug)
            ?? throw new NotFoundException("Produto", slug);
        if (produto.Disponibilidade != "Esgotado") return; // já há: nada a avisar
        if (await _repo.ExisteAsync(slug, email, ct)) return;
        if (await _repo.ContarPorSlugAsync(slug, ct) >= MaxPorProduto) return;
        await _repo.AddAsync(new AlertaStockLoja { TenantId = tenantId, Slug = slug, Email = email }, ct);
        await _repo.SaveAsync(ct);
    }

    public async Task<int> EnviarAsync(CatalogoDto catalogo, CancellationToken ct = default)
    {
        var emStock = catalogo.Produtos.Where(p => p.Disponibilidade != "Esgotado").ToDictionary(p => p.Slug);
        if (emStock.Count == 0 || !_email.IsConfigured) return 0;
        var pendentes = await _repo.DosSlugsAsync(emStock.Keys.ToList(), ct);
        var site = _opt.SiteBase.TrimEnd('/');
        var enviados = 0;
        foreach (var a in pendentes)
        {
            var p = emStock[a.Slug];
            var url = $"{site}/loja/{p.Categoria}/{p.Slug}";
            var titulo = WebUtility.HtmlEncode(p.Titulo);
            try
            {
                await _email.SendAsync(new EmailMessage(a.Email, $"Voltou a haver: {p.Titulo} — LopesTech",
                    $"<p>Olá! Pediste para te avisarmos quando voltasse a haver <strong>{titulo}</strong>.</p>" +
                    $"<p>Já há outra vez, por {p.Preco.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-PT"))} €. " +
                    $"<a href=\"{WebUtility.HtmlEncode(url)}\">Ver o produto</a></p>" +
                    "<p>As unidades são poucas — se quiseres, reserva pelo WhatsApp 920 182 332.</p>" +
                    "<p style=\"font-size:12px;color:#666\">Este foi o único email deste aviso: o teu endereço já foi apagado da nossa lista de avisos.</p>",
                    $"Olá! Já voltou a haver {p.Titulo}: {url}\n\nEste foi o único email deste aviso; o teu endereço já foi apagado."), ct);
                _repo.Remove(a);
                enviados++;
            }
            catch (Exception ex) { _log.LogWarning(ex, "Aviso de stock não enviado ({Slug})", a.Slug); }
        }
        if (enviados > 0) await _repo.SaveAsync(ct);
        return enviados;
    }

    public Task<IReadOnlyDictionary<string, int>> ContagensAsync(CancellationToken ct = default) => _repo.ContagensAsync(ct);
}
