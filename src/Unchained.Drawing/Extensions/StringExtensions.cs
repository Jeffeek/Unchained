using System.Text;

namespace Unchained.Drawing.Extensions;

internal static class StringExtensions
{
    internal static ReadOnlySpan<byte> ToUtf8Span(this string source) => Encoding.UTF8.GetBytes(source);
}
