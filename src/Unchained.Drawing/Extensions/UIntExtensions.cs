namespace Unchained.Drawing.Extensions;

internal static class UIntExtensions
{
    internal static void ExtractArgb(
        this uint argb,
        out byte r,
        out byte g,
        out byte b,
        out byte a
    )
    {
        const byte shift = 0xFF;

        a = (byte)((argb >> 24) & shift);
        r = (byte)((argb >> 16) & shift);
        g = (byte)((argb >> 8) & shift);
        b = (byte)(argb & shift);
    }
}
