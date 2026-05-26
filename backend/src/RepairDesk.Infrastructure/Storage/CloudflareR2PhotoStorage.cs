using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 implementation of IPhotoStorage using the standard S3-compatible AWSSDK.S3 client.
/// </summary>
public sealed class CloudflareR2PhotoStorage : IPhotoStorage, IDisposable
{
    private readonly R2StorageOptions _options;
    private readonly Lazy<IAmazonS3> _client;

    public CloudflareR2PhotoStorage(IConfiguration configuration)
    {
        _options = R2StorageOptions.FromConfiguration(configuration);
        _client = new Lazy<IAmazonS3>(CreateClient, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = NormalizeKey(key),
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // Cloudflare R2 não suporta chunked SigV4 encoding (STREAMING-AWS4-HMAC-SHA256-PAYLOAD).
            // Desactivar chunked encoding força um PUT regular com Content-Length conhecido.
            UseChunkEncoding = false,
            DisablePayloadSigning = true,
        };

        await _client.Value.PutObjectAsync(request, ct);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            using var response = await _client.Value.GetObjectAsync(_options.Bucket, NormalizeKey(key), ct);
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            return buffer;
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            throw new FileNotFoundException($"Foto nao encontrada: {key}", ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.Value.DeleteObjectAsync(_options.Bucket, NormalizeKey(key), ct);
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            // Delete is intentionally idempotent.
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.Value.GetObjectMetadataAsync(_options.Bucket, NormalizeKey(key), ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_client.IsValueCreated)
            _client.Value.Dispose();
    }

    private IAmazonS3 CreateClient()
    {
        _options.Validate();

        // AWSSDK.S3 3.7.x — sem trailer-encoding por defeito, compatível com Cloudflare R2.
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // Cloudflare R2 não suporta STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER (default no
            // AWSSDK 4.x). Forçar checksum calculation a WhenRequired evita o trailer.
            RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED,
        };

        return new AmazonS3Client(new BasicAWSCredentials(_options.AccessKey, _options.Secret), config);
    }

    private static bool IsNotFound(AmazonS3Exception ex) =>
        ex.StatusCode == HttpStatusCode.NotFound ||
        string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Path.IsPathRooted(key) ||
            key.Contains("..", StringComparison.Ordinal) ||
            key.Contains('\\', StringComparison.Ordinal))
            throw new ArgumentException("Chave invalida.", nameof(key));

        return key.TrimStart('/');
    }
}
