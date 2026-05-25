using Microsoft.Data.SqlClient;

namespace RepairDesk.API.Backups;

public interface ISqlServerBackupExecutor
{
    Task CreateBackupAsync(BackupExecutionRequest request, CancellationToken ct = default);
    Task RestoreBackupAsync(BackupRestoreExecutionRequest request, CancellationToken ct = default);
}

public sealed class SqlServerBackupExecutor : ISqlServerBackupExecutor
{
    private readonly ILogger<SqlServerBackupExecutor> _logger;

    public SqlServerBackupExecutor(ILogger<SqlServerBackupExecutor> logger)
    {
        _logger = logger;
    }

    public async Task CreateBackupAsync(BackupExecutionRequest request, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(request.ConnectionString);
        await conn.OpenAsync(ct);

        // COMPRESSION não suportado em SQL Server Express. INIT+CHECKSUM são standard.
        var sql = $"""
            BACKUP DATABASE {QuoteDatabaseName(request.DatabaseName)}
            TO DISK = @path
            WITH INIT, CHECKSUM
            """;

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = 0,
        };
        cmd.Parameters.Add(new SqlParameter("@path", System.Data.SqlDbType.NVarChar, 4000)
        {
            Value = request.SqlBackupPath,
        });

        _logger.LogInformation(
            "BackupStartedSqlCommand Database={DatabaseName} SqlBackupPath={SqlBackupPath}",
            request.DatabaseName,
            request.SqlBackupPath);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RestoreBackupAsync(BackupRestoreExecutionRequest request, CancellationToken ct = default)
    {
        var masterConnectionString = BuildMasterConnectionString(request.ConnectionString);
        await using var conn = new SqlConnection(masterConnectionString);
        await conn.OpenAsync(ct);

        var databaseName = QuoteDatabaseName(request.DatabaseName);
        var sql = $"""
            ALTER DATABASE {databaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            RESTORE DATABASE {databaseName}
            FROM DISK = @path
            WITH REPLACE, RECOVERY, CHECKSUM;
            ALTER DATABASE {databaseName} SET MULTI_USER;
            """;

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = 0,
        };
        cmd.Parameters.Add(new SqlParameter("@path", System.Data.SqlDbType.NVarChar, 4000)
        {
            Value = request.SqlBackupPath,
        });

        _logger.LogWarning(
            "BackupRestoreStartedSqlCommand Database={DatabaseName} SqlBackupPath={SqlBackupPath}",
            request.DatabaseName,
            request.SqlBackupPath);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            await TrySetMultiUserAsync(conn, request.DatabaseName, ct);
        }
    }

    private static string QuoteDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required.", nameof(databaseName));

        return $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string BuildMasterConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
        };
        return builder.ConnectionString;
    }

    private static async Task TrySetMultiUserAsync(SqlConnection conn, string databaseName, CancellationToken ct)
    {
        try
        {
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(
                $"ALTER DATABASE {QuoteDatabaseName(databaseName)} SET MULTI_USER;",
                conn)
            {
                CommandTimeout = 30,
            };
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Best-effort recovery only; the original restore exception is more useful to callers.
        }
    }
}
