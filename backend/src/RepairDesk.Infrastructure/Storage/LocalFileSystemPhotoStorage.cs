using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.Storage;

/// <summary>
/// Implementação local de IPhotoStorage para desenvolvimento e self-hosted.
/// Guarda ficheiros num directório (configurável via Storage:LocalRoot).
/// Default: /data/photos (volume Docker no container).
/// </summary>
public class LocalFileSystemPhotoStorage : IPhotoStorage
{
    private readonly string _root;

    public LocalFileSystemPhotoStorage(IConfiguration config)
    {
        _root = config["Storage:LocalRoot"] ?? "/data/photos";
        Directory.CreateDirectory(_root);
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Foto não encontrada: {key}");
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        return Task.FromResult(File.Exists(path));
    }

    private string ResolvePath(string key)
    {
        // Validação anti path-traversal
        if (key.Contains("..") || Path.IsPathRooted(key))
            throw new ArgumentException("Chave inválida.", nameof(key));
        return Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
    }
}
