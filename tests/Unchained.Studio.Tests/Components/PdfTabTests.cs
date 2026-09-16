using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Unchained.Pdf.Engine;
using Unchained.Pptx.Engine;
using Unchained.Studio.Components.Pdf;
using Unchained.Studio.Components.Shared;
using Unchained.Studio.Infrastructure;
using Unchained.Studio.Services;
using Unchained.Studio.Tests.Services;
using Unchained.Xlsx.Engine;

namespace Unchained.Studio.Tests.Components;

/// <summary>Tests for the <see cref="PdfTab" /> component.</summary>
public sealed class PdfTabTests : MudTestContext
{
    private static SessionStateService CreateSession() =>
        new(
            new DocumentProcessor(),
            new PresentationProcessor(),
            new SpreadsheetProcessor(),
            new RenderingService(new FakePdfRenderer())
        );

    private void RegisterServices(SessionStateService session, RenderingService renderer)
    {
        Services.AddSingleton(session);
        Services.AddSingleton(renderer);
        Services.AddSingleton(new FileExportService(JSInterop.JSRuntime));
        Services.AddSingleton<IUserFeedback>(new FakeUserFeedback());
        Services.AddSingleton<IStudioDialogs>(new FakeStudioDialogs());
    }

    private static Task<byte[]> GetTestPdfBytes()
    {
        // Use a real test PDF from the shared test files
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Unchained.Pdf.Tests.Shared", "TestFiles", "arabic.pdf"
        );
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            // Fallback to any PDF in EmptyFiles
            fullPath = Path.Combine(AppContext.BaseDirectory, "EmptyFiles", "document", "empty.pdf");
        }

        return File.ReadAllBytesAsync(fullPath);
    }

    [Fact]
    public void Render_NoDocument_RendersEmptyState()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var cut = Render<PdfTab>();

        cut.Markup.ShouldNotBeNullOrWhiteSpace();
        cut.Markup.ShouldContain("Drop a PDF here");
    }

    [Fact]
    public void Render_NoDocument_ShowsFileDropZone()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var cut = Render<PdfTab>();

        cut.FindComponent<FileDropZone>().ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadPdf_ValidDocument_ShowsThreePanelLayout()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var pdfBytes = await GetTestPdfBytes();
        await session.LoadPdfAsync(pdfBytes, "test.pdf");

        // Render AFTER loading document
        var cut = Render<PdfTab>();

        cut.Markup.ShouldContain("three-panel");
    }

    [Fact]
    public async Task LoadPdf_ShowsDocumentTree()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var pdfBytes = await GetTestPdfBytes();
        await session.LoadPdfAsync(pdfBytes, "test.pdf");

        var cut = Render<PdfTab>();

        cut.FindComponent<DocumentTree>().ShouldNotBeNull();
    }

    [Fact]
    public async Task LoadPdf_ShowsOperationsBar()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var pdfBytes = await GetTestPdfBytes();
        await session.LoadPdfAsync(pdfBytes, "test.pdf");

        var cut = Render<PdfTab>();

        cut.Markup.ShouldContain("operations-bar");
        cut.Markup.ShouldContain("Merge");
        cut.Markup.ShouldContain("Split");
    }

    [Fact]
    public async Task LoadPdf_ShowsWatermarkButton()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var pdfBytes = await GetTestPdfBytes();
        await session.LoadPdfAsync(pdfBytes, "test.pdf");

        var cut = Render<PdfTab>();

        cut.Markup.ShouldContain("Watermark");
    }

    [Fact]
    public async Task LoadPdf_ShowsEncryptionButton()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var pdfBytes = await GetTestPdfBytes();
        await session.LoadPdfAsync(pdfBytes, "test.pdf");

        var cut = Render<PdfTab>();

        cut.Markup.ShouldContain("Encrypt");
    }

    [Fact]
    public async Task LoadPdf_DocumentLoaded_SessionContainsDocument()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var pdfBytes = await GetTestPdfBytes();
        await session.LoadPdfAsync(pdfBytes, "test.pdf");

        session.Pdf.ShouldNotBeNull();
        session.Pdf.Document.ShouldNotBeNull();
        session.Pdf.Document.PageCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task LoadPdf_ShowsPlayboardComponent()
    {
        var renderer = new RenderingService(new FakePdfRenderer());
        var session = CreateSession();
        RegisterServices(session, renderer);

        var pdfBytes = await GetTestPdfBytes();
        await session.LoadPdfAsync(pdfBytes, "test.pdf");

        var cut = Render<PdfTab>();

        // Playboard should be present
        cut.Markup.ShouldContain("panel-preview");
    }
}

