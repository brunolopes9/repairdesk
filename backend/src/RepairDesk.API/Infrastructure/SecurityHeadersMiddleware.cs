namespace RepairDesk.API.Infrastructure;

/// <summary>
/// Sprint 249 (Doc 74): aplica HTTP security headers a TODAS as responses.
///
/// Cobertura:
/// - <c>Strict-Transport-Security</c> (HSTS) — só HTTPS em Production
/// - <c>X-Content-Type-Options: nosniff</c> — impede browsers de adivinharem MIME
/// - <c>X-Frame-Options: DENY</c> — impede clickjacking (legacy; CSP frame-ancestors é o moderno)
/// - <c>Referrer-Policy: strict-origin-when-cross-origin</c> — não leak path em cross-origin
/// - <c>Permissions-Policy</c> — desactiva APIs sensíveis que não usamos (câmara, mic, etc.)
/// - <c>Content-Security-Policy</c> — restringe sources de scripts/imagens/etc.
/// - <c>Cross-Origin-Opener-Policy: same-origin</c> — isolamento de processo
/// - <c>Cross-Origin-Resource-Policy: same-site</c>
///
/// CSP é definida em modo conservador. Frontend Vite SPA carrega tudo do mesmo origin
/// após build (nginx serve estáticos do mesmo host). Imagens públicas via R2 são
/// permitidas via <c>img-src</c>. wa.me / tel: são esquemas — não scripts.
///
/// O middleware corre cedo no pipeline (antes de UseCors) para garantir que respostas
/// CORS preflight também levam os headers.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;
    private readonly string? _r2PublicUrl;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env, IConfiguration configuration)
    {
        _next = next;
        _env = env;
        // Sprint 169: URL pública R2 (img-src). Vazio em dev (storage local).
        _r2PublicUrl = configuration["Storage:R2:PublicUrl"];
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var headers = ctx.Response.Headers;

            // Servidor — remover identificação (defesa em profundidade, não secreto).
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Desactiva APIs sensíveis para mitigar plugins/extensions malignos
            // a usar a tab para acesso ao hardware. Mender não usa nenhuma destas.
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()";

            // Isolamento de processo browser (mitiga Spectre + cross-window attacks).
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-site";

            // CSP: restritivo. Os endpoints da API devolvem JSON/PDF/imagens — nunca
            // HTML para o browser interpretar como página. Mesmo assim, definimos CSP
            // para o caso de algum endpoint devolver HTML por engano (ex.: erro de
            // configuração que sirva debug page).
            var cspParts = new List<string>
            {
                "default-src 'none'",
                "frame-ancestors 'none'",     // duplica X-Frame-Options para browsers modernos
                "base-uri 'none'",
                "form-action 'none'",
            };
            // PDFs e imagens podem ser embedded — permitir self e R2 público.
            var imgSrc = "img-src 'self' data:";
            if (!string.IsNullOrWhiteSpace(_r2PublicUrl))
                imgSrc += $" {_r2PublicUrl}";
            cspParts.Add(imgSrc);

            headers["Content-Security-Policy"] = string.Join("; ", cspParts);

            // HSTS — só em Production (HTTP em dev é normal).
            // max-age 1 ano, includeSubDomains, preload-ready.
            if (_env.IsProduction())
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";

            return Task.CompletedTask;
        });

        await _next(ctx);
    }
}
