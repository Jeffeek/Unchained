namespace Unchained.Ooxml.Engine;

/// <summary>
///     The three OOXML document families the shared core can open via the Open XML SDK.
/// </summary>
public enum OoxmlFormat
{
    /// <summary>PowerPoint presentation (<c>.pptx</c> / <c>.pptm</c>).</summary>
    Presentation,

    /// <summary>Word document (<c>.docx</c> / <c>.docm</c>).</summary>
    Wordprocessing,

    /// <summary>Excel workbook (<c>.xlsx</c> / <c>.xlsm</c>).</summary>
    Spreadsheet
}
