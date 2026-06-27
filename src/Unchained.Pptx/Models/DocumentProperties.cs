using Unchained.Ooxml.Properties;

namespace Unchained.Pptx.Models;

/// <summary>
///     Metadata properties of a presentation document, drawn from the OPC core properties
///     (<c>docProps/core.xml</c>) and the extended app properties (<c>docProps/app.xml</c>).
/// </summary>
public sealed class DocumentProperties : OoXmlCoreProperties
{
    /// <summary>The revision number, incremented on each save.</summary>
    public int? RevisionNumber { get; set; }

    // ── Read-only statistics ──────────────────────────────────────────────────

    /// <summary>The total number of slides in the presentation (read-only).</summary>
    public int SlideCount { get; internal set; }

    /// <summary>The number of hidden slides (read-only).</summary>
    public int HiddenSlideCount { get; internal set; }

    /// <summary>The number of slides that have speaker notes (read-only).</summary>
    public int NoteCount { get; internal set; }

    // ── Custom properties ─────────────────────────────────────────────────────

    /// <summary>
    ///     Arbitrary name/value pairs stored in <c>docProps/custom.xml</c>.
    ///     Values may be <see langword="string" />, <see langword="int" />,
    ///     <see langword="double" />, <see langword="bool" />, or <see cref="DateTimeOffset" />.
    /// </summary>
    public Dictionary<string, object?> CustomProperties { get; } = new(StringComparer.Ordinal);
}
