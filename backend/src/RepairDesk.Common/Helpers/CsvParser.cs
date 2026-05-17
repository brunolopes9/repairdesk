namespace RepairDesk.Common.Helpers;

/// <summary>
/// Parser CSV minimalista, tolerante a Excel/Google Sheets exports:
/// - Aceita separadores <c>,</c>, <c>;</c> ou <c>\t</c> (auto-detecta na 1ª linha)
/// - Suporta campos com aspas duplas que contenham separador, quebras de linha e "" escapadas
/// - Ignora BOM UTF-8 inicial
/// - Linhas em branco são descartadas
///
/// Não é compliance total RFC 4180 — privilegia robustez para inputs reais.
/// </summary>
public static class CsvParser
{
    public static List<string[]> Parse(string content)
    {
        if (string.IsNullOrEmpty(content)) return new List<string[]>();

        // Remove BOM UTF-8 se presente
        if (content[0] == '﻿') content = content[1..];

        var separator = DetectSeparator(content);
        var rows = new List<string[]>();
        var current = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == separator)
                {
                    current.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r')
                {
                    // ignore — \n seguinte fecha a linha
                }
                else if (c == '\n')
                {
                    current.Add(field.ToString());
                    field.Clear();
                    if (current.Any(s => !string.IsNullOrWhiteSpace(s)))
                        rows.Add(current.ToArray());
                    current = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }
        }
        // Última linha sem \n final
        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            if (current.Any(s => !string.IsNullOrWhiteSpace(s)))
                rows.Add(current.ToArray());
        }
        return rows;
    }

    private static char DetectSeparator(string content)
    {
        var firstLine = content.Split('\n')[0];
        // Outside-quotes counts são suficientes — a primeira linha (header) raramente tem quotes
        var commas = firstLine.Count(c => c == ',');
        var semis = firstLine.Count(c => c == ';');
        var tabs = firstLine.Count(c => c == '\t');
        if (tabs > commas && tabs > semis) return '\t';
        if (semis > commas) return ';';
        return ',';
    }
}
