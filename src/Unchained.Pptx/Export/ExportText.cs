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

    /// <summary>
    ///     Builds an inline <c>data:</c> URI (base64-encoded) for embedding image bytes
    ///     directly in HTML <c>&lt;img&gt;</c> or SVG <c>&lt;image&gt;</c> markup.
    /// </summary>
    internal static string ToBase64DataUri(ReadOnlyMemory<byte> data, string contentType) =>
        $"data:{contentType};base64,{Convert.ToBase64String(data.ToArray())}";
}
