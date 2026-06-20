using System.Text;

namespace Accountrack.SharedKernel.Csv;

/// <summary>
/// Minimal, dependency-free CSV reader/writer (RFC-4180-ish) for the import/export capability
/// (ADR-0031). Handles quoted fields, embedded commas/quotes/newlines, and a header row. Excel
/// (.xlsx) support is layered on later via a spreadsheet library.
/// </summary>
public static class Csv
{
    /// <summary>Parses CSV text into rows of fields. The first row is treated as the header by callers.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        if (string.IsNullOrEmpty(text))
        {
            return rows;
        }

        var field = new StringBuilder();
        var record = new List<string>();
        var inQuotes = false;
        var i = 0;

        void EndField() { record.Add(field.ToString()); field.Clear(); }
        void EndRecord()
        {
            EndField();
            // Skip blank lines (a single empty field).
            if (!(record.Count == 1 && record[0].Length == 0))
            {
                rows.Add(record.ToList());
            }
            record.Clear();
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false; i++; continue;
                }
                field.Append(c); i++; continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; i++; break;
                case ',': EndField(); i++; break;
                case '\r': i++; break;
                case '\n': EndRecord(); i++; break;
                default: field.Append(c); i++; break;
            }
        }

        // Flush the trailing record if the file did not end with a newline.
        if (field.Length > 0 || record.Count > 0)
        {
            EndRecord();
        }

        return rows;
    }

    /// <summary>Writes a header + rows to CSV text, quoting fields that need it.</summary>
    public static string Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', header.Select(Escape))).Append("\r\n");
        foreach (var row in rows)
        {
            sb.Append(string.Join(',', row.Select(f => Escape(f ?? string.Empty)))).Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Escape(string field)
    {
        if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
