using Unchained.Xlsx.DefinedNames;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Security;
using Unchained.Xlsx.Styles;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Xlsx.Abstractions;

/// <summary>
///     The public contract for an in-memory workbook. Implemented by <see cref="SpreadsheetDocument" />.
/// </summary>
public interface ISpreadsheetDocument : IDisposable
{
    /// <summary>The worksheets in this workbook, in tab order.</summary>
    WorksheetCollection Sheets { get; }

    /// <summary>The workbook metadata.</summary>
    WorkbookProperties Properties { get; }

    /// <summary>The workbook style registry.</summary>
    StyleBook Styles { get; }

    /// <summary>The workbook's defined names (named ranges).</summary>
    DefinedNameCollection DefinedNames { get; }

    /// <summary>The workbook-level protection settings.</summary>
    WorkbookProtection Protection { get; }

    /// <summary>Whether the workbook uses the 1904 date system.</summary>
    bool Date1904 { get; }

    /// <summary>Whether the workbook was loaded from an encrypted file.</summary>
    bool WasLoadedEncrypted { get; }

    /// <summary>Marks all formulas for recalculation on next open.</summary>
    void RecalculateAll();

    /// <summary>Clears the in-memory encryption flag so the next save writes a decrypted package.</summary>
    void RemoveEncryption();
}
