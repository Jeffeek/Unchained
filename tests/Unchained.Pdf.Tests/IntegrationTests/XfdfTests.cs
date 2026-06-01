using Shouldly;
using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

public sealed class XfdfTests
{
    private static readonly DocumentProcessor Processor = new();
    private static readonly XfdfEditor Editor = new();
    private static readonly AnnotationEditor AnnotEditor = new();

    private static Task<Abstractions.IPdfDocument> LoadAsync(byte[] b) =>
        Processor.LoadAsync(new MemoryStream(b));

    [Fact]
    public void ExportAnnotationsToXfdf_NoAnnotations_ReturnsEmptyXfdf()
    {
        var doc = Processor.LoadAsync(new MemoryStream(Helpers.PdfFixtures.SinglePage())).GetAwaiter().GetResult();
        var xfdf = Editor.ExportAnnotationsToXfdf(doc);
        xfdf.ShouldContain("<annots");
        xfdf.ShouldNotContain("<text");
    }

    [Fact]
    public async Task ExportAnnotationsToXfdf_WithAnnotation_ContainsRect()
    {
        await using var doc = await LoadAsync(
            Helpers.PdfFixtures.WithAnnotation(contents: "Exported note"));
        var xfdf = Editor.ExportAnnotationsToXfdf(doc);
        xfdf.ShouldContain("rect=");
        xfdf.ShouldContain("Exported note");
    }

    [Fact]
    public async Task ExportAnnotationsToXfdf_WithAnnotation_ContainsPageAttr()
    {
        await using var doc = await LoadAsync(
            Helpers.PdfFixtures.WithAnnotation());
        var xfdf = Editor.ExportAnnotationsToXfdf(doc);
        xfdf.ShouldContain("page=\"0\"");
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_AddsAnnotation()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        const string xfdf = """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="50,700,100,750"><contents>Imported note</contents></text></annots></xfdf>""";
        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf);
        doc.Pages[1].GetAnnotations().Count.ShouldBe(1);
    }

    [Fact]
    public async Task ImportAnnotationsFromXfdf_Contents_RoundTripped()
    {
        await using var doc = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        const string xfdf = """<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><annots><text page="0" rect="50,700,100,750"><contents>Hello XFDF</contents></text></annots></xfdf>""";
        await Editor.ImportAnnotationsFromXfdfAsync(doc, xfdf);
        doc.Pages[1].GetAnnotations()[0].Contents.ShouldBe("Hello XFDF");
    }

    [Fact]
    public async Task ExportThenImport_RoundTripsAnnotations()
    {
        await using var source = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        // ReSharper disable once BadListLineBreaks
        await AnnotEditor.AddAnnotationAsync(source, 1, new Annotation(AnnotationSubtype.Text, 50, 700, 50, 50, "Round-trip note"));

        var xfdf = Editor.ExportAnnotationsToXfdf(source);

        await using var target = await LoadAsync(Helpers.PdfFixtures.SinglePage());
        await Editor.ImportAnnotationsFromXfdfAsync(target, xfdf);
        target.Pages[1].GetAnnotations().Count.ShouldBe(1);
        target.Pages[1].GetAnnotations()[0].Contents.ShouldBe("Round-trip note");
    }
}
