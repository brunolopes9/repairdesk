namespace RepairDesk.Services.Calendario;

/// <summary>Sprint 619: descarrega um .ics. Isolado para os testes poderem trocar por um falso.</summary>
public interface IIcsFetcher
{
    Task<string> ObterAsync(Uri url, CancellationToken ct = default);
}

/// <summary>
/// Só https e só domínios de calendários conhecidos (evita usar o servidor para pedir endereços internos — SSRF).
/// Segue no máximo 3 redireções, e só para domínios da mesma lista. Limite de 5 MB e 15 s.
/// </summary>
public sealed class IcsFetcher : IIcsFetcher
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private readonly HttpClient _http;
    public IcsFetcher(HttpClient http) => _http = http;

    public async Task<string> ObterAsync(Uri url, CancellationToken ct = default)
    {
        var atual = url;
        for (var i = 0; i <= 3; i++)
        {
            if (!CalendarioUrl.Permitido(atual)) throw new InvalidOperationException("Endereço de calendário não permitido.");
            using var req = new HttpRequestMessage(HttpMethod.Get, atual);
            req.Headers.TryAddWithoutValidation("Accept", "text/calendar, */*");
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location is { } loc)
            {
                atual = loc.IsAbsoluteUri ? loc : new Uri(atual, loc);
                continue;
            }
            if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"O calendário respondeu {(int)resp.StatusCode}.");
            if (resp.Content.Headers.ContentLength > MaxBytes) throw new InvalidOperationException("Calendário demasiado grande.");
            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int n;
            while ((n = await s.ReadAsync(buffer, ct)) > 0)
            {
                ms.Write(buffer, 0, n);
                if (ms.Length > MaxBytes) throw new InvalidOperationException("Calendário demasiado grande.");
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        throw new InvalidOperationException("Demasiadas redireções.");
    }
}

public static class CalendarioUrl
{
    private static readonly string[] Dominios =
        ["calendar.google.com", "outlook.office365.com", "outlook.live.com", "outlook.office.com", "icloud.com"];

    public static bool Permitido(Uri u)
        => u.Scheme == Uri.UriSchemeHttps && u.IsDefaultPort
           && Dominios.Any(d => u.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

    /// <summary>Aceita https:// ou webcal:// (o iCloud usa webcal); devolve null se não for de um calendário conhecido.</summary>
    public static Uri? Normalizar(string? texto)
    {
        var t = texto?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (t.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) t = "https://" + t["webcal://".Length..];
        return Uri.TryCreate(t, UriKind.Absolute, out var u) && Permitido(u) ? u : null;
    }
}
