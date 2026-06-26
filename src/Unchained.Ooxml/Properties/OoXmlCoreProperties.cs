namespace Unchained.Ooxml.Properties;

/// <summary>
///     Shared metadata properties of an OOXML document, drawn from the OPC core properties
///     (<c>docProps/core.xml</c>) and the extended app properties (<c>docProps/app.xml</c>).
/// </summary>
public class OoXmlCoreProperties
{
    // ── Core (Dublin Core / OOXML core properties) ───────────────────────────

    /// <summary>The document title.</summary>
    public string? Title { get; set; }

    /// <summary>The subject of the document.</summary>
    public string? Subject { get; set; }

    /// <summary>The primary author (creator) of the document.</summary>
    public string? Author { get; set; }

    /// <summary>The user who last modified the document.</summary>
    public string? LastModifiedBy { get; set; }

    /// <summary>A comma-separated list of keywords associated with the document.</summary>
    public string? Keywords { get; set; }

    /// <summary>A description or abstract of the document.</summary>
    public string? Description { get; set; }

    /// <summary>The content category (e.g. "Sales", "Engineering").</summary>
    public string? Category { get; set; }

    /// <summary>The content status (e.g. "Draft", "Final").</summary>
    public string? ContentStatus { get; set; }

    // ── Dates ─────────────────────────────────────────────────────────────────

    /// <summary>The date and time when the document was first created.</summary>
    public DateTimeOffset? Created { get; set; }

    /// <summary>The date and time of the most recent save.</summary>
    public DateTimeOffset? Modified { get; set; }

    /// <summary>The date and time the document was last printed (Pptx-specific).</summary>
    public DateTimeOffset? LastPrinted { get; set; }

    // ── Extended (app) properties ─────────────────────────────────────────────

    /// <summary>The name of the organisation that produced the document.</summary>
    public string? Company { get; set; }

    /// <summary>The name of the manager associated with the document.</summary>
    public string? Manager { get; set; }

    /// <summary>The name of the application that created the document.</summary>
    public string? ApplicationName { get; set; }
}
