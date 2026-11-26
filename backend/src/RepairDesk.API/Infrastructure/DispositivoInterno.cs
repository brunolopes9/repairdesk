namespace RepairDesk.API.Infrastructure;

/// <summary>
/// 26/11 (Bruno: «isto não pode considerar nunca a minha visita»): todo o navegador onde se entra no Mender fica marcado
/// como «da casa». O js/visitas.js do lopestech.pt envia os eventos para app.lopestech.pt por sendBeacon — mesmo site, por
/// isso o cookie segue com o pedido e as visitas desse navegador nunca chegam a ser guardadas. Renova-se em cada login
/// e refresh (o Mender faz refresh ao abrir), por isso não expira enquanto se usar o Mender nesse aparelho.
/// </summary>
public static class DispositivoInterno
{
    public const string Cookie = "rd_interno";

    public static void Marcar(HttpRequest request, HttpResponse response)
        => response.Cookies.Append(Cookie, "1", new CookieOptions
        {
            HttpOnly = true,
            Secure = string.Equals(request.Scheme, "https", StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Lax,
            Path = "/api/public",
            Expires = DateTimeOffset.UtcNow.AddDays(400),
            IsEssential = true,
        });

    public static bool Eh(HttpRequest request) => request.Cookies.ContainsKey(Cookie);
}
