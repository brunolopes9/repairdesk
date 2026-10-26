using System.Globalization;
using System.Text;

namespace RepairDesk.Common.Helpers;

/// <summary>
/// Sprint 566: códigos de barras EAN-13 — validação, código interno da loja e desenho (SVG).
/// Interno = prefixo "20" (gama reservada a uso dentro da loja, não colide com marcas) + nº do artigo
/// com 10 dígitos + dígito de controlo. Qualquer leitor de códigos de barras lê EAN-13.
/// </summary>
public static class Ean13
{
    // Padrões L (ímpar), G (par) e R por dígito; a paridade dos 6 primeiros vem do 1.º dígito.
    private static readonly string[] L = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];
    private static readonly string[] G = ["0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111"];
    private static readonly string[] R = ["1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100"];
    private static readonly string[] Paridade = ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    public static string Interno(int numero)
    {
        if (numero is < 0 or > 999_999_999) throw new ArgumentOutOfRangeException(nameof(numero));
        var doze = "20" + numero.ToString("D10", CultureInfo.InvariantCulture);
        return doze + DigitoControlo(doze);
    }

    public static bool Valido(string? codigo)
        => codigo is { Length: 13 } && codigo.All(char.IsAsciiDigit) && codigo[12] - '0' == DigitoControlo(codigo[..12]);

    public static int DigitoControlo(string dozeDigitos)
    {
        var soma = 0;
        for (var i = 0; i < 12; i++) soma += (dozeDigitos[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - soma % 10) % 10;
    }

    /// <summary>Sequência de módulos (1 = barra) — 95 módulos.</summary>
    public static string Modulos(string codigo)
    {
        if (!Valido(codigo)) throw new ArgumentException("EAN-13 inválido.", nameof(codigo));
        var sb = new StringBuilder("101");
        var par = Paridade[codigo[0] - '0'];
        for (var i = 1; i <= 6; i++) sb.Append(par[i - 1] == 'L' ? L[codigo[i] - '0'] : G[codigo[i] - '0']);
        sb.Append("01010");
        for (var i = 7; i <= 12; i++) sb.Append(R[codigo[i] - '0']);
        sb.Append("101");
        return sb.ToString();
    }

    /// <summary>
    /// SVG só com as barras (viewBox em módulos, com 9 de margem de cada lado) — o texto vai à parte.
    /// <paramref name="altura"/> em módulos (a largura são 113): dá a proporção do espaço da etiqueta.
    /// </summary>
    public static string Svg(string codigo, int altura = 60)
    {
        const int margem = 9;
        altura = Math.Clamp(altura, 10, 400);
        var modulos = Modulos(codigo);
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {modulos.Length + 2 * margem} {altura}\" preserveAspectRatio=\"none\">");
        for (var i = 0; i < modulos.Length;)
        {
            if (modulos[i] == '0') { i++; continue; }
            var ini = i;
            while (i < modulos.Length && modulos[i] == '1') i++;
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{ini + margem}\" y=\"0\" width=\"{i - ini}\" height=\"{altura}\" fill=\"#000\"/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }
}
