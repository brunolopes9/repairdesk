using System.Text;
using System.Text.Json;

namespace RepairDesk.Services.Imei;

public sealed record TacLookupResult(string Tac, string? Brand, string? Model, bool Found);

public interface ITacLookupService
{
    /// <summary>Resolve marca+modelo a partir do IMEI (usa os 8 primeiros dígitos = TAC).</summary>
    TacLookupResult Resolve(string imei);
    /// <summary>Importa um CSV "tac;marca;modelo" (ou separado por vírgula), substitui a base e persiste.</summary>
    Task<int> ImportCsvAsync(Stream csv, CancellationToken ct = default);
    int Count { get; }
}

/// <summary>
/// Sprint 390 (Doc 87/04): lookup TAC→modelo 100% local e offline. A base vive num JSON em disco
/// (data dir) carregado para um dicionário em memória (O(1)). NÃO trazemos um dataset inventado —
/// o admin importa um dump aberto (Osmocom CC-BY-SA / MoazEb MIT, convertido p/ CSV tac;marca;modelo)
/// pelo endpoint de import; daí em diante a deteção é instantânea e grátis. O TAC (8 dígitos) NÃO é
/// dado pessoal (é o código do modelo), por isso nada de sensível sai sequer do servidor.
/// </summary>
public sealed class TacLookupService : ITacLookupService
{
    private readonly string _dataFilePath;
    private volatile IReadOnlyDictionary<string, (string Brand, string Model)> _map =
        new Dictionary<string, (string, string)>();

    public TacLookupService(string dataFilePath)
    {
        _dataFilePath = dataFilePath;
        TryLoadFromDisk();
    }

    public int Count => _map.Count;

    public TacLookupResult Resolve(string imei)
    {
        var tac = ExtractTac(imei);
        if (tac is null) return new TacLookupResult("", null, null, false);
        if (_map.TryGetValue(tac, out var v)) return new TacLookupResult(tac, v.Brand, v.Model, true);
        return new TacLookupResult(tac, null, null, false);
    }

    public async Task<int> ImportCsvAsync(Stream csv, CancellationToken ct = default)
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        using var reader = new StreamReader(csv, Encoding.UTF8);
        string? line;
        var first = true;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var delimiter = line.Contains(';') ? ';' : ',';
            var parts = line.Split(delimiter, 3, StringSplitOptions.TrimEntries);
            if (parts.Length < 3) continue;
            var tac = new string(parts[0].Where(char.IsDigit).ToArray());
            // Salta cabeçalho (ex: "tac;brand;model").
            if (first && (tac.Length != 8 || parts[0].Equals("tac", StringComparison.OrdinalIgnoreCase)))
            {
                first = false;
                continue;
            }
            first = false;
            if (tac.Length != 8) continue;
            var brand = parts[1];
            var model = parts[2];
            if (brand.Length == 0 && model.Length == 0) continue;
            map[tac] = (brand, model);
        }

        _map = map;
        await PersistAsync(map, ct);
        return map.Count;
    }

    private static string? ExtractTac(string? imei)
    {
        if (string.IsNullOrWhiteSpace(imei)) return null;
        var digits = new string(imei.Where(char.IsDigit).ToArray());
        return digits.Length >= 8 ? digits[..8] : null;
    }

    private void TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(_dataFilePath)) return;
            var json = File.ReadAllText(_dataFilePath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
            if (raw is null) return;
            var map = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            foreach (var (tac, arr) in raw)
                if (arr.Length >= 2) map[tac] = (arr[0], arr[1]);
            _map = map;
        }
        catch
        {
            // Base corrompida/ausente não deve impedir o arranque — fica vazia (found:false gracioso).
        }
    }

    private async Task PersistAsync(IReadOnlyDictionary<string, (string Brand, string Model)> map, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_dataFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var raw = map.ToDictionary(kv => kv.Key, kv => new[] { kv.Value.Brand, kv.Value.Model });
        await File.WriteAllTextAsync(_dataFilePath, JsonSerializer.Serialize(raw), ct);
    }
}
