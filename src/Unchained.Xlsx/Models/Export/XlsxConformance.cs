namespace Unchained.Xlsx.Models.Export;

/// <summary>The OOXML conformance class used when saving a workbook.</summary>
public enum XlsxConformance
{
    /// <summary>
    ///     Transitional conformance (ECMA-376 1st edition compatible) — the broadest
    ///     compatibility with existing applications. This is the default.
    /// </summary>
    Transitional,

    /// <summary>Strict conformance (ISO/IEC 29500 Strict).</summary>
    Strict
}
