using Microsoft.Extensions.Configuration;

namespace RepairDesk.Infrastructure.Storage;

public sealed class R2StorageOptions
{
    public const string SectionName = "Storage:R2";

    public string AccountId { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;

    public string Endpoint => string.IsNullOrWhiteSpace(AccountId)
        ? string.Empty
        : $"https://{AccountId}.r2.cloudflarestorage.com";

    public static R2StorageOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new R2StorageOptions
        {
            AccountId = (section["AccountId"] ?? string.Empty).Trim(),
            AccessKey = (section["AccessKey"] ?? string.Empty).Trim(),
            Secret = (section["Secret"] ?? string.Empty).Trim(),
            Bucket = (section["Bucket"] ?? string.Empty).Trim(),
        };
    }

    public void Validate()
    {
        var errors = ValidateValues();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Storage:R2 config invalid: {string.Join("; ", errors)}");
    }

    public IReadOnlyList<string> ValidateValues()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(AccountId))
            errors.Add("Storage:R2:AccountId is required.");
        else if (AccountId.Contains("://", StringComparison.Ordinal) ||
                 AccountId.Contains('/', StringComparison.Ordinal) ||
                 AccountId.Contains('\\', StringComparison.Ordinal) ||
                 AccountId.Contains(' ', StringComparison.Ordinal))
            errors.Add("Storage:R2:AccountId must be only the Cloudflare account id, not a URL.");

        if (string.IsNullOrWhiteSpace(AccessKey))
            errors.Add("Storage:R2:AccessKey is required.");

        if (string.IsNullOrWhiteSpace(Secret))
            errors.Add("Storage:R2:Secret is required.");

        if (string.IsNullOrWhiteSpace(Bucket))
            errors.Add("Storage:R2:Bucket is required.");
        else if (Bucket.Contains('/', StringComparison.Ordinal) ||
                 Bucket.Contains('\\', StringComparison.Ordinal) ||
                 Bucket.Contains(' ', StringComparison.Ordinal))
            errors.Add("Storage:R2:Bucket must be a bucket name, not a path.");

        return errors;
    }
}
