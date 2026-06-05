namespace Unchained.Pptx.Models.Export;

/// <summary>
/// Controls when ZIP64 extensions are used when saving a presentation.
/// </summary>
public enum Zip64Policy
{
    /// <summary>
    /// Use ZIP64 extensions only when the file or entry size requires it.
    /// This is the default and produces the most compatible output.
    /// </summary>
    IfNecessary,

    /// <summary>Always write a standard ZIP archive, never using ZIP64 extensions.</summary>
    Never,

    /// <summary>Always write ZIP64 extensions regardless of file size.</summary>
    Always
}
