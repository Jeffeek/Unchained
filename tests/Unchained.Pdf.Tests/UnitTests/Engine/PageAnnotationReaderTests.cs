using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Direct unit coverage for <see cref="PageAnnotationReader.GetAnnotations" />: every subtype
///     mapping, the colour array, the indirect-reference <c>/Annots</c> array, the missing/absent
///     annots cases, and the missing-rectangle default.
/// </summary>
public sealed class PageAnnotationReaderTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfDictionary Annot(string subtype, params (string Key, PdfObject Value)[] extra)
    {
        var d = new Dictionary<string, PdfObject>
        {
            ["Type"] = PdfName.Get("Annot"),
            ["Subtype"] = PdfName.Get(subtype),
            ["Rect"] = new PdfArray([new PdfInteger(10), new PdfInteger(20), new PdfInteger(110), new PdfInteger(70)])
        };
        foreach (var (k, v) in extra) d[k] = v;
        return new PdfDictionary(d);
    }

    private static PdfDictionary PageWith(params PdfObject[] annots) =>
        new(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Page,
                ["Annots"] = new PdfArray(annots.ToList())
            }
        );

    [Fact]
    public void NoAnnots_ReturnsEmpty() =>
        PageAnnotationReader.GetAnnotations(new PdfDictionary(new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page }), Core()).ShouldBeEmpty();

    [Fact]
    public void AnnotsNotArrayOrRef_ReturnsEmpty()
    {
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Annots"] = new PdfInteger(7) }
        );
        PageAnnotationReader.GetAnnotations(page, Core()).ShouldBeEmpty();
    }

    [
        Theory,
        InlineData("Text", AnnotationSubtype.Text),
        InlineData("Highlight", AnnotationSubtype.Highlight),
        InlineData("Link", AnnotationSubtype.Link),
        InlineData("FreeText", AnnotationSubtype.FreeText),
        InlineData("Square", AnnotationSubtype.Square),
        InlineData("Circle", AnnotationSubtype.Circle),
        InlineData("Unknown", AnnotationSubtype.Text)
    ]
    public void Subtype_MapsToEnum(string subtype, AnnotationSubtype expected)
    {
        var result = PageAnnotationReader.GetAnnotations(PageWith(Annot(subtype)), Core());
        result.ShouldHaveSingleItem().Subtype.ShouldBe(expected);
    }

    [Fact]
    public void RectAndContentsAndColor_AreRead()
    {
        var annot = Annot(
            "Text",
            ("Contents", PdfString.FromLatin1("hello")),
            ("C", new PdfArray([new PdfReal(1), new PdfReal(0), new PdfReal(0)]))
        );
        var a = PageAnnotationReader.GetAnnotations(PageWith(annot), Core()).ShouldHaveSingleItem();
        a.X.ShouldBe(10f);
        a.Y.ShouldBe(20f);
        a.Width.ShouldBe(100f);
        a.Height.ShouldBe(50f);
        a.Contents.ShouldBe("hello");
        a.Color.ShouldBe([1f, 0f, 0f]);
    }

    [Fact]
    public void MissingRect_DefaultsToZero()
    {
        var annot = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Get("Annot"), ["Subtype"] = PdfName.Get("Text") }
        );
        var a = PageAnnotationReader.GetAnnotations(PageWith(annot), Core()).ShouldHaveSingleItem();
        a.X.ShouldBe(0f);
        a.Width.ShouldBe(0f);
    }

    [Fact]
    public void AnnotsAsIndirectReference_IsResolved()
    {
        // /Annots is an indirect reference to an array; each element is also an indirect ref.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots 5 0 R >>",
            "<< /Foo 1 >>",
            "[6 0 R]",
            "<< /Type /Annot /Subtype /Square /Rect [0 0 10 10] >>"
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        var result = PageAnnotationReader.GetAnnotations(core.GetPage(1), core);
        result.ShouldHaveSingleItem().Subtype.ShouldBe(AnnotationSubtype.Square);
    }
}
