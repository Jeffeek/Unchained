using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Content;

public sealed class TextExtractorTests
{
    private static readonly IReadOnlyDictionary<string, string> HelveticaMap =
        new Dictionary<string, string> { ["F1"] = "Helvetica", ["F2"] = "Helvetica-Bold" };

    private static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new Dictionary<string, string>();

    // ── Basic operator handling ───────────────────────────────────────────────

    [Fact]
    public void Extract_NoOperators_ReturnsEmpty()
    {
        var spans = TextExtractor.Extract((IReadOnlyList<ContentOperator>)[], EmptyMap);
        spans.ShouldBeEmpty();
    }

    [Fact]
    public void Extract_NoTextBlock_NoTjOutside_ReturnsEmpty()
    {
        // Tj outside BT..ET is ignored
        var ops = new[]
        {
            new ContentOperator("Tj", [PdfString.FromLatin1("hello")])
        };
        TextExtractor.Extract(ops, EmptyMap).ShouldBeEmpty();
    }

    [Fact]
    public void Extract_SimpleTj_ReturnsOneSpan()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(12)]),
            new ContentOperator("Td", [new PdfInteger(100), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Hello")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(1);
        spans[0].Text.ShouldBe("Hello");
        spans[0].FontName.ShouldBe("Helvetica");
        spans[0].FontSize.ShouldBe(12.0);
    }

    [Fact]
    public void Extract_Tj_PositionMatchesTd()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(50), new PdfInteger(800)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("A")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans[0].X.ShouldBe(50.0, tolerance: 0.001);
        spans[0].Y.ShouldBe(800.0, tolerance: 0.001);
    }

    [Fact]
    public void Extract_Tm_SetsAbsolutePosition()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(999), new PdfInteger(999)]),
            new ContentOperator("Tm",
            [
                new PdfInteger(1), new PdfInteger(0),
                new PdfInteger(0), new PdfInteger(1),
                new PdfInteger(200), new PdfInteger(500)
            ]),
            new ContentOperator("Tj", [PdfString.FromLatin1("X")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans[0].X.ShouldBe(200.0, tolerance: 0.001);
        spans[0].Y.ShouldBe(500.0, tolerance: 0.001);
    }

    [Fact]
    public void Extract_TwoLinesViaTD_TwoSpans()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(36), new PdfInteger(750)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Line1")]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(-20)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Line2")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(2);
        spans[0].Y.ShouldBeGreaterThan(spans[1].Y);
    }

    [Fact]
    public void Extract_TStar_AdvancesLineByLeading()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("TL", [new PdfInteger(14)]),
            new ContentOperator("Td", [new PdfInteger(36), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("A")]),
            new ContentOperator("T*", []),
            new ContentOperator("Tj", [PdfString.FromLatin1("B")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(2);
        (spans[0].Y - spans[1].Y).ShouldBe(14.0, tolerance: 0.01);
    }

    // ── TJ array ─────────────────────────────────────────────────────────────

    [Fact]
    public void Extract_TJArray_ExtractsText()
    {
        var arr = new PdfArray([
            PdfString.FromLatin1("Hello"),
            new PdfInteger(-100),
            PdfString.FromLatin1(" World")
        ]);
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(12)]),
            new ContentOperator("Td", [new PdfInteger(36), new PdfInteger(700)]),
            new ContentOperator("TJ", [arr]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(2);
        spans[0].Text.ShouldBe("Hello");
        spans[1].Text.ShouldBe(" World");
    }

    // ── Font map resolution ───────────────────────────────────────────────────

    [Fact]
    public void Extract_UnknownFont_UsesResourceNameAsFontName()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F99"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(0)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("X")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, EmptyMap);
        spans[0].FontName.ShouldBe("F99");
    }

    // ── Text state parameters ─────────────────────────────────────────────────

    [Fact]
    public void Extract_Tc_AffectsAdvanceWidth()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Tc", [new PdfInteger(5)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(0)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("A")]),
            new ContentOperator("ET", [])
        };
        var spansWithTc = TextExtractor.Extract(ops, HelveticaMap);

        var opsNoTc = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(0)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("A")]),
            new ContentOperator("ET", [])
        };
        var spansNoTc = TextExtractor.Extract(opsNoTc, HelveticaMap);

        spansWithTc[0].Width.ShouldBeGreaterThan(spansNoTc[0].Width);
    }

    // ── Reading order sort ────────────────────────────────────────────────────

    [Fact]
    public void Extract_MultipleSpans_SortedByYDescThenXAsc()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Tm", [new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(200), new PdfInteger(300)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Low")]),
            new ContentOperator("Tm", [new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(50), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("High")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans[0].Text.ShouldBe("High");
        spans[1].Text.ShouldBe("Low");
    }

    // ── Quote operators ───────────────────────────────────────────────────────

    [Fact]
    public void Extract_SingleQuoteOp_MovesLineAndShowsText()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("TL", [new PdfInteger(12)]),
            new ContentOperator("Td", [new PdfInteger(36), new PdfInteger(700)]),
            new ContentOperator("'", [PdfString.FromLatin1("Next line")]),
            new ContentOperator("ET", [])
        };
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(1);
        spans[0].Text.ShouldBe("Next line");
    }

    // ── SpansToText ───────────────────────────────────────────────────────────

    [Fact]
    public void SpansToText_EmptySpans_ReturnsEmpty() =>
        TextExtractor.SpansToText([]).ShouldBe(string.Empty);

    [Fact]
    public void SpansToText_SingleSpan_ReturnsText()
    {
        var span = new TextSpan(
            "Hello",
            0,
            100,
            50,
            12,
            "Helvetica"
        );
        TextExtractor.SpansToText([span]).ShouldBe("Hello");
    }

    [Fact]
    public void SpansToText_TwoSpansSameLine_JoinedWithSpace()
    {
        var spans = new[]
        {
            new TextSpan(
                "Hello",
                0,
                100,
                30,
                12,
                "Helvetica"
            ),
            new TextSpan(
                "World",
                50,
                100,
                30,
                12,
                "Helvetica"
            )
        };
        var text = TextExtractor.SpansToText(spans);
        text.ShouldBe("Hello World");
    }

    [Fact]
    public void SpansToText_TwoDifferentLines_JoinedWithNewline()
    {
        var spans = new[]
        {
            new TextSpan(
                "Line1",
                0,
                200,
                30,
                12,
                "Helvetica"
            ),
            new TextSpan(
                "Line2",
                0,
                180,
                30,
                12,
                "Helvetica"
            )
        };
        var text = TextExtractor.SpansToText(spans);
        text.ShouldContain('\n');
        text.ShouldStartWith("Line1");
        text.ShouldEndWith("Line2");
    }
}
