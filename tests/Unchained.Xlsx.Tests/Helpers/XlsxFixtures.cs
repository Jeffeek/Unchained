using Unchained.Xlsx;
using Unchained.Xlsx.Engine;

namespace Unchained.Xlsx.Tests.Helpers;

/// <summary>
///     Shared helpers for building and round-tripping workbooks in tests.
/// </summary>
internal static class XlsxFixtures
{
    /// <summary>Creates a blank in-memory workbook with the given sheet names.</summary>
    public static SpreadsheetDocument WithSheets(params string[] sheetNames)
    {
        using var processor = new SpreadsheetProcessor();
        var document = processor.CreateBlank(sheetNames.Length > 0 ? sheetNames[0] : "Sheet1");
        for (var i = 1; i < sheetNames.Length; i++)
            document.Sheets.Add(sheetNames[i]);
        return document;
    }

    /// <summary>Saves <paramref name="document" /> to bytes and reloads it through a fresh processor.</summary>
    public static async Task<SpreadsheetDocument> RoundTripAsync(SpreadsheetDocument document)
    {
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream();
        await processor.SaveAsync(document, ms);
        return await processor.LoadAsync(ms.ToArray());
    }

    /// <summary>Saves <paramref name="document" /> to a byte array.</summary>
    public static async Task<byte[]> SaveBytesAsync(SpreadsheetDocument document)
    {
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream();
        await processor.SaveAsync(document, ms);
        return ms.ToArray();
    }
}
