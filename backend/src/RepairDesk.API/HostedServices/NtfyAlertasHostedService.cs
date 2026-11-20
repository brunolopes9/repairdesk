using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// 20/11 (Bruno): alertas externos → push do Mender. Ouve tópicos ntfy (stream JSON, ex.: o vigia de stock da
/// PS5 na Worten) e cada mensagem vira uma notificação insistente para os dispositivos da empresa.
/// Não passa pelo «silenciar avisos» da rotina — é para tocar sempre.
/// Config: Alertas:Ntfy:Topicos (separados por vírgula), Alertas:Ntfy:Empresa (slug público, ex.: lopestech-77edde),
/// Alertas:Ntfy:Base (por defeito https://ntfy.sh). Sem tópicos → não faz nada.
/// </summary>
public sealed class NtfyAlertasHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<NtfyAlertasHostedService> _logger;

    public NtfyAlertasHostedService(IServiceScopeFactory scopes, IConfiguration config, IHttpClientFactory http,
        ILogger<NtfyAlertasHostedService> logger)
    {
        _scopes = scopes;
        _config = config;
        _http = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topicos = (_config["Alertas:Ntfy:Topicos"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')).ToArray();
        var slug = _config["Alertas:Ntfy:Empresa"];
        if (topicos.Length == 0 || string.IsNullOrWhiteSpace(slug)) return;

        Guid tenantId;
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = await db.Tenants.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.IntakeSlug == slug, stoppingToken);
            if (t is null) { _logger.LogWarning("Alertas ntfy: empresa {Slug} não existe — desligado.", slug); return; }
            tenantId = t.Id;
        }
        var baseUrl = (_config["Alertas:Ntfy:Base"] ?? "https://ntfy.sh").TrimEnd('/');
        _logger.LogInformation("Alertas ntfy: a ouvir {N} tópico(s) para a empresa {Slug}.", topicos.Length, slug);
        // O mesmo alerta pode chegar por dois tópicos (o vigia publica nos dois): só um push por mensagem igual.
        var recentes = new Dictionary<string, DateTime>();
        await Task.WhenAll(topicos.Select(t => OuvirAsync(baseUrl, t, tenantId, recentes, stoppingToken)));
    }

    private async Task OuvirAsync(string baseUrl, string topico, Guid tenantId, Dictionary<string, DateTime> recentes, CancellationToken ct)
    {
        var espera = TimeSpan.FromSeconds(5);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var http = _http.CreateClient("ntfy");
                http.Timeout = Timeout.InfiniteTimeSpan;
                using var resp = await http.GetAsync($"{baseUrl}/{topico}/json", HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);
                espera = TimeSpan.FromSeconds(5);
                while (!ct.IsCancellationRequested && await reader.ReadLineAsync(ct) is { } linha)
                {
                    if (string.IsNullOrWhiteSpace(linha)) continue;
                    var alerta = Ler(linha);
                    if (alerta is null) continue;
                    var chave = $"{alerta.Value.Titulo}|{alerta.Value.Texto}";
                    lock (recentes)
                    {
                        foreach (var velho in recentes.Where(kv => kv.Value < DateTime.UtcNow.AddMinutes(-2)).Select(kv => kv.Key).ToList())
                            recentes.Remove(velho);
                        if (!recentes.TryAdd(chave, DateTime.UtcNow)) continue;
                    }
                    using var scope = _scopes.CreateScope();
                    var fila = scope.ServiceProvider.GetRequiredService<IStaffPushQueue>();
                    await fila.EnqueueAsync(new StaffPushJob(tenantId, alerta.Value.Titulo, alerta.Value.Texto,
                        alerta.Value.Url ?? "/", $"alerta-{topico}") { Insistente = true }, ct);
                    _logger.LogInformation("Alerta ntfy ({Topico}) enviado como push: {Titulo}", topico, alerta.Value.Titulo);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alertas ntfy: ligação ao tópico {Topico} caiu — volta a ligar em {S}s.", topico, espera.TotalSeconds);
            }
            try { await Task.Delay(espera, ct); }
            catch (OperationCanceledException) { return; }
            espera = TimeSpan.FromSeconds(Math.Min(espera.TotalSeconds * 2, 300));
        }
    }

    /// <summary>Uma linha do stream JSON do ntfy → alerta (só eventos «message»).</summary>
    public static (string Titulo, string Texto, string? Url)? Ler(string linha)
    {
        try
        {
            using var doc = JsonDocument.Parse(linha);
            var r = doc.RootElement;
            if (!r.TryGetProperty("event", out var ev) || ev.GetString() != "message") return null;
            var texto = r.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            var titulo = r.TryGetProperty("title", out var t) && !string.IsNullOrWhiteSpace(t.GetString()) ? t.GetString()! : "Alerta";
            string? url = r.TryGetProperty("click", out var c) && c.GetString() is { } u && u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? u : null;
            static string Corta(string s, int n) => s.Length > n ? s[..n] : s;
            return (Corta(titulo, 120), Corta(texto, 400), url);
        }
        catch (JsonException) { return null; }
    }
}
