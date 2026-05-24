using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace RepairDesk.API.Backups;

/// <summary>
/// Sprint 352 (Doc 76 gap crítico): replica DataProtection keys off-VPS encriptadas.
///
/// **Porquê:** se o volume <c>dp_keys</c> desaparecer (VPS perdida, disco corrompido,
/// rebuild com volume reset), TODOS os secrets cifrados em DB tornam-se ilegíveis —
/// Moloni refresh tokens, Anthropic key per-tenant, OAuth state, etc. Identificado
/// como single point of failure em Doc 76 §inventário.
///
/// **Como:**
/// 1. Tar todo o conteúdo de <c>/data/dp-keys</c> em memória.
/// 2. AES-GCM encrypt com chave derivada de password (PBKDF2 100k iterations, SHA256).
/// 3. Upload R2 separado dos backups SQL (prefixo <c>dp-keys/yyyy/MM/...</c>).
///
/// **Restore:** documentado em Doc 76 §3. Script <c>scripts/Restore-DpKeys.ps1</c>
/// faz o inverso.
///
/// **Segurança:** password vive em env var <c>DpKeysBackup__Password</c>. Bruno guarda
/// cópia em 1Password. Compromise password + R2 access = compromise dp-keys.
/// </summary>
public interface IDpKeysBackupService
{
    bool IsConfigured { get; }
    Task<DpKeysBackupResult> RunBackupAsync(CancellationToken ct);
}

public sealed record DpKeysBackupResult(string R2Key, long EncryptedBytes, int KeyCount);

public sealed class DpKeysBackupOptions
{
    public bool Enabled { get; set; }
    public string KeysPath { get; set; } = "/data/dp-keys";
    public string CronSchedule { get; set; } = "03:30";
    /// <summary>Password offline — deriva chave AES via PBKDF2. Min 16 chars.</summary>
    public string Password { get; set; } = "";
    /// <summary>Bucket separado (ou mesmo) dos SQL backups.</summary>
    public BackupR2Options R2 { get; set; } = new();
}

public sealed class DpKeysBackupService : IDpKeysBackupService, IDisposable
{
    private const int PbkdfIterations = 100_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;  // AES-GCM standard
    private const int TagSize = 16;     // AES-GCM standard
    private const int KeySize = 32;     // AES-256

    private readonly DpKeysBackupOptions _options;
    private readonly ILogger<DpKeysBackupService> _logger;
    private Lazy<IAmazonS3>? _client;

    public DpKeysBackupService(DpKeysBackupOptions options, ILogger<DpKeysBackupService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.Password)
        && _options.Password.Length >= 16
        && _options.R2.IsConfigured
        && Directory.Exists(_options.KeysPath);

    public async Task<DpKeysBackupResult> RunBackupAsync(CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("DpKeysBackup not configured (Enabled, Password >= 16 chars, R2, KeysPath).");

        // 1. Tar o conteúdo
        await using var tarStream = new MemoryStream();
        var keyCount = await TarKeysDirectoryAsync(_options.KeysPath, tarStream, ct);
        tarStream.Position = 0;
        if (keyCount == 0)
        {
            _logger.LogWarning("DpKeysBackup: 0 keys em {Path} — nada para fazer", _options.KeysPath);
            throw new InvalidOperationException("Nenhuma key encontrada em " + _options.KeysPath);
        }

        // 2. Encrypt
        var encrypted = EncryptStream(tarStream, _options.Password);

        // 3. Upload R2
        var key = BuildR2Key(DateTimeOffset.UtcNow);
        await UploadAsync(key, encrypted, ct);

        _logger.LogInformation(
            "DpKeysBackupSuccess KeyCount={KeyCount} EncryptedBytes={Bytes} R2Key={R2Key}",
            keyCount, encrypted.Length, key);

        return new DpKeysBackupResult(key, encrypted.Length, keyCount);
    }

    private static async Task<int> TarKeysDirectoryAsync(string root, Stream output, CancellationToken ct)
    {
        await using var writer = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: true);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var relativeName = Path.GetRelativePath(root, file).Replace('\\', '/');
            var entry = new PaxTarEntry(TarEntryType.RegularFile, relativeName)
            {
                ModificationTime = File.GetLastWriteTimeUtc(file),
                DataStream = File.OpenRead(file),
            };
            await writer.WriteEntryAsync(entry, ct);
            await entry.DataStream!.DisposeAsync();
        }
        return files.Count;
    }

    private static byte[] EncryptStream(MemoryStream plaintext, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            PbkdfIterations,
            HashAlgorithmName.SHA256,
            KeySize);

        var plain = plaintext.ToArray();
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        // Layout: [magic:8][version:1][salt:16][nonce:12][tag:16][cipher:N]
        // Magic "MDRDP_K1" (Mender Data Protection Keys v1) + version byte para futuros formatos.
        var magic = Encoding.ASCII.GetBytes("MDRDP_K1");
        var buffer = new byte[magic.Length + 1 + SaltSize + NonceSize + TagSize + cipher.Length];
        var offset = 0;
        Buffer.BlockCopy(magic, 0, buffer, offset, magic.Length); offset += magic.Length;
        buffer[offset++] = 0x01;  // version
        Buffer.BlockCopy(salt, 0, buffer, offset, SaltSize); offset += SaltSize;
        Buffer.BlockCopy(nonce, 0, buffer, offset, NonceSize); offset += NonceSize;
        Buffer.BlockCopy(tag, 0, buffer, offset, TagSize); offset += TagSize;
        Buffer.BlockCopy(cipher, 0, buffer, offset, cipher.Length);
        return buffer;
    }

    /// <summary>Inverso de EncryptStream — usado apenas em tests e no script de restore.</summary>
    public static byte[] DecryptStream(byte[] encrypted, string password)
    {
        var magic = Encoding.ASCII.GetBytes("MDRDP_K1");
        if (encrypted.Length < magic.Length + 1 + SaltSize + NonceSize + TagSize)
            throw new CryptographicException("dp-keys backup demasiado curto");
        for (int i = 0; i < magic.Length; i++)
            if (encrypted[i] != magic[i])
                throw new CryptographicException("dp-keys backup com magic inválido");
        if (encrypted[magic.Length] != 0x01)
            throw new CryptographicException("dp-keys backup versão desconhecida");

        var offset = magic.Length + 1;
        var salt = encrypted[offset..(offset + SaltSize)]; offset += SaltSize;
        var nonce = encrypted[offset..(offset + NonceSize)]; offset += NonceSize;
        var tag = encrypted[offset..(offset + TagSize)]; offset += TagSize;
        var cipher = encrypted[offset..];

        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            PbkdfIterations,
            HashAlgorithmName.SHA256,
            KeySize);

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private string BuildR2Key(DateTimeOffset stamp)
    {
        var name = $"dp-keys-{stamp:yyyyMMdd-HHmm}.tar.aes";
        var prefix = string.IsNullOrWhiteSpace(_options.R2.Prefix)
            ? "dp-keys"
            : $"{_options.R2.Prefix.TrimEnd('/')}/dp-keys";
        return $"{prefix}/{stamp:yyyy}/{stamp:MM}/{name}";
    }

    private async Task UploadAsync(string key, byte[] payload, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.R2.Bucket,
            Key = key,
            InputStream = new MemoryStream(payload),
            ContentType = "application/octet-stream",
            AutoCloseStream = true,
        };
        await GetClient().PutObjectAsync(request, ct);
    }

    private IAmazonS3 GetClient()
    {
        _client ??= new Lazy<IAmazonS3>(() =>
        {
            var config = new AmazonS3Config
            {
                ServiceURL = _options.R2.Endpoint,
                AuthenticationRegion = "auto",
                ForcePathStyle = true,
            };
            return new AmazonS3Client(
                new BasicAWSCredentials(_options.R2.AccessKey, _options.R2.Secret),
                config);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        return _client.Value;
    }

    public void Dispose()
    {
        if (_client?.IsValueCreated == true) _client.Value.Dispose();
    }
}
