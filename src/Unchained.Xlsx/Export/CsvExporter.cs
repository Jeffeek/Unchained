using System.Globalization;
using System.Text;
using Unchained.Xlsx.Formatting;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Export;

/// <summary>Exports a worksheet's cell values to CSV.</summary>
internal static class CsvExporter
{
    public static byte[] Export(Worksheet sheet, CsvSaveOptions options)
    {
        var range = options.Range ?? sheet.GetUsedRange();
        var builder = new StringBuilder();

        if (range != null)
        {
            for (var row = range.Value.TopLeft.Row; row <= range.Value.BottomRight.Row; row++)
            {
                var fields = new List<string>(range.Value.ColumnCount);
                for (var col = range.Value.TopLeft.Column; col <= range.Value.BottomRight.Column; col++)
                    fields.Add(Escape(FormatCell(sheet.GetCell(row, col), options), options));

                builder.Append(string.Join(options.Delimiter, fields));
                builder.Append("\r\n");
            }
        }

        var text = builder.ToString();

        // Encode without an embedded BOM, then prepend the preamble only when requested.
        var encoding = options.Encoding is UTF8Encoding
            ? new UTF8Encoding(false)
            : options.Encoding;

        var body = encoding.GetBytes(text);
        var preamble = options.WriteBom ? options.Encoding.GetPreamble() : [];
        if (preamble.Length == 0)
            return body;

        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    private static string FormatCell(Cell.Cell? cell, CsvSaveOptions options) =>
        cell == null
            ? string.Empty
            : cell.CellType switch
            {
                CellType.String => cell.GetString() ?? string.Empty,
                CellType.Boolean => cell.GetBoolean() == true ? options.TrueValue : options.FalseValue,
                CellType.Error => cell.GetError()?.ToLiteral() ?? string.Empty,
                CellType.Number => FormatNumber(cell, options),
                CellType.Formula => cell.GetString() ?? FormatNumber(cell, options) ?? cell.Formula ?? string.Empty,
                _ => string.Empty
            } ?? string.Empty;

    private static string? FormatNumber(Cell.Cell cell, CsvSaveOptions options)
    {
        var value = cell.GetDouble();
        if (value is null)
            return null;

        // Render dates using the configured date format when the cell carries a date number format.
        var code = cell.NumberFormatCode;
        // ReSharper disable once InvertIf
        if (NumberFormatter.IsDateTimeFormatCode(code) && cell.GetDateTime() is { } dt)
        {
            var hasTime = dt.TimeOfDay != TimeSpan.Zero;
            return dt.ToString(hasTime ? options.DateTimeFormat : options.DateFormat, CultureInfo.InvariantCulture);
        }

        return value.Value.ToString(options.NumberFormat, CultureInfo.InvariantCulture);
    }

    private static string Escape(string field, CsvSaveOptions options)
    {
        var needsQuote = options.QuoteAllFields ||
                         (options.QuoteFieldsWithDelimiter &&
                          (field.Contains(options.Delimiter) || field.Contains('"') || field.Contains('\n') || field.Contains('\r')));

        return !needsQuote ? field : "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
