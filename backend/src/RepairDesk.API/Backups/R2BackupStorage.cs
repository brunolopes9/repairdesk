using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace RepairDesk.API.Backups;

public interface IBackupRemoteStorage
{
    bool IsConfigured { get; }
    Task<string> UploadAsync(string localPath, string fileName, BackupR2Options options, CancellationToken ct = default);
    Task<IReadOnlyList<BackupFileDto>> ListAsync(BackupR2Options options, CancellationToken ct = default);
}

public sealed class R2BackupStorage : IBackupRemoteStorage, IDisposable
{
    private readonly ILogger<R2BackupStorage> _logger;
    private Lazy<IAmazonS3>? _client;
    private string? _clientFingerprint;

    public R2BackupStorage(ILogger<R2BackupStorage> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured => true;

    public async Task<string> UploadAsync(string localPath, string fileName, BackupR2Options options, CancellationToken ct = default)
    {
        if (!options.IsConfigured)
            throw new InvalidOperationException("R2 backup storage is not configured.");

        var key = options.BuildKey(fileName);
        await using var stream = File.OpenRead(localPath);
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = "application/octet-stream",
            AutoCloseStream = false,
        };

        await GetClient(options).PutObjectAsync(request, ct);
        _logger.LogInformation("BackupUploaded Bucket={Bucket} Key={R2Key}", options.Bucket, key);
        return key;
    }

    public async Task<IReadOnlyList<BackupFileDto>> ListAsync(BackupR2Options options, CancellationToken ct = default)
    {
        if (!options.IsConfigured)
            return [];

        var prefix = string.IsNullOrWhiteSpace(options.Prefix) ? string.Empty : $"{options.Prefix}/";
        var client = GetClient(options);
        var results = new List<BackupFileDto>();
        string? token = null;

        do
        {
            var response = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = options.Bucket,
                Prefix = prefix,
                ContinuationToken = token,
            }, ct);

            results.AddRange(response.S3Objects
                .Where(o => o.Key.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                .Select(o => new BackupFileDto(
                    Path.GetFileName(o.Key),
                    BackupLocation.R2,
                    o.LastModified.GetValueOrDefault(DateTime.UtcNow),
                    o.Size.GetValueOrDefault(),
                    "available",
                    null,
                    o.Key)));

            token = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrWhiteSpace(token));

        return results.OrderByDescending(b => b.Timestamp).ToList();
    }

    public void Dispose()
    {
        if (_client?.IsValueCreated == true)
            _client.Value.Dispose();
    }

    private IAmazonS3 GetClient(BackupR2Options options)
    {
        var fingerprint = $"{options.AccountId}|{options.AccessKey}|{options.Bucket}";
        if (_client is not null && _clientFingerprint == fingerprint)
            return _client.Value;

        _client = new Lazy<IAmazonS3>(() =>
        {
            var config = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                AuthenticationRegion = "auto",
                ForcePathStyle = true,
            };
            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.Secret), config);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        _clientFingerprint = fingerprint;
        return _client.Value;
    }
}
