using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Unchained.Studio.Components.Xlsx;
using Unchained.Studio.Infrastructure;
using Unchained.Studio.Services;
using Unchained.Xlsx.Abstractions;
using Unchained.Xlsx.Cell;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Sheets;
using Unchained.Xlsx.Styles;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Studio.Features.Xlsx;

/// <summary>
///     View-model for the XLSX tab. Owns selection, formula bar, formatting, sheet management,
///     dialog launchers, and document operations. Raises <see cref="Changed" /> whenever observable
///     state changes so the tab can re-render.
/// </summary>
public sealed class XlsxEditorViewModel(SessionStateService session, IStudioDialogs dialogs, IUserFeedback feedback, Func<Worksheet?> sheetFn)
{
    // ── Selection ─────────────────────────────────────────────────────────────

    private CellRange _selection = new(new CellReference(1, 1), new CellReference(1, 1));

    public CellRange Selection => _selection;

    public CellReference FormulaReference { get; private set; } = new(1, 1);

    public string FormulaText { get; private set; } = string.Empty;

    public bool FormulaActive { get; private set; }

    public Cell? SelectedCell { get; private set; }

    // ── Document operations ───────────────────────────────────────────────────

    public string? ExtractedText { get; private set; }
    public bool ShowTextPanel { get; set; }

    /// <summary>Raised whenever observable VM state changes so the host tab can re-render.</summary>
    public event Action? Changed;

    private void NotifyChanged() => Changed?.Invoke();

    // ── Selection / Formula bar ───────────────────────────────────────────────

    internal void OnCellSelected(Cell? cell)
    {
        SelectedCell = cell;
        FormulaText = cell switch
        {
            null => string.Empty,
            { FormulaText: { } f } => f,
            _ => cell.CellType switch
            {
                CellType.Number => cell.GetDouble()?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                CellType.Boolean => cell.GetBoolean() == true ? "TRUE" : "FALSE",
                _ => cell.GetString() ?? cell.GetFormattedString()
            }
        };
        NotifyChanged();
    }

    internal void OnSelectionChanged(CellRange range)
    {
        _selection = range;
        FormulaReference = range.TopLeft;
        FormulaText = string.Empty;
        NotifyChanged();
    }

    internal void OnCellReferenceInserted((CellReference Reference, int CursorPos) tuple)
    {
        FormulaReference = tuple.Reference;
        FormulaText = tuple.Reference.ToAbsoluteA1();
    }

    internal void OnFormulaActiveChanged(bool active) => FormulaActive = active;

    internal void OnGridEdited()
    {
        var session1 = session.Xlsx;
        if (session1 is null) return;

        session1.Document.Recalculate();
        session1.MarkDirty();
        NotifyChanged();
    }

    internal void OnFormulaCommitted(string text)
    {
        var sheet = sheetFn();
        var session1 = session.Xlsx;
        if (sheet is null || session1 is null) return;

        var row = FormulaReference.Row;
        var col = FormulaReference.Column;

        if (string.IsNullOrEmpty(text))
            sheet.ClearCell(row, col);
        else if (text.StartsWith('='))
            sheet.SetFormula(row, col, text);
        else if (bool.TryParse(text, out var b))
            sheet.SetValue(row, col, b);
        else if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
            sheet.SetValue(row, col, d);
        else
            sheet.SetValue(row, col, text);

        session1.Document.Recalculate();
        session1.MarkDirty();
        var cell = sheet.GetCell(row, col);
        FormulaText = cell switch
        {
            null => string.Empty,
            { FormulaText: { } f } => f,
            _ => cell.CellType switch
            {
                CellType.Number => cell.GetDouble()?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                CellType.Boolean => cell.GetBoolean() == true ? "TRUE" : "FALSE",
                _ => cell.GetString() ?? cell.GetFormattedString()
            }
        };
        NotifyChanged();
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    internal void QuickFont(Action<CellFont> mutate) => ApplyToSelection(cell => cell.ApplyFont(mutate));
    internal void QuickAlign(Action<CellAlignment> mutate) => ApplyToSelection(cell => cell.ApplyAlignment(mutate));
    internal void QuickBorderAll() => ApplyToSelection(static cell => cell.ApplyBorder(static b => b.SetAllEdges(BorderStyle.Thin)));

    private void ApplyToSelection(Action<Cell> action)
    {
        var sheet = sheetFn();
        if (sheet is null) return;

        foreach (var reference in _selection.Cells())
            action(sheet[reference.Row, reference.Column]);

        session.Xlsx!.MarkDirty();
        NotifyChanged();
    }

    internal void MergeSelection()
    {
        var sheet = sheetFn();
        if (sheet is null) return;

        sheet.MergeCells(_selection);
        session.Xlsx!.MarkDirty();
        NotifyChanged();
    }

    internal void UnmergeSelection()
    {
        var sheet = sheetFn();
        if (sheet is null) return;

        sheet.UnmergeCells(_selection);
        session.Xlsx!.MarkDirty();
        NotifyChanged();
    }

    // ── Sheet management ──────────────────────────────────────────────────────

    internal void SelectSheet(int number)
    {
        var session1 = session.Xlsx;
        if (session1 is null) return;

        session1.CurrentSheet = number;
        _selection = new CellRange(new CellReference(1, 1), new CellReference(1, 1));
        NotifyChanged();
    }

    internal void AddSheet()
    {
        var session1 = session.Xlsx;
        if (session1 is null) return;

        var doc = session1.Document;
        doc.Sheets.Add(UniqueSheetName(doc));
        session1.CurrentSheet = doc.Sheets.Count;
        session1.MarkDirty();
        NotifyChanged();
    }

    internal async Task DeleteSheet(Worksheet sheet)
    {
        var session1 = session.Xlsx;
        if (session1 is null || session1.Document.Sheets.Count <= 1) return;

        var confirmed = await dialogs.ShowMessageBoxAsync(
            "Delete sheet",
            $"Delete '{sheet.Name}'? This cannot be undone.",
            "Delete",
            "Cancel"
        );
        if (confirmed != true) return;

        session1.Document.Sheets.Remove(sheet);
        session1.CurrentSheet = Math.Clamp(session1.CurrentSheet, 1, session1.Document.Sheets.Count);
        session1.MarkDirty();
        NotifyChanged();
    }

    internal async Task RenameSheet(Worksheet sheet)
    {
        var session1 = session.Xlsx;
        if (session1 is null) return;

        var newName = await dialogs.ShowAsync<RenameSheetDialog, string>(
            "Rename Sheet",
            p => p[nameof(RenameSheetDialog.CurrentName)] = sheet.Name,
            MaxWidth.ExtraSmall
        );
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            sheet.Name = newName;
            session1.MarkDirty();
        }
        catch (ArgumentException ex)
        {
            feedback.Error(ex.Message);
        }
    }

    internal void MoveSheet(int delta)
    {
        var sheet = sheetFn();
        var session1 = session.Xlsx;
        if (sheet is null || session1 is null) return;

        var current = session1.Document.Sheets.IndexOf(sheet);
        var target = Math.Clamp(current + delta, 0, session1.Document.Sheets.Count - 1);
        if (target == current) return;

        session1.Document.Sheets.MoveTo(sheet, target);
        session1.CurrentSheet = target + 1;
        session1.MarkDirty();
        NotifyChanged();
    }

    internal void ToggleHidden()
    {
        var sheet = sheetFn();
        var session1 = session.Xlsx;
        if (sheet is null || session1 is null) return;

        if (sheet.State == SheetState.Visible &&
            session1.Document.Sheets.Count(static s => s.State == SheetState.Visible) <= 1)
        {
            feedback.Error("A workbook must keep at least one visible sheet.");
            return;
        }

        sheet.State = sheet.State == SheetState.Visible ? SheetState.Hidden : SheetState.Visible;
        session1.MarkDirty();
        NotifyChanged();
    }

    private static string UniqueSheetName(ISpreadsheetDocument doc)
    {
        var n = doc.Sheets.Count + 1;
        string name;
        do
            name = $"Sheet{n++}";
        while (doc.Sheets.Find(name) is not null);

        return name;
    }

    // ── Dialog launchers ──────────────────────────────────────────────────────

    internal Task OpenFormatDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<CellFormatDialog>(
                "Format Cells",
                p =>
                {
                    p[nameof(CellFormatDialog.Sheet)] = sheet;
                    p[nameof(CellFormatDialog.Range)] = _selection;
                    p[nameof(CellFormatDialog.OnApplied)] = VoidCallback(MarkDirtyAndInvalidate);
                }
            );
    }

    internal Task OpenRowColumnDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<RowColumnDialog>(
                "Rows & Columns",
                p =>
                {
                    p[nameof(RowColumnDialog.Sheet)] = sheet;
                    p[nameof(RowColumnDialog.InitialRow)] = _selection.TopLeft.Row;
                    p[nameof(RowColumnDialog.InitialColumn)] = _selection.TopLeft.Column;
                    p[nameof(RowColumnDialog.OnChanged)] = VoidCallback(MarkDirty);
                }
            );
    }

    public Task OpenTablesDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<TablesDialog>(
                "Tables",
                p =>
                {
                    p[nameof(TablesDialog.Sheet)] = sheet;
                    p[nameof(TablesDialog.InitialRange)] = _selection.ToA1();
                    p[nameof(TablesDialog.OnChanged)] = VoidCallback(MarkDirty);
                }
            );
    }

    public Task OpenNamedRangesDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<NamedRangesDialog>(
                "Named Ranges",
                p =>
                {
                    p[nameof(NamedRangesDialog.Document)] = session.Xlsx!.Document;
                    p[nameof(NamedRangesDialog.CurrentSheet)] = sheet;
                    p[nameof(NamedRangesDialog.OnChanged)] = VoidCallback(MarkDirty);
                }
            );
    }

    public Task OpenDataValidationDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<DataValidationDialog>(
                "Data Validation",
                p =>
                {
                    p[nameof(DataValidationDialog.Sheet)] = sheet;
                    p[nameof(DataValidationDialog.InitialRange)] = _selection.ToA1();
                    p[nameof(DataValidationDialog.OnChanged)] = VoidCallback(MarkDirty);
                }
            );
    }

    public Task OpenInsertImage()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<InsertImageDialog>(
                "Insert Image",
                p =>
                {
                    p[nameof(InsertImageDialog.Sheet)] = sheet;
                    p[nameof(InsertImageDialog.InitialAnchor)] = _selection.TopLeft.ToA1();
                    p[nameof(InsertImageDialog.OnInserted)] = VoidCallback(MarkDirtyAndInvalidate);
                }
            );
    }

    public Task OpenInsertChart()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<InsertChartDialog>(
                "Insert Chart",
                p =>
                {
                    p[nameof(InsertChartDialog.Sheet)] = sheet;
                    p[nameof(InsertChartDialog.InitialRange)] = _selection.ToA1();
                    p[nameof(InsertChartDialog.OnInserted)] = VoidCallback(MarkDirtyAndInvalidate);
                }
            );
    }

    public Task OpenGenerateData()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<GenerateDataDialog>(
                "Generate Data",
                p =>
                {
                    p[nameof(GenerateDataDialog.Sheet)] = sheet;
                    p[nameof(GenerateDataDialog.Selection)] = _selection;
                    p[nameof(GenerateDataDialog.OnGenerated)] = VoidCallback(MarkDirtyAndInvalidate);
                }
            );
    }

    public Task OpenChartEditor(ChartDrawing chart)
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<ChartEditDialog>(
                "Edit Chart",
                p =>
                {
                    p[nameof(ChartEditDialog.Sheet)] = sheet;
                    p[nameof(ChartEditDialog.Chart)] = chart;
                    p[nameof(ChartEditDialog.OnChanged)] = VoidCallback(MarkDirtyAndInvalidate);
                }
            );
    }

    internal Task OpenSheetSettings()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<SheetSettingsDialog>(
                "Sheet Settings",
                p =>
                {
                    p[nameof(SheetSettingsDialog.Sheet)] = sheet;
                    p[nameof(SheetSettingsDialog.OnApplied)] = VoidCallback(MarkDirty);
                }
            );
    }

    internal Task OpenPageSetup()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<PageSetupDialog>(
                "Page Setup",
                p =>
                {
                    p[nameof(PageSetupDialog.Sheet)] = sheet;
                    p[nameof(PageSetupDialog.OnApplied)] = VoidCallback(MarkDirty);
                }
            );
    }

    internal Task OpenProtectionDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<ProtectionDialog>(
                "Protection",
                p =>
                {
                    p[nameof(ProtectionDialog.Document)] = session.Xlsx!.Document;
                    p[nameof(ProtectionDialog.Sheet)] = sheet;
                    p[nameof(ProtectionDialog.OnApplied)] = VoidCallback(MarkDirty);
                }
            );
    }

    internal Task OpenPasteImport()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<PasteImportDialog>(
                "Paste Import",
                p =>
                {
                    p[nameof(PasteImportDialog.Sheet)] = sheet;
                    p[nameof(PasteImportDialog.InitialTarget)] = _selection.TopLeft.ToA1();
                    p[nameof(PasteImportDialog.OnImported)] = VoidCallback(MarkDirtyAndInvalidate);
                }
            );
    }

    internal Task OpenMetadataDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<MetadataDialog>(
                "Metadata",
                p =>
                {
                    p[nameof(MetadataDialog.Document)] = session.Xlsx!.Document;
                    p[nameof(MetadataDialog.OnApplied)] = VoidCallback(MarkDirty);
                }
            );
    }

    internal Task OpenCsvExport()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<CsvExportDialog>(
                "Export CSV",
                p =>
                {
                    p[nameof(CsvExportDialog.Sheet)] = sheet;
                    p[nameof(CsvExportDialog.FileName)] = session.Xlsx!.FileName;
                }
            );
    }

    internal Task OpenSaveDialog()
    {
        var sheet = sheetFn();
        return sheet is null
            ? Task.CompletedTask
            : dialogs.ShowVoidAsync<SaveDialog>(
                "Download Workbook",
                p =>
                {
                    p[nameof(SaveDialog.Processor)] = session.Xlsx!.Processor;
                    p[nameof(SaveDialog.Document)] = session.Xlsx!.Document;
                    p[nameof(SaveDialog.FileName)] = session.Xlsx!.FileName;
                }
            );
    }

    internal void ExtractText()
    {
        var sheet = sheetFn();
        if (sheet is null) return;

        ExtractedText = sheet.GetAllText();
        ShowTextPanel = true;
    }

    internal void DownloadText(FileExportService exporter)
    {
        if (ExtractedText is null) return;

        var session1 = session.Xlsx;
        _ = exporter.TriggerDownloadAsync(
            Encoding.UTF8.GetBytes(ExtractedText),
            Path.ChangeExtension(session1?.FileName ?? "workbook", ".txt"),
            "text/plain"
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void MarkDirty() => session.Xlsx?.MarkDirty();

    private static EventCallback VoidCallback(Action action) => new(null, action);

    private void MarkDirtyAndInvalidate()
    {
        session.Xlsx?.MarkDirty();
        NotifyChanged();
    }
}
