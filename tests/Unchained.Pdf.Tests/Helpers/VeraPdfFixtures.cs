using Xunit;

namespace Unchained.Pdf.Tests.Helpers;

/// <summary>
/// Locates veraPDF test corpus files in <c>TestFiles/veraPDF/</c>.
/// Files follow the naming convention: <c>…-pass-….pdf</c> (spec-conforming)
/// and <c>…-fail-….pdf</c> (intentionally violating a spec rule).
/// </summary>
/// <remarks>
/// Source: https://github.com/veraPDF/veraPDF-corpus (CC BY 4.0)
/// Subdirectories:
///   ISO-32000-1/ — ISO 32000-1 (PDF 1.7) parser tests
///   ISO-32000-2/ — ISO 32000-2 (PDF 2.0) parser tests
///   TWG/          — Test Working Group files (PDF/A 1–3)
///   PDF-A-1b/     — PDF/A-1b conformance tests (file structure, graphics, fonts)
/// </remarks>
internal static class VeraPdfFixtures
{
    private static readonly string TestFilesDir =
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "veraPDF");

    /// <summary>
    /// Returns all <c>*.pdf</c> files under <c>TestFiles/veraPDF/</c>, recursively.
    /// Each element is a single-element <c>object[]</c> containing the full path.
    /// </summary>
    internal static IEnumerable<string> AllPdfFilePaths() =>
        Directory.Exists(TestFilesDir)
            ? Directory
                .GetFiles(TestFilesDir, "*.pdf", SearchOption.AllDirectories)
                .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            : [];

    /// <summary>
    /// Returns only "pass" files — PDFs that conform to the spec (should parse and have pages).
    /// </summary>
    internal static IEnumerable<string> PassPdfFilePaths() =>
        AllPdfFilePaths()
            .Where(static o =>
                Path.GetFileNameWithoutExtension(o).Contains("-pass-", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns only "fail" files — PDFs that intentionally violate a spec rule.
    /// These may or may not parse; both outcomes are acceptable for a non-validating parser.
    /// </summary>
    public static IEnumerable<string> FailPdfFilePaths() =>
        AllPdfFilePaths()
            .Where(static o => Path.GetFileNameWithoutExtension(o).Contains("-fail-", StringComparison.OrdinalIgnoreCase));
}
