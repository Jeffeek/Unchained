namespace Unchained.Pdf.Core;

/// <summary>
///     Numeric coercion helpers for the PDF object model. Centralises the
///     <see cref="PdfInteger" /> / <see cref="PdfReal" /> unwrapping that otherwise recurs as
///     handwritten <c>switch</c> blocks throughout the parser, converters, and renderer.
/// </summary>
internal static class PdfNumericExtensions
{
    extension(PdfObject? obj)
    {
        /// <summary>Numeric value as a <see langword="double" />, or <paramref name="fallback" /> for non-numeric objects.</summary>
        internal double ToDouble(double fallback = 0) => obj switch
        {
            PdfInteger i => i.Value,
            PdfReal r => r.Value,
            _ => fallback
        };

        /// <summary>Numeric value as a <see langword="double" />, or <see langword="null" /> for non-numeric objects.</summary>
        internal double? ToDoubleOrNull() => obj switch
        {
            PdfInteger i => i.Value,
            PdfReal r => r.Value,
            _ => null
        };

        /// <summary>Numeric value truncated to an <see langword="int" />, or <paramref name="fallback" /> for non-numeric objects.</summary>
        internal int ToInt(int fallback = 0) => obj switch
        {
            PdfInteger i => (int)i.Value,
            PdfReal r => (int)r.Value,
            _ => fallback
        };
    }
}
