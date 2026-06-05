namespace Unchained.Pptx.Models;

/// <summary>
/// Metadata properties of a presentation document, drawn from the OPC core properties
/// (<c>docProps/core.xml</c>) and the extended app properties (<c>docProps/app.xml</c>).
/// </summary>
public sealed class DocumentProperties
{
    // ── Core (Dublin Core / OOXML core properties) ───────────────────────────

    /// <summary>The presentation title.</summary>
    public string? Title { get; set; }

    /// <summary>The subject of the presentation.</summary>
    public string? Subject { get; set; }

    /// <summary>The primary author (creator) of the presentation.</summary>
    public string? Author { get; set; }

    /// <summary>The user who last saved the presentation.</summary>
    public string? LastModifiedBy { get; set; }

    /// <summary>A comma-separated list of keywords associated with the presentation.</summary>
    public string? Keywords { get; set; }

    /// <summary>A description or abstract of the presentation.</summary>
    public string? Description { get; set; }

    /// <summary>The content category (e.g. "Sales", "Engineering").</summary>
    public string? Category { get; set; }

    /// <summary>The content status (e.g. "Draft", "Final").</summary>
    public string? ContentStatus { get; set; }

    // ── Dates ─────────────────────────────────────────────────────────────────

    /// <summary>The date and time when the presentation was first created.</summary>
    public DateTimeOffset? Created { get; set; }

    /// <summary>The date and time of the most recent save.</summary>
    public DateTimeOffset? Modified { get; set; }

    /// <summary>The date and time the presentation was last printed.</summary>
    public DateTimeOffset? LastPrinted { get; set; }

    // ── Extended (app) properties ─────────────────────────────────────────────

    /// <summary>The name of the organisation that produced the presentation.</summary>
    public string? Company { get; set; }

    /// <summary>The name of the manager associated with the presentation.</summary>
    public string? Manager { get; set; }

    /// <summary>The name of the application that created the presentation.</summary>
    public string? ApplicationName { get; set; }

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
    /// Arbitrary name/value pairs stored in <c>docProps/custom.xml</c>.
    /// Values may be <see langword="string"/>, <see langword="int"/>,
    /// <see langword="double"/>, <see langword="bool"/>, or <see cref="DateTimeOffset"/>.
    /// </summary>
    public Dictionary<string, object?> CustomProperties { get; } = new(StringComparer.Ordinal);
}
