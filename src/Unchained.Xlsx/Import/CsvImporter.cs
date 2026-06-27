using System.Globalization;
using System.Text;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Import;

/// <summary>Parses CSV text into a fresh single-sheet <see cref="SpreadsheetDocument" />.</summary>
internal static class CsvImporter
{
    public static SpreadsheetDocument Import(byte[] data, CsvLoadOptions options, SpreadsheetProcessor processor)
    {
        var encoding = options.Encoding ?? DetectEncoding(data);
        var text = encoding.GetString(StripPreamble(data, encoding));

        var document = processor.CreateBlank(options.SheetName);
        var sheet = document.Sheets[0];

        var rows = ParseCsv(text, options.Delimiter);
        for (var r = 0; r < rows.Count; r++)
        {
            var fields = rows[r];
            for (var c = 0; c < fields.Count; c++)
                AssignValue(sheet, r + 1, c + 1, fields[c], options);
        }

        return document;
    }

    // ReSharper disable BadListLineBreaks
    private static void AssignValue(Worksheet sheet, int row, int column, string field, CsvLoadOptions options)
        // ReSharper restore BadListLineBreaks
    {
        if (field.Length == 0)
            return;

        if (!options.TypeInference)
        {
            sheet.SetValue(row, column, field);
            return;
        }

        if (bool.TryParse(field, out var boolean))
        {
            sheet.SetValue(row, column, boolean);
            return;
        }

        // Avoid treating zero-padded ids ("007") as numbers.
        var looksNumeric = !(field.Length > 1 && field[0] == '0' && field[1] != '.');
        if (looksNumeric &&
            double.TryParse(field, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number))
        {
            sheet.SetValue(row, column, number);
            return;
        }

        if (!string.IsNullOrEmpty(options.DateFormat) &&
            DateTime.TryParseExact(field, options.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            sheet.SetValue(row, column, date);
            return;
        }

        sheet.SetValue(row, column, field);
    }

    // ── CSV parsing (RFC 4180-ish) ──────────────────────────────────────────────

    private static List<List<string>> ParseCsv(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else
                    field.Append(c);

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                break;
                case '\r':
                break; // handled by \n
                case '\n':
                    current.Add(field.ToString());
                    field.Clear();
                    rows.Add(current);
                    current = [];
                break;
                default:
                {
                    if (c == delimiter)
                    {
                        current.Add(field.ToString());
                        field.Clear();
                    }
                    else
                        field.Append(c);

                    break;
                }
            }
        }

        if (field.Length <= 0 && current.Count <= 0) return rows;

        current.Add(field.ToString());
        rows.Add(current);

        return rows;
    }

    private static Encoding DetectEncoding(IReadOnlyList<byte> data) =>
        data switch
        {
            [0xEF, 0xBB, 0xBF, ..] => Encoding.UTF8,
            [0xFF, 0xFE, ..] => Encoding.Unicode,
            [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode,
            _ => Encoding.UTF8
        };

    private static byte[] StripPreamble(byte[] data, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        return preamble.Length == 0 || data.Length < preamble.Length || preamble.Where((t, i) => data[i] != t).Any() ? data : data[preamble.Length..];
    }
}
