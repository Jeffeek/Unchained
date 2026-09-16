using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Unchained.Pdf.Engine;
using Unchained.Pptx.Engine;
using Unchained.Studio.Components.Shared;
using Unchained.Studio.Components.Xlsx;
using Unchained.Studio.Features.Xlsx;
using Unchained.Studio.Infrastructure;
using Unchained.Studio.Services;
using Unchained.Studio.Tests.Services;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Worksheets;
using TestContext = Xunit.TestContext;

namespace Unchained.Studio.Tests.Components;

/// <summary>Tests for the <see cref="XlsxTab" /> component.</summary>
public sealed class XlsxTabTests : MudTestContext
{
    private static SessionStateService CreateSession() =>
        new(
            new DocumentProcessor(),
            new PresentationProcessor(),
            new SpreadsheetProcessor(),
            new RenderingService(new FakePdfRenderer())
        );

    private void RegisterServices(SessionStateService session)
    {
        Services.AddSingleton(session);
        Services.AddSingleton(new FileExportService(JSInterop.JSRuntime));
        Services.AddSingleton<IUserFeedback>(new FakeUserFeedback());
        Services.AddSingleton<IStudioDialogs>(new FakeStudioDialogs());

        // XlsxEditorViewModel needs Func<Worksheet?> - return the current sheet
        Services.AddSingleton<Func<Worksheet?>>(() =>
        {
            var xlsx = session.Xlsx;
            if (xlsx?.Document == null)
                return null;

            var index = xlsx.CurrentSheet - 1; // CurrentSheet is 1-based
            return index >= 0 && index < xlsx.Document.Sheets.Count
                ? xlsx.Document.Sheets[index]
                : null;
        });
        Services.AddSingleton<XlsxEditorViewModel>();
    }

    [Fact]
    public void Render_NoDocument_RendersEmptyState()
    {
        var session = CreateSession();
        RegisterServices(session);

        var cut = Render<XlsxTab>();

        cut.Markup.ShouldNotBeNullOrWhiteSpace();
        cut.Markup.ShouldContain("Drop a spreadsheet here");
    }

    [Fact]
    public void Render_NoDocument_ShowsFileDropZone()
    {
        var session = CreateSession();
        RegisterServices(session);

        var cut = Render<XlsxTab>();

        cut.FindComponent<FileDropZone>().ShouldNotBeNull();
    }

    [Fact]
    public void Render_NoDocument_ShowsNewWorkbookButton()
    {
        var session = CreateSession();
        RegisterServices(session);

        var cut = Render<XlsxTab>();

        cut.Markup.ShouldContain("New Workbook");
    }

    [Fact]
    public async Task LoadXlsx_ValidDocument_ShowsThreePanelLayout()
    {
        var session = CreateSession();
        RegisterServices(session);

        var processor = new SpreadsheetProcessor();
        var workbook = processor.CreateBlank("Sheet1");
        using var ms = new MemoryStream();
        await processor.SaveAsync(workbook, ms, cancellationToken: TestContext.Current.CancellationToken);

        await session.LoadXlsxAsync(ms.ToArray(), "test.xlsx", TestContext.Current.CancellationToken);

        var cut = Render<XlsxTab>();

        cut.Markup.ShouldContain("three-panel");
    }

    [Fact]
    public async Task LoadXlsx_ShowsDocumentTree()
    {
        var session = CreateSession();
        RegisterServices(session);

        var processor = new SpreadsheetProcessor();
        var workbook = processor.CreateBlank("Sheet1");
        using var ms = new MemoryStream();
        await processor.SaveAsync(workbook, ms, cancellationToken: TestContext.Current.CancellationToken);

        await session.LoadXlsxAsync(ms.ToArray(), "test.xlsx", TestContext.Current.CancellationToken);

        var cut = Render<XlsxTab>();

        cut.FindComponent<DocumentTree>().ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadXlsx_ShowsSheetGrid()
    {
        var session = CreateSession();
        RegisterServices(session);

        var processor = new SpreadsheetProcessor();
        var workbook = processor.CreateBlank("Sheet1");
        using var ms = new MemoryStream();
        await processor.SaveAsync(workbook, ms, cancellationToken: TestContext.Current.CancellationToken);

        await session.LoadXlsxAsync(ms.ToArray(), "test.xlsx", TestContext.Current.CancellationToken);

        var cut = Render<XlsxTab>();

        cut.FindComponent<SheetGrid>().ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadXlsx_ShowsSheetTabs()
    {
        var session = CreateSession();
        RegisterServices(session);

        var processor = new SpreadsheetProcessor();
        var workbook = processor.CreateBlank("MySheet");
        using var ms = new MemoryStream();
        await processor.SaveAsync(workbook, ms, cancellationToken: TestContext.Current.CancellationToken);

        await session.LoadXlsxAsync(ms.ToArray(), "test.xlsx", TestContext.Current.CancellationToken);

        var cut = Render<XlsxTab>();

        cut.Markup.ShouldContain("MySheet");
    }

    [Fact]
    public async Task LoadXlsx_ShowsFormatToolbar()
    {
        var session = CreateSession();
        RegisterServices(session);

        var processor = new SpreadsheetProcessor();
        var workbook = processor.CreateBlank("Sheet1");
        using var ms = new MemoryStream();
        await processor.SaveAsync(workbook, ms, cancellationToken: TestContext.Current.CancellationToken);

        await session.LoadXlsxAsync(ms.ToArray(), "test.xlsx", TestContext.Current.CancellationToken);

        var cut = Render<XlsxTab>();

        cut.Markup.ShouldContain("format-toolbar");
    }

    [Fact]
    public async Task LoadXlsx_MultiSheetWorkbook_ShowsAllSheets()
    {
        var session = CreateSession();
        RegisterServices(session);

        var processor = new SpreadsheetProcessor();
        var workbook = processor.CreateBlank("Sheet1");
        workbook.Sheets.Add("Sheet2");
        workbook.Sheets.Add("Sheet3");

        using var ms = new MemoryStream();
        await processor.SaveAsync(workbook, ms, cancellationToken: TestContext.Current.CancellationToken);

        await session.LoadXlsxAsync(ms.ToArray(), "test.xlsx", TestContext.Current.CancellationToken);

        var cut = Render<XlsxTab>();

        cut.Markup.ShouldContain("Sheet1");
        cut.Markup.ShouldContain("Sheet2");
        cut.Markup.ShouldContain("Sheet3");
    }

    [Fact]
    public async Task LoadXlsx_ShowsSheetControls()
    {
        var session = CreateSession();
        RegisterServices(session);

        var processor = new SpreadsheetProcessor();
        var workbook = processor.CreateBlank("Sheet1");
        using var ms = new MemoryStream();
        await processor.SaveAsync(workbook, ms, cancellationToken: TestContext.Current.CancellationToken);

        await session.LoadXlsxAsync(ms.ToArray(), "test.xlsx", TestContext.Current.CancellationToken);

        var cut = Render<XlsxTab>();

        // Should have sheet tabs area with controls
        cut.Markup.ShouldContain("Sheet1");
        var buttons = cut.FindAll("button");
        buttons.Count.ShouldBeGreaterThan(0);
    }
}
