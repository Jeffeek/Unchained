namespace Unchained.Pdf.Models;

/// <summary>Severity of a PDF/A conformance violation.</summary>
public enum PdfAViolationSeverity
{
    /// <summary>The document does not conform to the specification. Archival viewers may reject it.</summary>
    Error,

    /// <summary>A best-practice issue that may affect interoperability but is not spec-mandated.</summary>
    Warning
}

/// <summary>
///     A single PDF/A rule violation found during conformance validation.
/// </summary>
/// <param name="RuleId">
///     ISO 19005 clause identifier, e.g. <c>6.3.3</c> for the font-embedding rule.
/// </param>
/// <param name="Description">Human-readable description of the violation.</param>
/// <param name="Severity">Whether this is a hard error or a warning.</param>
/// <param name="ObjectNumber">Indirect object number where the violation was found, if applicable.</param>
/// <param name="PageNumber">1-based page number where the violation was found, if applicable.</param>
public sealed record PdfAViolation(
    string RuleId,
    string Description,
    PdfAViolationSeverity Severity = PdfAViolationSeverity.Error,
    int? ObjectNumber = null,
    int? PageNumber = null
);
