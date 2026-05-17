using System.Globalization;
using System.Text;

namespace RepairDesk.Common.Helpers;

/// <summary>
/// Writer CSV minimalista, RFC 4180-compatible, Excel-friendly:
/// - UTF-8 com BOM (Excel PT abre correctamente acentos)
/// - Separador <c>,</c> por defeito (configurável)
/// - Aspas duplas escaped como <c>""</c>
/// - Campos com vírgula, quebra de linha ou aspas são automaticamente envoltos em aspas
/// - Datas em formato ISO 8601 (yyyy-MM-dd) ou ISO completo
/// - Decimais com ponto decimal (cultura invariante)
/// </summary>
public sealed class CsvBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly char _separator;
    private bool _firstCellInRow = true;

    public CsvBuilder(char separator = ',')
    {
        _separator = separator;
    }

    public CsvBuilder Row(params object?[] cells)
    {
        _firstCellInRow = true;
        foreach (var c in cells) Cell(c);
        _sb.Append('\n');
        return this;
    }

    public CsvBuilder Cell(object? value)
    {
        if (!_firstCellInRow) _sb.Append(_separator);
        _firstCellInRow = false;
        _sb.Append(Escape(value));
        return this;
    }

    public byte[] ToUtf8WithBom()
    {
        var utf8 = Encoding.UTF8.GetBytes(_sb.ToString());
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var result = new byte[bom.Length + utf8.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(utf8, 0, result, bom.Length, utf8.Length);
        return result;
    }

    public override string ToString() => _sb.ToString();

    private string Escape(object? value)
    {
        var s = value switch
        {
            null => "",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? "",
        };
        // Precisa de quoting?
        if (s.Contains(_separator) || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
        {
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
}
