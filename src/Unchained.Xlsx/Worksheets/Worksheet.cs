using System.Xml.Linq;
using Unchained.Ooxml.Drawing;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Sheets;

namespace Unchained.Xlsx.Worksheets;

/// <summary>
///     A single worksheet within a <see cref="SpreadsheetDocument" />.
/// </summary>
/// <remarks>
///     A worksheet preserves the raw parsed <c>&lt;worksheet&gt;</c> XML for any content that
///     Unchained does not model explicitly (custom views, sparklines, extension lists). Modeled
///     sections — cell data, merged cells, columns — are rewritten from the object model on save.
/// </remarks>
public sealed partial class Worksheet
{
    internal Worksheet(
        SpreadsheetDocument document,
        string name,
        int sheetId,
        string relationshipId,
        string partUri,
        SheetState state
    )
    {
        Document = document;
        _name = name;
        SheetId = sheetId;
        RelationshipId = relationshipId;
        PartUri = partUri;
        State = state;
    }

    private string _name;

    /// <summary>The owning workbook.</summary>
    internal SpreadsheetDocument Document { get; }

    /// <summary>
    ///     The OPC part URI of this worksheet (e.g. <c>/xl/worksheets/sheet1.xml</c>).
    ///     Empty for sheets created in-memory that have not yet been assigned a part.
    /// </summary>
    internal string PartUri { get; set; }

    /// <summary>The relationship id (<c>rId*</c>) of this sheet within <c>workbook.xml</c>.</summary>
    internal string RelationshipId { get; set; }

    /// <summary>
    ///     The raw parsed <c>&lt;worksheet&gt;</c> element, preserved for round-tripping unmodeled
    ///     content. <see langword="null" /> for in-memory sheets with no backing part yet.
    /// </summary>
    internal XElement? RawElement { get; set; }

    // ── Identity ───────────────────────────────────────────────────────────────

    /// <summary>The user-visible sheet name shown on the tab.</summary>
    /// <exception cref="ArgumentException">Thrown when the name is empty, too long, or contains invalid characters.</exception>
    public string Name
    {
        get => _name;
        set
        {
            ValidateSheetName(value);
            _name = value;
        }
    }

    /// <summary>The stable workbook-unique sheet identifier. Does not change when sheets are reordered.</summary>
    public int SheetId { get; }

    /// <summary>The zero-based position of this sheet within <see cref="SpreadsheetDocument.Sheets" />.</summary>
    public int TabIndex => Document.Sheets.IndexOf(this);

    /// <summary>The visibility state of this sheet.</summary>
    public SheetState State { get; set; }

    /// <summary>The tab colour, or <see langword="null" /> for the default (no colour).</summary>
    public ColorSpec? TabColor { get; set; }

    // ── Validation ─────────────────────────────────────────────────────────────

    private static readonly char[] InvalidNameChars = ['\\', '/', '?', '*', '[', ']', ':'];

    internal static void ValidateSheetName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name.Length > 31)
            throw new ArgumentException("Sheet names cannot exceed 31 characters.", nameof(name));
        if (name.IndexOfAny(InvalidNameChars) >= 0)
            throw new ArgumentException(@"Sheet names cannot contain any of \ / ? * [ ] :", nameof(name));
        if (name.StartsWith('\'') || name.EndsWith('\''))
            throw new ArgumentException("Sheet names cannot start or end with an apostrophe.", nameof(name));
    }
}
