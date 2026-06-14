namespace Unchained.Pptx.Shapes;

/// <summary>
///     A shape that embeds an OLE object (e.g. an Excel spreadsheet or a Word document)
///     inside the presentation. The embedded data is preserved verbatim; editing the OLE
///     object is not supported in M1–M4.
/// </summary>
public sealed class OleShape : Shape
{
    /// <summary>The raw embedded OLE object data.</summary>
    public ReadOnlyMemory<byte> EmbeddedData { get; set; }

    /// <summary>The ProgID of the OLE server that owns this object (e.g. <c>"Excel.Sheet.12"</c>).</summary>
    public string ProgId { get; set; } = string.Empty;

    /// <summary>
    ///     Path of the linked source file, or <see langword="null" /> for fully-embedded objects.
    /// </summary>
    public string? LinkedFilePath { get; set; }
}
