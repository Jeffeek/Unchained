using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class HtmlExportTests : PptxTestBase
{
    private static PresentationProcessor Processor() => new();

    // ── Output structure ──────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsHtml_OneSlide_CreatesOneFile()
    {
        var doc = PptxFixtures.WithSlides(1);
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            files.Count.ShouldBe(1);
            File.Exists(files[0]).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_ThreeSlides_CreatesThreeFiles()
    {
        var doc = PptxFixtures.WithSlides(3);
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            files.Count.ShouldBe(3);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_CreatesDirectoryIfNotExists()
    {
        var doc = PptxFixtures.WithSlides(1);
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "output");
        try
        {
            await Processor().SaveAsHtmlAsync(doc, dir);
            Directory.Exists(dir).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    // ── HTML validity ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsHtml_OutputContainsDoctypeHtml()
    {
        var doc = PptxFixtures.WithSlides(1);
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            var html = await File.ReadAllTextAsync(files[0]);
            html.ShouldContain("<!DOCTYPE html>");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_OutputContainsSlideDiv()
    {
        var doc = PptxFixtures.WithSlides(1);
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            var html = await File.ReadAllTextAsync(files[0]);
            html.ShouldContain("class=\"slide\"");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_WithText_TextAppearsInOutput()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0].Shapes.AddTextBox(
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(4),
            Emu.FromInches(2),
            "Hello HTML World");

        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            var html = await File.ReadAllTextAsync(files[0]);
            html.ShouldContain("Hello HTML World");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_HiddenSlide_ExcludedByDefault()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[1].IsHidden = true;

        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            files.Count.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_HiddenSlide_IncludedWhenRequested()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[1].IsHidden = true;

        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc,
                dir,
                new HtmlSaveOptions { IncludeHiddenSlides = true });
            files.Count.ShouldBe(2);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_ShapeWithSolidFill_ContainsCssColor()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddShape(AutoShapeType.Rectangle,
            Emu.FromInches(1),
            Emu.FromInches(1),
            Emu.FromInches(3),
            Emu.FromInches(2));
        shape.Fill.SetSolid(ColorSpec.FromRgb(0, 112, 192));

        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            var html = await File.ReadAllTextAsync(files[0]);
            html.ShouldContain("background:rgb(0,112,192)");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SaveAsHtml_EmptyPresentation_CreatesZeroFiles()
    {
        var doc = Processor().CreateBlank();
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var files = await Processor().SaveAsHtmlAsync(doc, dir);
            files.Count.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
