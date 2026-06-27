using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.PageSetup;
using Unchained.Xlsx.Security;

namespace Unchained.Xlsx.Worksheets;

public sealed partial class Worksheet
{
    private bool _layoutParsed;
    private PageSetup.PageSetup? _pageSetup;
    private PageMargins? _pageMargins;
    private HeaderFooterSetup? _headerFooter;
    private SheetView? _view;
    private SheetProtection? _protection;
    private CellRange? _autoFilter;

    /// <summary>The print page setup for this worksheet.</summary>
    public PageSetup.PageSetup PageSetup
    {
        get { EnsureLayoutParsed(); return _pageSetup ??= new PageSetup.PageSetup(); }
    }

    /// <summary>The print margins for this worksheet.</summary>
    public PageMargins PageMargins
    {
        get { EnsureLayoutParsed(); return _pageMargins ??= new PageMargins(); }
    }

    /// <summary>The header/footer settings for this worksheet.</summary>
    public HeaderFooterSetup HeaderFooter
    {
        get { EnsureLayoutParsed(); return _headerFooter ??= new HeaderFooterSetup(); }
    }

    /// <summary>The view settings (grid lines, zoom, frozen panes) for this worksheet.</summary>
    public SheetView View
    {
        get { EnsureLayoutParsed(); return _view ??= new SheetView(); }
    }

    /// <summary>The sheet-level protection settings.</summary>
    public SheetProtection Protection
    {
        get { EnsureLayoutParsed(); return _protection ??= new SheetProtection(); }
    }

    /// <summary>The auto-filter range, or <see langword="null" /> when none is set.</summary>
    public CellRange? AutoFilter
    {
        get { EnsureLayoutParsed(); return _autoFilter; }
        set { EnsureLayoutParsed(); _autoFilter = value; }
    }

    /// <summary>Sets the auto-filter range.</summary>
    public void SetAutoFilter(CellRange range) => AutoFilter = range;

    /// <summary>Removes the auto-filter.</summary>
    public void RemoveAutoFilter() => AutoFilter = null;

    /// <summary>Freezes the given number of leading rows and columns.</summary>
    public void FreezePanes(int rows, int columns) =>
        View.FrozenPanes = new FrozenPanes(rows, columns);

    /// <summary>
    ///     <see langword="true" /> once any layout aspect was accessed. The writer then regenerates
    ///     the page-setup / view / protection / auto-filter elements from the model.
    /// </summary>
    internal bool LayoutMaterialised => _layoutParsed;

    internal PageSetup.PageSetup? PageSetupOrNull => _pageSetup;
    internal PageMargins? PageMarginsOrNull => _pageMargins;
    internal HeaderFooterSetup? HeaderFooterOrNull => _headerFooter;
    internal SheetView? ViewOrNull => _view;
    internal SheetProtection? ProtectionOrNull => _protection;
    internal CellRange? AutoFilterOrNull => _autoFilter;

    private void EnsureLayoutParsed()
    {
        if (_layoutParsed)
            return;

        _layoutParsed = true;
        if (RawElement == null)
            return;

        Parsing.WorksheetLayoutParser.Parse(this, RawElement);
    }

    // ── Loader setters (used by WorksheetLayoutParser) ──────────────────────────

    internal void SetParsedPageSetup(PageSetup.PageSetup value) => _pageSetup = value;
    internal void SetParsedPageMargins(PageMargins value) => _pageMargins = value;
    internal void SetParsedHeaderFooter(HeaderFooterSetup value) => _headerFooter = value;
    internal void SetParsedView(SheetView value) => _view = value;
    internal void SetParsedProtection(SheetProtection value) => _protection = value;
    internal void SetParsedAutoFilter(CellRange value) => _autoFilter = value;
}
