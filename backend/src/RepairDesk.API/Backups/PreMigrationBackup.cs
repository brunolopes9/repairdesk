using Microsoft.EntityFrameworkCore;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Backups;

/// <summary>
/// Faz um backup da BD imediatamente antes de aplicar migrações pendentes no arranque.
/// Rede de segurança para migrações destrutivas (DropTable/DropColumn): se o backup falhar, o
/// arranque aborta e a migração NÃO corre — preferível a perder dados sem cópia.
/// Só actua com <c>Backup:Enabled=true</c> e numa BD que já tem migrações aplicadas
/// (uma BD nova não tem nada a proteger).
/// </summary>
public static class PreMigrationBackup
{
    public static async Task RunIfPendingAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        if (!config.GetValue("Backup:Enabled", false)) return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (!await db.Database.CanConnectAsync(ct)) return;
        var applied = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!applied.Any()) return;

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0) return;

        logger.LogWarning("PreMigrationBackup: {Count} migração(ões) pendente(s) ({Migrations}) — backup antes de aplicar.",
            pending.Count, string.Join(", ", pending));

        var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var result = await backup.RunBackupAsync(BackupTrigger.PreMigration, ct);
        logger.LogInformation("PreMigrationBackup concluído: {Result}", result);
    }
}
