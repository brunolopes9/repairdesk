using System.Net;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>Sprint 620 (Doc 98 F5): regras puras da caixa das faturas.</summary>
public static class FaturasEmail
{
    public const long MaxBytes = 15 * 1024 * 1024;
    public const string PastaProcessadas = "Mender-Processadas";
    public const string PastaSemPdf = "Mender-Sem-PDF";
    public const string PastaErros = "Mender-Erros";

    public static bool EPdf(string? contentType, string? nome)
        => string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
           || (nome?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false);

    public static string Destino(int pdfs, int falhas) => pdfs == 0 ? PastaSemPdf : falhas > 0 ? PastaErros : PastaProcessadas;

    /// <summary>Só nomes de servidor (ex.: mail.lopestech.pt, imap.gmail.com) — nada de IPs, localhost ou nomes internos.</summary>
    public static bool HostValido(string? host)
    {
        var h = host?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(h) || h.Length > 120 || IPAddress.TryParse(h, out _)) return false;
        if (h == "localhost" || h.EndsWith(".local") || h.EndsWith(".internal") || !h.Contains('.')) return false;
        return Uri.CheckHostName(h) == UriHostNameType.Dns;
    }
}

/// <summary>
/// Sprint 620 (Doc 98 F5): lê a caixa dedicada às faturas de cada empresa que a ligou (Definições → Faturas por email).
/// O Bruno reencaminha para lá, com regras no Gmail/Outlook, só os emails dos fornecedores e das despesas fixas —
/// o Mender nunca tem acesso às caixas pessoais. Cada PDF entra na importação inteligente («por aprovar»; duplicados
/// detetados pelo SHA-256). Depois o email é movido para «Mender-Processadas», «Mender-Sem-PDF» ou «Mender-Erros» —
/// nada se apaga (e os códigos de confirmação do reencaminhamento ficam legíveis no webmail).
/// </summary>
public sealed class FaturasImapLeitor
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<FaturasImapLeitor> _logger;

    public FaturasImapLeitor(IServiceScopeFactory scopes, ILogger<FaturasImapLeitor> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>Só liga e autentica (para validar a configuração antes de a guardar).</summary>
    public static async Task TestarAsync(string host, int port, string user, string password, CancellationToken ct)
    {
        using var imap = new ImapClient { Timeout = 20_000 };
        await imap.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.SslOnConnect, ct);
        await imap.AuthenticateAsync(user, password, ct);
        await imap.DisconnectAsync(true, ct);
    }

    /// <summary>Lê todas as empresas com caixa configurada. Devolve o total de PDFs novos.</summary>
    public async Task<int> LerTodasAsync(CancellationToken ct)
    {
        List<Guid> ids;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ids = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .Where(t => !t.IsDeleted && t.FaturasImapHost != null && t.FaturasImapPasswordCifrada != null)
                .Select(t => t.Id).ToListAsync(ct);
        }
        var total = 0;
        foreach (var id in ids) total += await LerAsync(id, ct);
        return total;
    }

    public async Task<int> LerAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId, ct);
        if (t?.FaturasImapHost is null || t.FaturasImapUser is null || t.FaturasImapPasswordCifrada is null) return 0;
        try
        {
            var password = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect(t.FaturasImapPasswordCifrada);
            var n = await ProcessarAsync(t, password, ct);
            t.FaturasImapUltimaLeitura = DateTime.UtcNow;
            t.FaturasImapUltimoErro = null;
            await db.SaveChangesAsync(ct);
            return n;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Faturas por email: a caixa da empresa {Tenant} falhou.", tenantId);
            var erro = ex is MailKit.Security.AuthenticationException ? "A palavra-passe da caixa foi recusada." : ex.Message;
            t.FaturasImapUltimoErro = erro.Length > 300 ? erro[..300] : erro;
            await db.SaveChangesAsync(ct);
            return 0;
        }
    }

    private async Task<int> ProcessarAsync(Tenant t, string password, CancellationToken ct)
    {
        using var imap = new ImapClient { Timeout = 60_000 };
        await imap.ConnectAsync(t.FaturasImapHost, t.FaturasImapPort ?? 993, MailKit.Security.SecureSocketOptions.SslOnConnect, ct);
        await imap.AuthenticateAsync(t.FaturasImapUser, password, ct);
        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);
        var uids = await inbox.SearchAsync(SearchQuery.NotSeen, ct);

        var pastas = new Dictionary<string, IMailFolder>();
        async Task<IMailFolder> Pasta(string nome)
        {
            if (pastas.TryGetValue(nome, out var f)) return f;
            var raiz = imap.GetFolder(imap.PersonalNamespaces[0]);
            var sub = (await raiz.GetSubfoldersAsync(false, ct)).FirstOrDefault(x => x.Name == nome)
                      ?? await raiz.CreateAsync(nome, true, ct);
            return pastas[nome] = sub;
        }

        var importados = 0;
        foreach (var uid in uids.Take(50))
        {
            var msg = await inbox.GetMessageAsync(uid, ct);
            int pdfs = 0, falhas = 0;
            foreach (var parte in msg.Attachments.OfType<MimePart>())
            {
                if (!FaturasEmail.EPdf(parte.ContentType.MimeType, parte.FileName)) continue;
                pdfs++;
                try
                {
                    using var ms = new MemoryStream();
                    await parte.Content.DecodeToAsync(ms, ct);
                    if (ms.Length is 0 or > FaturasEmail.MaxBytes) { falhas++; continue; }
                    using var scope = _scopes.CreateScope();
                    var ingest = scope.ServiceProvider.GetRequiredService<ISupplierInvoiceImportService>();
                    var r = await ingest.IngestAsExternalAsync(t.Id, ms.ToArray(), parte.FileName ?? "fatura.pdf",
                        new SupplierInvoiceEmailMeta(msg.MessageId, msg.Subject, msg.From?.ToString(), msg.Date.UtcDateTime), null, ct);
                    if (!r.WasDuplicate) importados++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    falhas++;
                    _logger.LogWarning(ex, "Faturas por email: anexo {Nome} de «{Assunto}» falhou.", parte.FileName, msg.Subject);
                }
            }
            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
            await inbox.MoveToAsync(uid, await Pasta(FaturasEmail.Destino(pdfs, falhas)), ct);
        }
        await imap.DisconnectAsync(true, ct);

        if (importados > 0)
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IStaffPushQueue>().EnqueueAsync(new StaffPushJob(
                t.Id,
                importados == 1 ? "📥 1 fatura nova por aprovar" : $"📥 {importados} faturas novas por aprovar",
                "Chegaram por email. Confirma fornecedor, valores e IVA.",
                "/importacoes",
                "faturas-email"), ct);
        }
        return importados;
    }
}

/// <summary>Sprint 620: de 5 em 5 minutos lê as caixas das faturas. Config: Faturas:Imap:Enabled (default true).</summary>
public sealed class FaturasImapHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);
    private readonly FaturasImapLeitor _leitor;
    private readonly IConfiguration _config;
    private readonly ILogger<FaturasImapHostedService> _logger;

    public FaturasImapHostedService(FaturasImapLeitor leitor, IConfiguration config, ILogger<FaturasImapHostedService> logger)
    {
        _leitor = leitor;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Faturas:Imap:Enabled", true)) return;
        try { await Task.Delay(TimeSpan.FromSeconds(75), stoppingToken); }
        catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var n = await _leitor.LerTodasAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("Faturas por email: {N} PDF(s) novos importados.", n);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Faturas por email falharam — tenta daqui a 5 min."); }
            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
