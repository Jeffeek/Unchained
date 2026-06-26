namespace Unchained.Ooxml;

/// <summary>Controls OOXML conformance class when saving a package.</summary>
public enum OoXmlConformance
{
    /// <summary>
    ///     Transitional conformance (ECMA-376 1st edition compatible) — the broadest
    ///     compatibility with existing applications. This is the default.
    /// </summary>
    Transitional,

    /// <summary>Strict conformance (ISO/IEC 29500 Strict).</summary>
    Strict
}
