using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.PageSetup;
using Unchained.Xlsx.Parsing;
using Unchained.Xlsx.Security;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    /// <summary>The print page setup for this worksheet.</summary>
    public PageSetup.PageSetup PageSetup
    {
        get
        {
            EnsureLayoutParsed();
            return PageSetupOrNull ??= new PageSetup.PageSetup();
        }
    }

    /// <summary>The print margins for this worksheet.</summary>
    public PageMargins PageMargins
    {
        get
        {
            EnsureLayoutParsed();
            return PageMarginsOrNull ??= new PageMargins();
        }
    }

    /// <summary>The header/footer settings for this worksheet.</summary>
    public HeaderFooterSetup HeaderFooter
    {
        get
        {
            EnsureLayoutParsed();
            return HeaderFooterOrNull ??= new HeaderFooterSetup();
        }
    }

    /// <summary>The view settings (grid lines, zoom, frozen panes) for this worksheet.</summary>
    public SheetView View
    {
        get
        {
            EnsureLayoutParsed();
            return ViewOrNull ??= new SheetView();
        }
    }

    /// <summary>The sheet-level protection settings.</summary>
    public SheetProtection Protection
    {
        get
        {
            EnsureLayoutParsed();
            return ProtectionOrNull ??= new SheetProtection();
        }
    }

    /// <summary>The auto-filter range, or <see langword="null" /> when none is set.</summary>
    public CellRange? AutoFilter
    {
        get
        {
            EnsureLayoutParsed();
            return AutoFilterOrNull;
        }
        set
        {
            EnsureLayoutParsed();
            AutoFilterOrNull = value;
        }
    }

    /// <summary>
    ///     <see langword="true" /> once any layout aspect was accessed. The writer then regenerates
    ///     the page-setup / view / protection / auto-filter elements from the model.
    /// </summary>
    internal bool LayoutMaterialised { get; private set; }

    internal PageSetup.PageSetup? PageSetupOrNull { get; private set; }

    internal PageMargins? PageMarginsOrNull { get; private set; }

    internal HeaderFooterSetup? HeaderFooterOrNull { get; private set; }

    internal SheetView? ViewOrNull { get; private set; }

    internal SheetProtection? ProtectionOrNull { get; private set; }

    internal CellRange? AutoFilterOrNull { get; private set; }

    /// <summary>Sets the auto-filter range.</summary>
    public void SetAutoFilter(CellRange range) => AutoFilter = range;

    /// <summary>Removes the auto-filter.</summary>
    public void RemoveAutoFilter() => AutoFilter = null;

    /// <summary>Freezes the given number of leading rows and columns.</summary>
    public void FreezePanes(int rows, int columns) =>
        View.FrozenPanes = new FrozenPanes(rows, columns);

    private void EnsureLayoutParsed()
    {
        if (LayoutMaterialised)
            return;

        LayoutMaterialised = true;
        if (RawElement == null)
            return;

        WorksheetLayoutParser.Parse(this, RawElement);
    }

    // ── Loader setters (used by WorksheetLayoutParser) ──────────────────────────

    internal void SetParsedPageSetup(PageSetup.PageSetup value) => PageSetupOrNull = value;
    internal void SetParsedPageMargins(PageMargins value) => PageMarginsOrNull = value;
    internal void SetParsedHeaderFooter(HeaderFooterSetup value) => HeaderFooterOrNull = value;
    internal void SetParsedView(SheetView value) => ViewOrNull = value;
    internal void SetParsedProtection(SheetProtection value) => ProtectionOrNull = value;
    internal void SetParsedAutoFilter(CellRange value) => AutoFilterOrNull = value;
}
