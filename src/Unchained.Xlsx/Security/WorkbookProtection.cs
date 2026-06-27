namespace Unchained.Xlsx.Security;

/// <summary>
///     Workbook-level protection: prevents structural changes (adding, removing, renaming, or
///     reordering sheets) and/or window changes. An optional password hash gates unprotection.
/// </summary>
public sealed class WorkbookProtection
{
    /// <summary>Whether the workbook structure is locked.</summary>
    public bool LockStructure { get; set; }

    /// <summary>Whether the workbook windows are locked.</summary>
    public bool LockWindows { get; set; }

    /// <summary>The legacy password hash, or <see langword="null" /> when no password is set.</summary>
    public string? PasswordHash { get; internal set; }

    /// <summary><see langword="true" /> when any protection is active.</summary>
    public bool IsProtected => LockStructure || LockWindows;

    /// <summary>Enables workbook protection.</summary>
    public void Protect(string? password = null, bool lockStructure = true, bool lockWindows = false)
    {
        LockStructure = lockStructure;
        LockWindows = lockWindows;
        PasswordHash = password == null ? null : LegacyPasswordHash.Compute(password);
    }

    /// <summary>Disables workbook protection.</summary>
    public void Unprotect()
    {
        LockStructure = false;
        LockWindows = false;
        PasswordHash = null;
    }
}
