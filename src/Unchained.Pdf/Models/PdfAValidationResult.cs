namespace Unchained.Pdf.Models;

/// <summary>
/// Results of validating a PDF document against a PDF/A conformance profile.
/// </summary>
public sealed class PdfAValidationResult
{
    /// <summary>The profile that was checked.</summary>
    public PdfAProfile Profile { get; init; }

    /// <summary>
    /// <see langword="true"/> when no <see cref="PdfAViolationSeverity.Error"/> violations were found.
    /// Warning-only documents are still considered conformant.
    /// </summary>
    public bool IsConformant => Violations.All(static v => v.Severity != PdfAViolationSeverity.Error);

    /// <summary>All violations found, ordered by severity (errors first) then by rule ID.</summary>
    public IReadOnlyList<PdfAViolation> Violations { get; init; } = [];

    /// <summary>Errors (conformance-breaking violations).</summary>
    public IReadOnlyList<PdfAViolation> Errors =>
        Violations.Where(static v => v.Severity == PdfAViolationSeverity.Error).ToList();

    /// <summary>Warnings (best-practice issues).</summary>
    public IReadOnlyList<PdfAViolation> Warnings =>
        Violations.Where(static v => v.Severity == PdfAViolationSeverity.Warning).ToList();

    /// <inheritdoc />
    public override string ToString() =>
        IsConformant
            ? $"Conformant ({Profile}, {Warnings.Count} warning(s))"
            : $"Non-conformant ({Profile}, {Errors.Count} error(s), {Warnings.Count} warning(s))";
}
