using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Loja;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// 23/11: encomendas pagas no site por Multibanco (ou MB WAY) entram muitas vezes horas depois, com o cliente já fora
/// da página. De 10 em 10 minutos pergunta-se ao ifthenpay pelas que ainda aguardam pagamento — não depende do callback.
/// Config: <c>Loja:VerificarPagamentos:Enabled</c> (por omissão true).
/// </summary>
public sealed class PagamentosLojaPendentesHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<PagamentosLojaPendentesHostedService> _log;

    public PagamentosLojaPendentesHostedService(IServiceScopeFactory scopes, IConfiguration config, ILogger<PagamentosLojaPendentesHostedService> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Loja:VerificarPagamentos:Enabled", true) || _config.GetValue("E2E:Enabled", false)) return;
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Verificação dos pagamentos da loja falhou — tenta no próximo ciclo."); }
            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        List<Guid> tenants;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var limite = DateTime.UtcNow.AddDays(-(PagamentoNoSiteIfthenpay.DiasMultibanco + 2));
            tenants = await db.EncomendasLoja.IgnoreQueryFilters()
                .Where(e => !e.IsDeleted && e.Estado == EncomendaLojaEstado.AguardaPagamento && e.MetodoPagamento != null && e.CreatedAt > limite)
                .Select(e => e.TenantId).Distinct().ToListAsync(ct);
        }
        foreach (var tenantId in tenants)
        {
            using var scope = _scopes.CreateScope();
            // O contexto da empresa vem do utilizador do pedido — aqui cria-se um «pedido» só com a empresa.
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(HttpTenantContext.TenantIdClaim, tenantId.ToString()) })),
            };
            try
            {
                var n = await scope.ServiceProvider.GetRequiredService<IEncomendaLojaService>().VerificarPagamentosPendentesAsync(ct);
                if (n > 0) _log.LogInformation("{N} encomenda(s) da loja confirmadas como pagas (empresa {Tenant}).", n, tenantId);
            }
            finally { accessor.HttpContext = null; }
        }
    }
}
