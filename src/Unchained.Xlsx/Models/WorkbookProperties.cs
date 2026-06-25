namespace Unchained.Xlsx.Models;

/// <summary>
///     Metadata properties of a workbook, drawn from the OPC core properties
///     (<c>docProps/core.xml</c>) and the extended app properties (<c>docProps/app.xml</c>).
/// </summary>
public sealed class WorkbookProperties
{
    // ── Core (Dublin Core / OOXML core properties) ───────────────────────────

    /// <summary>The workbook title.</summary>
    public string? Title { get; set; }

    /// <summary>The subject of the workbook.</summary>
    public string? Subject { get; set; }

    /// <summary>The primary author (creator) of the workbook.</summary>
    public string? Author { get; set; }

    /// <summary>The user who last saved the workbook.</summary>
    public string? LastModifiedBy { get; set; }

    /// <summary>A comma-separated list of keywords associated with the workbook.</summary>
    public string? Keywords { get; set; }

    /// <summary>A description or abstract of the workbook.</summary>
    public string? Description { get; set; }

    /// <summary>The content category.</summary>
    public string? Category { get; set; }

    /// <summary>The content status (e.g. "Draft", "Final").</summary>
    public string? ContentStatus { get; set; }

    // ── Dates ─────────────────────────────────────────────────────────────────

    /// <summary>The date and time when the workbook was first created.</summary>
    public DateTimeOffset? Created { get; set; }

    /// <summary>The date and time of the most recent save.</summary>
    public DateTimeOffset? Modified { get; set; }

    // ── Extended (app) properties ─────────────────────────────────────────────

    /// <summary>The name of the organisation that produced the workbook.</summary>
    public string? Company { get; set; }

    /// <summary>The name of the manager associated with the workbook.</summary>
    public string? Manager { get; set; }

    /// <summary>The name of the application that created the workbook.</summary>
    public string? ApplicationName { get; set; }
}
