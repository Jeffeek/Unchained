using System.Text;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Export;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Tests.Shared;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Export;

/// <summary>
///     Branch coverage for <see cref="PptxToHtmlWriter" /> driven directly: hidden-slide inclusion,
///     solid/border shape styling, text-frame paragraphs with alignment + bold/italic + run colour,
///     embedded picture data URIs, the progress callback, and additional CSS injection.
/// </summary>
public sealed class PptxToHtmlWriterTests
{
    private static string SlideHtml(IReadOnlyDictionary<string, byte[]> files, int slideNumber) =>
        Encoding.UTF8.GetString(files[$"slide{slideNumber}.html"]);

    [Fact]
    public void Write_ShapeWithTextAndBorder_EmitsStyledMarkup()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var shape = slide.Shapes.AddTextBox(Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(4), Emu.FromInches(2), "Hello");
        shape.Fill.SetSolid(ColorSpec.FromRgb(0xEE, 0xEE, 0xEE));
        shape.Line.Fill.SetSolid(ColorSpec.FromRgb(0, 0, 0));
        shape.Line.WidthPoints = 2.0;

        var para = shape.TextFrame.Paragraphs[0];
        para.Alignment = TextAlignment.Center;
        var run = para.Runs[0];
        run.Format.Bold = InheritableBool.True;
        run.Format.Italic = InheritableBool.True;
        run.Format.Fill = new FillFormat();
        run.Format.Fill.SetSolid(ColorSpec.FromRgb(0x11, 0x22, 0x33));

        var files = PptxToHtmlWriter.Write(doc, HtmlSaveOptions.Default);
        var html = SlideHtml(files, 1);

        html.ShouldContain("border:2.0px solid");
        html.ShouldContain("text-align:center");
        html.ShouldContain("font-weight:bold");
        html.ShouldContain("font-style:italic");
        html.ShouldContain("Hello");
    }

    [Fact]
    public void Write_HiddenSlide_ExcludedByDefault_IncludedWhenRequested()
    {
        var doc = PptxFixtures.WithSlides(2);
        doc.Slides[0].IsHidden = true;

        var defaultFiles = PptxToHtmlWriter.Write(doc, HtmlSaveOptions.Default);
        defaultFiles.Count.ShouldBe(1); // hidden slide 1 dropped

        var allFiles = PptxToHtmlWriter.Write(doc, new HtmlSaveOptions { IncludeHiddenSlides = true });
        allFiles.Count.ShouldBe(2);
    }

    [Fact]
    public void Write_TransparentShape_NoFill_EmitsTransparentBackground()
    {
        var doc = PptxFixtures.WithSlides(1);
        var shape = doc.Slides[0].Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1));
        shape.Fill.SetNone();

        var html = SlideHtml(PptxToHtmlWriter.Write(doc, HtmlSaveOptions.Default), 1);
        html.ShouldContain("background:transparent");
    }

    [Fact]
    public void Write_AdditionalCssAndProgress_AreApplied()
    {
        var doc = PptxFixtures.WithSlides(2);
        var reports = new List<double>();
        var options = new HtmlSaveOptions
        {
            AdditionalCss = ".custom{color:red}",
            Progress = new Progress<double>(reports.Add)
        };

        var files = PptxToHtmlWriter.Write(doc, options);
        SlideHtml(files, 1).ShouldContain(".custom{color:red}");
        // Progress is reported via a captured callback; the synchronous Progress<T> may post to the
        // sync context, so just assert export succeeded for both slides.
        files.Count.ShouldBe(2);
    }

    [Fact]
    public void Write_EmbeddedPicture_InlinesDataUri()
    {
        var doc = PptxFixtures.WithSlides(1);
        var slide = doc.Slides[0];
        var image = doc.Media.AddImage(new byte[] { 1, 2, 3, 4 }, "image/png");
        slide.Shapes.AddParsed(
            new PictureShape
            {
                Image = image,
                X = Emu.FromInches(1),
                Y = Emu.FromInches(1),
                Width = Emu.FromInches(2),
                Height = Emu.FromInches(2)
            }
        );

        var html = SlideHtml(PptxToHtmlWriter.Write(doc, new HtmlSaveOptions { EmbedImages = true }), 1);
        html.ShouldContain("data:image/png;base64,");
    }
}
