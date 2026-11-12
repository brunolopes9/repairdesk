using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 implementation of IPhotoStorage using the standard S3-compatible AWSSDK.S3 client.
/// </summary>
public sealed class CloudflareR2PhotoStorage : IPhotoStorage, IDirectAccessStorage, IMultipartUploadStorage, IDisposable
{
    private readonly R2StorageOptions _options;
    private readonly Lazy<IAmazonS3> _client;

    static CloudflareR2PhotoStorage()
    {
        // URLs pré-assinados do R2 exigem SigV4 (o SDK usa SigV2 por defeito com ServiceURL custom).
        AWSConfigsS3.UseSignatureVersion4 = true;
    }

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

    // ---- Sprint 598 (Doc 95 §6): acesso direto por URLs pré-assinados ---------------------------

    /// <summary>O R2 só aceita validade até 7 dias.</summary>
    private static readonly TimeSpan MaxPresignValidity = TimeSpan.FromDays(7);

    public string CreateUploadUrl(string key, string contentType, TimeSpan validFor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        return _client.Value.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = NormalizeKey(key),
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(Clamp(validFor)),
            Protocol = Protocol.HTTPS,
        });
    }

    public string CreateDownloadUrl(string key, TimeSpan validFor, string? downloadFileName = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = NormalizeKey(key),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(Clamp(validFor)),
            Protocol = Protocol.HTTPS,
        };
        if (!string.IsNullOrWhiteSpace(downloadFileName))
            request.ResponseHeaderOverrides.ContentDisposition =
                $"attachment; filename*=UTF-8''{Uri.EscapeDataString(downloadFileName)}";
        return _client.Value.GetPreSignedURL(request);
    }

    public async Task<string> StartMultipartUploadAsync(string key, string contentType, CancellationToken ct = default)
    {
        var r = await _client.Value.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = _options.Bucket,
            Key = NormalizeKey(key),
            ContentType = contentType,
        }, ct);
        return r.UploadId;
    }

    public string CreatePartUploadUrl(string key, string uploadId, int partNumber, TimeSpan validFor)
        => _client.Value.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = NormalizeKey(key),
            Verb = HttpVerb.PUT,
            UploadId = uploadId,
            PartNumber = partNumber,
            Expires = DateTime.UtcNow.Add(Clamp(validFor)),
            Protocol = Protocol.HTTPS,
        });

    public async Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyList<MultipartPart> parts, CancellationToken ct = default)
    {
        var req = new CompleteMultipartUploadRequest { BucketName = _options.Bucket, Key = NormalizeKey(key), UploadId = uploadId };
        req.AddPartETags(parts.OrderBy(p => p.PartNumber).Select(p => new PartETag(p.PartNumber, p.ETag)));
        await _client.Value.CompleteMultipartUploadAsync(req, ct);
    }

    public async Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken ct = default)
    {
        try
        {
            await _client.Value.AbortMultipartUploadAsync(_options.Bucket, NormalizeKey(key), uploadId, ct);
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex) || ex.ErrorCode == "NoSuchUpload")
        {
            // Já não existe — nada a fazer.
        }
    }

    public async Task<long?> GetSizeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var meta = await _client.Value.GetObjectMetadataAsync(_options.Bucket, NormalizeKey(key), ct);
            return meta.ContentLength;
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return null;
        }
    }

    public async Task EnsureBrowserCorsAsync(IReadOnlyCollection<string> origins, CancellationToken ct = default)
    {
        if (origins.Count == 0) return;

        // Junta à configuração existente em vez de a substituir — o bucket pode ter outras regras.
        var rules = new List<CORSRule>();
        try
        {
            var current = await _client.Value.GetCORSConfigurationAsync(_options.Bucket, ct);
            if (current.Configuration?.Rules is { } existing) rules.AddRange(existing);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound
                                           || string.Equals(ex.ErrorCode, "NoSuchCORSConfiguration", StringComparison.OrdinalIgnoreCase))
        {
            // Sem regras ainda.
        }

        const string ruleId = "mender-browser-direct";
        var wanted = origins.Select(o => o.TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(o => o).ToList();
        var mine = rules.FirstOrDefault(r => r.Id == ruleId);
        if (mine is not null && mine.AllowedOrigins.OrderBy(o => o).SequenceEqual(wanted, StringComparer.OrdinalIgnoreCase))
            return;

        rules.RemoveAll(r => r.Id == ruleId);
        rules.Add(new CORSRule
        {
            Id = ruleId,
            AllowedOrigins = wanted,
            AllowedMethods = new List<string> { "GET", "PUT", "HEAD" },
            AllowedHeaders = new List<string> { "content-type" },
            ExposeHeaders = new List<string> { "ETag" },
            MaxAgeSeconds = 3600,
        });
        await _client.Value.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
        {
            BucketName = _options.Bucket,
            Configuration = new CORSConfiguration { Rules = rules },
        }, ct);
    }

    private static TimeSpan Clamp(TimeSpan validFor)
        => validFor <= TimeSpan.Zero ? TimeSpan.FromMinutes(15) : validFor > MaxPresignValidity ? MaxPresignValidity : validFor;

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
