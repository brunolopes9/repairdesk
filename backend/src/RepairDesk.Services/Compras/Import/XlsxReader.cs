using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace RepairDesk.Services.Compras.Import;

/// <summary>
/// Leitor mínimo de .xlsx (Office Open XML): devolve os VALORES das células (fórmulas já calculadas
/// pelo Excel) por folha → linha → coluna. Sem dependências externas; não precisa de estilos nem fórmulas.
/// </summary>
public sealed class XlsxReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly Dictionary<string, Dictionary<int, Dictionary<string, string>>> _sheets = new(StringComparer.OrdinalIgnoreCase);

    private XlsxReader() { }

    public IReadOnlyCollection<string> SheetNames => _sheets.Keys;

    /// <summary>Linhas da folha (nº da linha → coluna → valor). Vazio se a folha não existir.</summary>
    public IReadOnlyDictionary<int, Dictionary<string, string>> Sheet(string name)
        => _sheets.TryGetValue(name, out var s) ? s : new Dictionary<int, Dictionary<string, string>>();

    public static XlsxReader Read(Stream xlsx)
    {
        using var zip = new ZipArchive(xlsx, ZipArchiveMode.Read, leaveOpen: true);
        var reader = new XlsxReader();

        var shared = new List<string>();
        var sst = zip.GetEntry("xl/sharedStrings.xml");
        if (sst is not null)
        {
            var doc = Load(sst);
            shared.AddRange(doc.Root!.Elements(Main + "si").Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value))));
        }

        var workbook = Load(zip.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("Ficheiro .xlsx inválido (sem workbook)."));
        var rels = Load(zip.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException("Ficheiro .xlsx inválido (sem relações)."));
        var targets = rels.Root!.Elements(PkgRel + "Relationship")
            .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);

        foreach (var sheet in workbook.Root!.Descendants(Main + "sheet"))
        {
            var name = (string)sheet.Attribute("name")!;
            var rid = (string)sheet.Attribute(Rel + "id")!;
            if (!targets.TryGetValue(rid, out var target)) continue;
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            var entry = zip.GetEntry(path);
            if (entry is null) continue;

            var rows = new Dictionary<int, Dictionary<string, string>>();
            foreach (var row in Load(entry).Descendants(Main + "row"))
            {
                var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in row.Elements(Main + "c"))
                {
                    var reference = (string?)c.Attribute("r");
                    if (reference is null) continue;
                    var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                    var type = (string?)c.Attribute("t");
                    string? value = type switch
                    {
                        "s" => c.Element(Main + "v") is { } v && int.TryParse(v.Value, out var i) && i < shared.Count ? shared[i] : null,
                        "inlineStr" => string.Concat(c.Descendants(Main + "t").Select(t => t.Value)),
                        _ => c.Element(Main + "v")?.Value,
                    };
                    if (!string.IsNullOrEmpty(value)) cells[column] = value;
                }
                if (cells.Count > 0 && int.TryParse((string?)row.Attribute("r"), out var n)) rows[n] = cells;
            }
            reader._sheets[name] = rows;
        }
        return reader;
    }

    private static XDocument Load(ZipArchiveEntry entry)
    {
        using var s = entry.Open();
        return XDocument.Load(s);
    }

    public static decimal? Number(Dictionary<string, string> row, string col)
        => row.TryGetValue(col, out var v) && decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static string? Text(Dictionary<string, string> row, string col)
        => row.TryGetValue(col, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    /// <summary>Datas do Excel vêm como nº de série (dias desde 30/12/1899).</summary>
    public static DateTime? Date(Dictionary<string, string> row, string col)
    {
        if (Number(row, col) is { } serial and > 0 and < 2_958_466)
            return DateTime.SpecifyKind(new DateTime(1899, 12, 30).AddDays((double)Math.Floor(serial)), DateTimeKind.Utc);
        if (Text(row, col) is { } t && DateTime.TryParse(t, CultureInfo.GetCultureInfo("pt-PT"), DateTimeStyles.AssumeUniversal, out var dt))
            return DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc);
        return null;
    }
}
