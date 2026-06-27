using System.Globalization;

namespace Unchained.Xlsx.Security;

/// <summary>
///     Computes the legacy 16-bit SpreadsheetML password hash used by <c>sheetProtection</c> and
///     <c>workbookProtection</c> (ECMA-376 §18.2.29 / §18.3.1.85). This is an obfuscation hash, not
///     encryption — it only gates unprotection through the application UI.
/// </summary>
internal static class LegacyPasswordHash
{
    /// <summary>Returns the 4-digit uppercase hex hash of <paramref name="password" />.</summary>
    public static string Compute(string password)
    {
        // Algorithm from the OOXML spec: rotate-and-xor over the character codes.
        var hash = 0;
        for (var i = password.Length - 1; i >= 0; i--)
        {
            hash = ((hash >> 14) & 0x01) | ((hash << 1) & 0x7FFF);
            hash ^= password[i];
        }

        hash = ((hash >> 14) & 0x01) | ((hash << 1) & 0x7FFF);
        hash ^= password.Length;
        hash ^= 0xCE4B;

        return hash.ToString("X4", CultureInfo.InvariantCulture);
    }
}
