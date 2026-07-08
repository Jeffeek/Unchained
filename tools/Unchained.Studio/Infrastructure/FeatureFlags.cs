namespace Unchained.Studio.Infrastructure;

/// <summary>
///     Application-wide feature flags.
///     Set in <c>Program.cs</c> at startup.
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    ///     When <see langword="true" />, enables the Unchained vs Pdfium comparison features
    ///     (compare toggle in the preview panel, batch export dialog).
    ///     <para>
    ///         Default <see langword="false" /> — disabled until the developer
    ///         explicitly enables it.
    ///     </para>
    /// </summary>
    public static bool EnablePdfiumCompare { get; set; }
}
