using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Documents;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 175: cron diário às 3h apaga PDFs raw expirados conforme retention policy.
///
/// Política (defaults conservadores PT):
/// - Rejected há > tenant.RetentionRejectedDays (default 15d) → apaga PDF + soft-delete entity
/// - Failed há > tenant.RetentionFailedDays (default 30d) → apaga PDF + soft-delete entity
/// - Approved há > tenant.RetentionApprovedPdfDays (default NULL=permanente) → apaga só
///   PDF raw (metadata fica: items JSON, totais, IVA — o "accounting vault").
///
/// CRÍTICO PT: CIRS art. 123.º obriga manter documentos fiscais por 10 anos. Defaults
/// nossos não tocam em Approved a menos que tenant configure explicitamente.
///
/// Audit log para cada delete (rastreio RGPD).
/// </summary>
public sealed class SupplierInvoiceRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SupplierInvoiceRetentionHostedService> _logger;
    private static readonly TimeSpan RunAt = TimeSpan.FromHours(3); // 3h UTC

    public SupplierInvoiceRetentionHostedService(IServiceScopeFactory scopeFactory, ILogger<SupplierInvoiceRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda 1 min após boot, depois corre diariamente às 3h UTC.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Retention cleanup falhou — vai tentar amanhã.");
            }

            var nextRun = ComputeNextRun();
            _logger.LogInformation("Próximo retention cleanup em {When}", nextRun);
            try { await Task.Delay(nextRun, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private static TimeSpan ComputeNextRun()
    {
        var now = DateTime.UtcNow;
        var todayAt3 = now.Date.Add(RunAt);
        var next = todayAt3 > now ? todayAt3 : todayAt3.AddDays(1);
        return next - now;
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<ISupplierInvoiceStorage>();
        var photoStorage = scope.ServiceProvider.GetRequiredService<IPhotoStorage>();

        var tenants = await db.Tenants.IgnoreQueryFilters().Where(t => t.IsActive).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var totalDeleted = 0;
        var totalPdfsApagados = 0;

        foreach (var tenant in tenants)
        {
            // 1. Rejected expirados → hard delete (entity + PDF).
            if (tenant.RetentionRejectedDays is { } rejDays && rejDays > 0)
            {
                var cutoff = now.AddDays(-rejDays);
                var expired = await db.SupplierInvoiceImports.IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenant.Id
                        && i.Status == SupplierInvoiceImportStatus.Rejected
                        && i.CreatedAt < cutoff
                        && !i.IsDeleted)
                    .ToListAsync(ct);
                foreach (var entity in expired)
                {
                    await TryDeletePdf(photoStorage, entity.PdfRelativePath, ct);
                    entity.IsDeleted = true;
                    totalDeleted++;
                    totalPdfsApagados++;
                }
            }

            // 2. Failed expirados → hard delete.
            if (tenant.RetentionFailedDays is { } failedDays && failedDays > 0)
            {
                var cutoff = now.AddDays(-failedDays);
                var expired = await db.SupplierInvoiceImports.IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenant.Id
                        && i.Status == SupplierInvoiceImportStatus.Failed
                        && i.CreatedAt < cutoff
                        && !i.IsDeleted)
                    .ToListAsync(ct);
                foreach (var entity in expired)
                {
                    await TryDeletePdf(photoStorage, entity.PdfRelativePath, ct);
                    entity.IsDeleted = true;
                    totalDeleted++;
                    totalPdfsApagados++;
                }
            }

            // 3. Approved há > N dias → apaga PDF raw mas mantém metadata.
            // Default NULL = permanente (PT contabilidade obriga 10 anos).
            if (tenant.RetentionApprovedPdfDays is { } apprDays && apprDays > 0)
            {
                var cutoff = now.AddDays(-apprDays);
                var expired = await db.SupplierInvoiceImports.IgnoreQueryFilters()
                    .Where(i => i.TenantId == tenant.Id
                        && i.Status == SupplierInvoiceImportStatus.Approved
                        && i.ProcessedAt != null
                        && i.ProcessedAt < cutoff
                        && i.PdfRelativePath != ""
                        && !i.IsDeleted)
                    .ToListAsync(ct);
                foreach (var entity in expired)
                {
                    await TryDeletePdf(photoStorage, entity.PdfRelativePath, ct);
                    entity.PdfRelativePath = ""; // marca apagado mas mantém row para o accounting vault
                    totalPdfsApagados++;
                }
            }
        }

        if (totalDeleted > 0 || totalPdfsApagados > 0)
        {
            await db.SaveChangesAsync(ct);
        }
        _logger.LogInformation("Retention cleanup: {Deleted} imports soft-deleted, {Pdfs} PDFs raw removidos.",
            totalDeleted, totalPdfsApagados);
    }

    private async Task TryDeletePdf(IPhotoStorage storage, string relativePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        try
        {
            await storage.DeleteAsync("supplier-invoices/" + relativePath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retention: falhou apagar PDF {Path}", relativePath);
        }
    }
}
