using Unchained.Studio.Features.Xlsx;
using Unchained.Studio.Services;
using Unchained.Studio.Tests.Services;
using Unchained.Xlsx.Drawings;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models.Cell;
using Unchained.Xlsx.Models.Sheets;
using Unchained.Xlsx.Worksheets;

namespace Unchained.Studio.Tests.Features;

/// <summary>
///     Tests for <see cref="XlsxEditorViewModel" /> — selection, formula bar, formatting, sheet
///     management, and dialog launchers. Uses fake dialog/feedback services and a no-op renderer.
/// </summary>
public sealed class XlsxEditorViewModelTests
{
    private sealed record Harness(
        XlsxEditorViewModel Vm,
        SessionStateService Session,
        FakeStudioDialogs Dialogs,
        FakeUserFeedback Feedback
    );

    private static async Task<Harness> CreateAsync(int sheets = 1)
    {
        var session = new SessionStateService(
            new Pdf.Engine.DocumentProcessor(),
            new Pptx.Engine.PresentationProcessor(),
            new SpreadsheetProcessor(),
            new RenderingService(new FakePdfRenderer())
        );
        await session.LoadXlsxAsync(await WorkbookBytesAsync(sheets), "wb.xlsx", TestContext.Current.CancellationToken);

        var dialogs = new FakeStudioDialogs();
        var feedback = new FakeUserFeedback();
        var vm = new XlsxEditorViewModel(
            session,
            dialogs,
            feedback,
            () =>
            {
                var xlsx = session.Xlsx;
                return xlsx?.Document.Sheets[xlsx.CurrentSheet - 1];
            }
        );

        return new Harness(vm, session, dialogs, feedback);
    }

    private static async Task<byte[]> WorkbookBytesAsync(int sheets)
    {
        using var processor = new SpreadsheetProcessor();
        using var document = processor.CreateBlank("Sheet1");
        for (var i = 2; i <= sheets; i++)
            document.Sheets.Add($"Sheet{i}");
        using var ms = new MemoryStream();
        await processor.SaveAsync(document, ms, cancellationToken: TestContext.Current.CancellationToken);
        return ms.ToArray();
    }

    private static Worksheet ActiveSheet(SessionStateService session) =>
        session.Xlsx!.Document.Sheets[session.Xlsx.CurrentSheet - 1];

    private static CellRange Single(int row, int col) =>
        new(new CellReference(row, col), new CellReference(row, col));

    // ── Selection / formula bar ────────────────────────────────────────────

    [Fact]
    public async Task OnSelectionChanged_UpdatesSelectionAndRaisesChanged()
    {
        var h = await CreateAsync();
        var raised = 0;
        h.Vm.Changed += () => raised++;

        h.Vm.OnSelectionChanged(new CellRange(new CellReference(2, 3), new CellReference(4, 5)));

        h.Vm.Selection.ToA1().ShouldBe("C2:E4");
        h.Vm.FormulaReference.ToA1().ShouldBe("C2");
        h.Vm.FormulaText.ShouldBe(string.Empty);
        raised.ShouldBe(1);
    }

    [Fact]
    public async Task OnFormulaActiveChanged_SetsFlag()
    {
        var h = await CreateAsync();

        h.Vm.OnFormulaActiveChanged(true);

        h.Vm.FormulaActive.ShouldBeTrue();
    }

    [Fact]
    public async Task OnCellReferenceInserted_SetsReferenceAndAbsoluteA1()
    {
        var h = await CreateAsync();

        h.Vm.OnCellReferenceInserted((new CellReference(3, 2), 0));

        h.Vm.FormulaReference.ToA1().ShouldBe("B3");
        h.Vm.FormulaText.ShouldBe(new CellReference(3, 2).ToAbsoluteA1());
    }

    [Fact]
    public async Task OnCellSelected_PopulatesFormulaTextThenClearsOnNull()
    {
        var h = await CreateAsync();
        var sheet = ActiveSheet(h.Session);
        sheet[1, 1].SetValue("hello");

        h.Vm.OnCellSelected(sheet[1, 1]);
        h.Vm.SelectedCell.ShouldNotBeNull();
        h.Vm.FormulaText.ShouldBe("hello");

        h.Vm.OnCellSelected(null);
        h.Vm.FormulaText.ShouldBe(string.Empty);
        h.Vm.SelectedCell.ShouldBeNull();
    }

    [Fact]
    public async Task OnFormulaCommitted_NumberStringAndEmpty()
    {
        var h = await CreateAsync();
        var sheet = ActiveSheet(h.Session);
        h.Vm.OnSelectionChanged(Single(1, 1));

        h.Vm.OnFormulaCommitted("42");
        sheet.GetCell(1, 1)!.GetDouble().ShouldBe(42);
        h.Session.Xlsx!.IsDirty.ShouldBeTrue();

        h.Vm.OnFormulaCommitted("text");
        sheet.GetCell(1, 1)!.GetString().ShouldBe("text");

        h.Vm.OnFormulaCommitted(string.Empty);
        sheet.GetCell(1, 1).ShouldBeNull();
    }

    [Fact]
    public async Task OnGridEdited_MarksDirtyAndRaisesChanged()
    {
        var h = await CreateAsync();
        var raised = false;
        h.Vm.Changed += () => raised = true;

        h.Vm.OnGridEdited();

        h.Session.Xlsx!.IsDirty.ShouldBeTrue();
        raised.ShouldBeTrue();
    }

    // ── Formatting ─────────────────────────────────────────────────────────

    [Fact]
    public async Task QuickFont_AppliesToSelectionAndMarksDirty()
    {
        var h = await CreateAsync();
        h.Vm.OnSelectionChanged(Single(1, 1));

        h.Vm.QuickFont(static f => f.Bold = true);

        h.Session.Xlsx!.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task MergeThenUnmergeSelection_AdjustsMergedCells()
    {
        var h = await CreateAsync();
        var sheet = ActiveSheet(h.Session);
        h.Vm.OnSelectionChanged(new CellRange(new CellReference(1, 1), new CellReference(2, 2)));

        h.Vm.MergeSelection();
        sheet.MergedCells.Count.ShouldBe(1);

        h.Vm.UnmergeSelection();
        sheet.MergedCells.Count.ShouldBe(0);
    }

    // ── Sheet management ───────────────────────────────────────────────────

    [Fact]
    public async Task SelectSheet_SetsCurrentSheetAndResetsSelection()
    {
        var h = await CreateAsync(sheets: 2);
        h.Vm.OnSelectionChanged(Single(5, 5));

        h.Vm.SelectSheet(2);

        h.Session.Xlsx!.CurrentSheet.ShouldBe(2);
        h.Vm.Selection.ToA1().ShouldBe("A1:A1");
    }

    [Fact]
    public async Task AddSheet_AddsUniquelyNamedSheetAndSelectsIt()
    {
        var h = await CreateAsync(sheets: 2);
        var before = h.Session.Xlsx!.Document.Sheets.Count;

        h.Vm.AddSheet();

        h.Session.Xlsx.Document.Sheets.Count.ShouldBe(before + 1);
        h.Session.Xlsx.CurrentSheet.ShouldBe(before + 1);
        h.Session.Xlsx.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task MoveSheet_ReordersAndUpdatesCurrentSheet()
    {
        var h = await CreateAsync(sheets: 2);
        h.Session.Xlsx!.CurrentSheet = 1;
        var moved = ActiveSheet(h.Session);

        h.Vm.MoveSheet(1);

        h.Session.Xlsx.CurrentSheet.ShouldBe(2);
        h.Session.Xlsx.Document.Sheets.IndexOf(moved).ShouldBe(1);
    }

    [Fact]
    public async Task ToggleHidden_HidesVisibleSheet()
    {
        var h = await CreateAsync(sheets: 2);
        h.Session.Xlsx!.CurrentSheet = 1;

        h.Vm.ToggleHidden();

        h.Session.Xlsx.Document.Sheets[0].State.ShouldBe(SheetState.Hidden);
    }

    [Fact]
    public async Task ToggleHidden_RefusesToHideLastVisibleSheet()
    {
        var h = await CreateAsync(sheets: 1);

        h.Vm.ToggleHidden();

        h.Session.Xlsx!.Document.Sheets[0].State.ShouldBe(SheetState.Visible);
        h.Feedback.Errors.ShouldContain(static m => m.Contains("visible sheet"));
    }

    [Fact]
    public async Task DeleteSheet_RemovesWhenConfirmed()
    {
        var h = await CreateAsync(sheets: 2);
        h.Dialogs.NextMessageBoxResult = true;

        await h.Vm.DeleteSheet(h.Session.Xlsx!.Document.Sheets[1]);

        h.Session.Xlsx.Document.Sheets.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteSheet_KeepsWhenCancelled()
    {
        var h = await CreateAsync(sheets: 2);
        h.Dialogs.NextMessageBoxResult = false;

        await h.Vm.DeleteSheet(h.Session.Xlsx!.Document.Sheets[1]);

        h.Session.Xlsx.Document.Sheets.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteSheet_SkipsDialogWhenOnlyOneSheet()
    {
        var h = await CreateAsync(sheets: 1);

        await h.Vm.DeleteSheet(h.Session.Xlsx!.Document.Sheets[0]);

        h.Dialogs.MessageBoxCount.ShouldBe(0);
        h.Session.Xlsx.Document.Sheets.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RenameSheet_AppliesReturnedName()
    {
        var h = await CreateAsync();
        h.Dialogs.NextResult = "Renamed";

        await h.Vm.RenameSheet(h.Session.Xlsx!.Document.Sheets[0]);

        h.Session.Xlsx.Document.Sheets[0].Name.ShouldBe("Renamed");
    }

    [Fact]
    public async Task RenameSheet_IgnoresBlankName()
    {
        var h = await CreateAsync();
        h.Dialogs.NextResult = "   ";

        await h.Vm.RenameSheet(h.Session.Xlsx!.Document.Sheets[0]);

        h.Session.Xlsx.Document.Sheets[0].Name.ShouldBe("Sheet1");
    }

    // ── Text extraction ────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractText_CollectsSheetTextAndShowsPanel()
    {
        var h = await CreateAsync();
        ActiveSheet(h.Session)[1, 1].SetValue("hello world");

        h.Vm.ExtractText();

        h.Vm.ExtractedText!.ShouldContain("hello world");
        h.Vm.ShowTextPanel.ShouldBeTrue();
    }

    // ── Dialog launchers ───────────────────────────────────────────────────

    [Fact]
    public async Task DialogLaunchers_InvokeDialogServiceWithConfiguredParameters()
    {
        var h = await CreateAsync();

        await h.Vm.OpenFormatDialog();
        await h.Vm.OpenRowColumnDialog();
        await h.Vm.OpenTablesDialog();
        await h.Vm.OpenNamedRangesDialog();
        await h.Vm.OpenDataValidationDialog();
        await h.Vm.OpenInsertImage();
        await h.Vm.OpenInsertChart();
        await h.Vm.OpenGenerateData();
        await h.Vm.OpenChartEditor(new ChartDrawing());
        await h.Vm.OpenSheetSettings();
        await h.Vm.OpenPageSetup();
        await h.Vm.OpenProtectionDialog();
        await h.Vm.OpenPasteImport();
        await h.Vm.OpenMetadataDialog();
        await h.Vm.OpenCsvExport();
        await h.Vm.OpenSaveDialog();

        h.Dialogs.VoidTitles.ShouldContain("Format Cells");
        h.Dialogs.VoidTitles.ShouldContain("Tables");
        h.Dialogs.VoidTitles.ShouldContain("Insert Chart");
        h.Dialogs.VoidTitles.ShouldContain("Download Workbook");
        h.Dialogs.VoidTitles.Count.ShouldBe(16);
    }

    [Fact]
    public async Task DialogLaunchers_NoActiveSheet_DoNothing()
    {
        // A session with no loaded workbook → sheetFn returns null → launchers short-circuit.
        var session = new SessionStateService(
            new Pdf.Engine.DocumentProcessor(),
            new Pptx.Engine.PresentationProcessor(),
            new SpreadsheetProcessor(),
            new RenderingService(new FakePdfRenderer())
        );
        var dialogs = new FakeStudioDialogs();
        var vm = new XlsxEditorViewModel(session, dialogs, new FakeUserFeedback(), static () => null);

        await vm.OpenTablesDialog();

        dialogs.VoidTitles.ShouldBeEmpty();
    }
}
