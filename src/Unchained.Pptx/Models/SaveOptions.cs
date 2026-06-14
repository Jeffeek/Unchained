using Unchained.Pptx.Models.Export;

namespace Unchained.Pptx.Models;

/// <summary>
///     Options that control how a presentation is saved as a PPTX file.
/// </summary>
public sealed record SaveOptions
{
    /// <summary>A default <see cref="SaveOptions" /> instance with all settings at their defaults.</summary>
    public static readonly SaveOptions Default = new();

    /// <summary>
    ///     The OOXML conformance class to target.
    ///     Defaults to <see cref="PptxConformance.Transitional" /> for maximum compatibility.
    /// </summary>
    public PptxConformance Conformance { get; init; } = PptxConformance.Transitional;

    /// <summary>
    ///     Controls when ZIP64 extensions are written.
    ///     Defaults to <see cref="Zip64Policy.IfNecessary" />.
    /// </summary>
    public Zip64Policy Zip64 { get; init; } = Zip64Policy.IfNecessary;

    /// <summary>
    ///     When <see langword="true" />, the thumbnail image embedded in the package
    ///     (<c>docProps/thumbnail.*</c>) is regenerated before saving.
    ///     Defaults to <see langword="true" />.
    /// </summary>
    public bool RefreshThumbnail { get; init; } = true;

    /// <summary>
    ///     An optional password used to encrypt the output file.
    ///     When <see langword="null" /> (the default), the file is saved without encryption.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    ///     An optional progress callback that receives values in the range [0.0, 1.0]
    ///     as the save operation proceeds.
    /// </summary>
    public IProgress<double>? Progress { get; init; }

    /// <summary>
    ///     When <see langword="true" /> and the document was loaded through the OpenXML-SDK engine,
    ///     the save re-emits modelled content onto the held SDK package so unmodelled parts pass
    ///     through unchanged (Phase 2 in-place save). Ignored when the document has no held engine
    ///     (custom load path or <c>CreateBlank</c>), which falls back to the custom writer.
    ///     Defaults to <see langword="false" />.
    /// </summary>
    public bool UseOpenXmlEngine { get; init; }
}
