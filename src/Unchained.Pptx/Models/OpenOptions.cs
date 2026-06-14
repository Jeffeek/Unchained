namespace Unchained.Pptx.Models;

/// <summary>
///     Options that control how a presentation file is opened.
/// </summary>
public sealed class OpenOptions
{
    /// <summary>
    ///     The password required to open a password-protected presentation.
    ///     Leave <see langword="null" /> for unprotected files.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    ///     When <see langword="true" />, structural warnings encountered during loading
    ///     (such as unrecognised part types) are silently ignored instead of surfaced
    ///     via <see cref="WarningCallback" />. Defaults to <see langword="false" />.
    /// </summary>
    public bool IgnoreLoadWarnings { get; init; }

    /// <summary>
    ///     An optional callback that receives non-fatal warnings encountered while loading.
    ///     Ignored when <see cref="IgnoreLoadWarnings" /> is <see langword="true" />.
    /// </summary>
    public Action<string>? WarningCallback { get; init; }

    /// <summary>
    ///     When <see langword="true" />, the presentation is read through the OpenXML-SDK-backed
    ///     engine (<c>Unchained.Ooxml.Engine</c>) instead of the legacy custom parser. This is the
    ///     Phase 2 migration path; it runs in parallel with the custom parser until it reaches full
    ///     parity. Defaults to <see langword="false" /> (custom parser).
    /// </summary>
    public bool UseOpenXmlEngine { get; init; }
}
