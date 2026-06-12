namespace Unchained.Pptx.Export;

/// <summary>Shared text-escaping helpers for the HTML-family export writers.</summary>
internal static class ExportText
{
    /// <summary>
    ///     Escapes the five XML/HTML special characters (&amp;, &lt;, &gt;, ", ')
    ///     so <paramref name="text" /> can be embedded in HTML markup or attribute values.
    /// </summary>
    internal static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&#39;");
}
