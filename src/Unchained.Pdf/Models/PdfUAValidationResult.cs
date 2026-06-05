namespace Unchained.Pdf.Models;

/// <summary>Severity of a PDF/UA violation.</summary>
// ReSharper disable once InconsistentNaming
public enum PdfUAViolationSeverity
{
    /// <summary>The document cannot be considered PDF/UA-conformant.</summary>
    Error,
    /// <summary>The document may fail accessibility requirements in some viewers.</summary>
    Warning
}

/// <summary>A single rule violation found during PDF/UA validation.</summary>
/// <param name="RuleId">ISO 14289-1 clause identifier, e.g. <c>"7.1"</c>.</param>
/// <param name="Description">Human-readable description of the violation.</param>
/// <param name="Severity">Whether this is a hard error or a warning.</param>
/// <param name="ObjectNumber">Indirect object number where the violation was found, if applicable.</param>
/// <param name="PageNumber">1-based page number where the violation was found, if applicable.</param>
// ReSharper disable once InconsistentNaming
public sealed record PdfUAViolation(
    string RuleId,
    string Description,
    PdfUAViolationSeverity Severity,
    int? ObjectNumber = null,
    int? PageNumber = null
);

/// <summary>
/// Result returned by <see cref="Unchained.Pdf.Abstractions.IDocumentProcessor.ValidatePdfUAAsync"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class PdfUAValidationResult
{
    /// <summary>
    /// All violations found. Empty means the document passes all statically-verifiable
    /// PDF/UA-1 rules checked by this validator.
    /// </summary>
    public IReadOnlyList<PdfUAViolation> Violations { get; init; } = [];

    /// <summary><see langword="true"/> when no error-severity violations were found.</summary>
    public bool IsConformant => Violations.All(static v => v.Severity != PdfUAViolationSeverity.Error);
}
