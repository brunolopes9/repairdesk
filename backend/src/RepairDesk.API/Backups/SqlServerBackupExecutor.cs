using Microsoft.Data.SqlClient;

namespace RepairDesk.API.Backups;

public interface ISqlServerBackupExecutor
{
    Task CreateBackupAsync(BackupExecutionRequest request, CancellationToken ct = default);
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

        var sql = $"""
            BACKUP DATABASE {QuoteDatabaseName(request.DatabaseName)}
            TO DISK = @path
            WITH INIT, COMPRESSION, CHECKSUM
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

    private static string QuoteDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required.", nameof(databaseName));

        return $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
    }
}
