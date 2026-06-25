namespace Unchained.Xlsx.Models;

/// <summary>Settings that control how a spreadsheet is loaded.</summary>
public sealed class OpenOptions
{
    /// <summary>The password used to decrypt a password-protected workbook.</summary>
    public string? Password { get; init; }

    /// <summary>
    ///     When <see langword="true" />, recoverable parse warnings do not throw and the
    ///     workbook is loaded on a best-effort basis.
    /// </summary>
    public bool IgnoreLoadWarnings { get; init; }
}
