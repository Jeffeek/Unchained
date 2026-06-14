namespace Unchained.Pptx.Models.Export;

/// <summary>
///     Controls OOXML conformance class when saving a presentation.
/// </summary>
public enum PptxConformance
{
    /// <summary>
    ///     Transitional conformance (ECMA-376 §2.1.1).
    ///     Allows legacy constructs for maximum compatibility with older readers.
    ///     This is the default and the most widely supported option.
    /// </summary>
    Transitional,

    /// <summary>
    ///     Strict conformance (ECMA-376 §2.1.2).
    ///     Disallows legacy constructs and requires full OOXML compliance.
    ///     Use when targeting standards-compliant readers.
    /// </summary>
    Strict
}
