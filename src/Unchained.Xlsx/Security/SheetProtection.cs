namespace Unchained.Xlsx.Security;

/// <summary>
///     Sheet-level protection settings. When enabled, the application restricts editing per the
///     permission flags. An optional password hash gates unprotection in the UI; it is not encryption.
/// </summary>
public sealed class SheetProtection
{
    /// <summary>Whether the sheet is protected.</summary>
    public bool IsProtected { get; set; }

    /// <summary>The legacy 16-bit password hash, or <see langword="null" /> when no password is set.</summary>
    public string? PasswordHash { get; internal set; }

    /// <summary>Whether selecting locked cells is permitted.</summary>
    public bool AllowSelectLockedCells { get; set; } = true;

    /// <summary>Whether selecting unlocked cells is permitted.</summary>
    public bool AllowSelectUnlockedCells { get; set; } = true;

    /// <summary>Whether formatting cells is permitted.</summary>
    public bool AllowFormatCells { get; set; }

    /// <summary>Whether inserting rows is permitted.</summary>
    public bool AllowInsertRows { get; set; }

    /// <summary>Whether inserting columns is permitted.</summary>
    public bool AllowInsertColumns { get; set; }

    /// <summary>Whether deleting rows is permitted.</summary>
    public bool AllowDeleteRows { get; set; }

    /// <summary>Whether deleting columns is permitted.</summary>
    public bool AllowDeleteColumns { get; set; }

    /// <summary>Whether sorting is permitted.</summary>
    public bool AllowSort { get; set; }

    /// <summary>Whether using auto-filter is permitted.</summary>
    public bool AllowAutoFilter { get; set; }

    /// <summary>Enables protection, optionally storing a password hash.</summary>
    public void Protect(string? password = null)
    {
        IsProtected = true;
        PasswordHash = password == null ? null : LegacyPasswordHash.Compute(password);
    }

    /// <summary>Disables protection and clears any password hash.</summary>
    public void Unprotect()
    {
        IsProtected = false;
        PasswordHash = null;
    }
}
