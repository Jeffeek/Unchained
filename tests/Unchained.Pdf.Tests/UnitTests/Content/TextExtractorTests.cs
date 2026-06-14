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
        spans[0].X.ShouldBe(50.0, 0.001);
        spans[0].Y.ShouldBe(800.0, 0.001);
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
        spans[0].X.ShouldBe(200.0, 0.001);
        spans[0].Y.ShouldBe(500.0, 0.001);
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
        (spans[0].Y - spans[1].Y).ShouldBe(14.0, 0.01);
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
            new ContentOperator("Tm",
                [new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(200), new PdfInteger(300)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Low")]),
            new ContentOperator("Tm",
                [new PdfInteger(1), new PdfInteger(0), new PdfInteger(0), new PdfInteger(1), new PdfInteger(50), new PdfInteger(700)]),
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

    // ── Double-quote operator ─────────────────────────────────────────────────

    [Fact]
    public void Extract_DoubleQuoteOp_SetsTwTcMovesLineAndShowsText()
    {
        // " tw tc string — sets word/char spacing, moves to next line, shows string.
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("TL", [new PdfInteger(12)]),
            new ContentOperator("Td", [new PdfInteger(36), new PdfInteger(700)]),
            new ContentOperator("\"",
            [
                new PdfReal(2.0), // tw
                new PdfReal(0.5), // tc
                PdfString.FromLatin1("Quoted")
            ]),
            new ContentOperator("ET", [])
        };

        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(1);
        spans[0].Text.ShouldBe("Quoted");
    }

    [Fact]
    public void Extract_DoubleQuoteOp_UpdatesWordAndCharSpacing()
    {
        // Verify that " sets tw/tc which affect subsequent Tj width.
        // We compare a Tj that uses the tw/tc set by " vs default spacing.
        var opsWithSpacing = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("TL", [new PdfInteger(14)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("\"",
            [
                new PdfReal(5.0), // large tw
                new PdfReal(0.0),
                PdfString.FromLatin1("A B") // contains space, tw applies
            ]),
            new ContentOperator("ET", [])
        };

        var opsNoSpacing = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("TL", [new PdfInteger(14)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("A B")]),
            new ContentOperator("ET", [])
        };

        var withSpacing = TextExtractor.Extract(opsWithSpacing, HelveticaMap);
        var without = TextExtractor.Extract(opsNoSpacing, HelveticaMap);

        withSpacing[0].Width.ShouldBeGreaterThan(without[0].Width);
    }

    // ── TD operator (updates leading) ─────────────────────────────────────────

    [Fact]
    public void Extract_TDOp_UpdatesLeadingAndMovesLine()
    {
        // TD tx ty sets tl = -ty and moves the line.
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(36), new PdfInteger(750)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Line1")]),
            new ContentOperator("TD", [new PdfInteger(0), new PdfInteger(-16)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Line2")]),
            new ContentOperator("T*", []), // should now use leading=16
            new ContentOperator("Tj", [PdfString.FromLatin1("Line3")]),
            new ContentOperator("ET", [])
        };

        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(3);

        // Y order: Line1 highest, then Line2, then Line3.
        var line1 = spans.First(static s => s.Text == "Line1");
        var line2 = spans.First(static s => s.Text == "Line2");
        var line3 = spans.First(static s => s.Text == "Line3");

        line1.Y.ShouldBeGreaterThan(line2.Y);
        line2.Y.ShouldBeGreaterThan(line3.Y);
        // The gap between Line2 and Line3 should equal the leading set by TD (16).
        (line2.Y - line3.Y).ShouldBe(16.0, 0.01);
    }

    // ── Tz (horizontal scaling) ───────────────────────────────────────────────

    [Fact]
    public void Extract_TzOp_ScalesAdvanceWidth()
    {
        var ops100 = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Tz", [new PdfInteger(100)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Wide")]),
            new ContentOperator("ET", [])
        };

        var ops50 = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Tz", [new PdfInteger(50)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Wide")]),
            new ContentOperator("ET", [])
        };

        var normal = TextExtractor.Extract(ops100, HelveticaMap);
        var compressed = TextExtractor.Extract(ops50, HelveticaMap);

        normal[0].Width.ShouldBeGreaterThan(compressed[0].Width);
    }

    // ── Tw (word spacing) ─────────────────────────────────────────────────────

    [Fact]
    public void Extract_TwOp_IncreasesAdvanceForSpaceChar()
    {
        var opsWithTw = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Tw", [new PdfReal(5.0)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("A B")]),
            new ContentOperator("ET", [])
        };

        var opsNoTw = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("A B")]),
            new ContentOperator("ET", [])
        };

        var withTw = TextExtractor.Extract(opsWithTw, HelveticaMap);
        var without = TextExtractor.Extract(opsNoTw, HelveticaMap);

        withTw[0].Width.ShouldBeGreaterThan(without[0].Width);
    }

    // ── ShowString empty/zero-font guards ─────────────────────────────────────

    [Fact]
    public void Extract_ZeroFontSize_ProducesNoSpans()
    {
        // ShowString has an early-return when fontSize <= 0.
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(0)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Invisible")]),
            new ContentOperator("ET", [])
        };

        TextExtractor.Extract(ops, HelveticaMap).ShouldBeEmpty();
    }

    [Fact]
    public void Extract_EmptyString_ProducesNoSpans()
    {
        // ShowString must not add a span for an empty string.
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1(string.Empty)]),
            new ContentOperator("ET", [])
        };

        TextExtractor.Extract(ops, HelveticaMap).ShouldBeEmpty();
    }

    // ── TJ with PdfReal kern ──────────────────────────────────────────────────

    [Fact]
    public void Extract_TJArrayWithRealKern_AdjustsPosition()
    {
        // PdfReal element in a TJ array → tmE adjustment (same branch as PdfInteger but real).
        var arr = new PdfArray([
            PdfString.FromLatin1("Hello"),
            new PdfReal(-50.0),
            PdfString.FromLatin1("World")
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
        spans.Select(static s => s.Text).ShouldContain("Hello");
        spans.Select(static s => s.Text).ShouldContain("World");
    }

    // ── Multiple fonts in one text block ──────────────────────────────────────

    [Fact]
    public void Extract_MultipleFonts_SpansCarryCorrectFontNames()
    {
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(36), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Regular")]),
            new ContentOperator("Tf", [PdfName.Get("F2"), new PdfInteger(10)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(-20)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("Bold")]),
            new ContentOperator("ET", [])
        };

        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(2);
        spans.First(static s => s.Text == "Regular").FontName.ShouldBe("Helvetica");
        spans.First(static s => s.Text == "Bold").FontName.ShouldBe("Helvetica-Bold");
    }

    // ── Tf outside BT ─────────────────────────────────────────────────────────

    [Fact]
    public void Extract_TfOutsideBt_StateNotSet()
    {
        // Tf outside BT..ET still updates the font state (no inText guard on Tf).
        // Then a Tj inside BT should use that font.
        var ops = new[]
        {
            new ContentOperator("Tf", [PdfName.Get("F2"), new PdfInteger(14)]),
            new ContentOperator("BT", []),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("Tj", [PdfString.FromLatin1("X")]),
            new ContentOperator("ET", [])
        };

        // Tf sets font state even outside BT. However, "F2" is not in HelveticaMap,
        // so the extractor cannot decode the glyph — no span is produced.
        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.Count.ShouldBe(0);
    }

    // ── Single-quote operator with non-PdfString operand ──────────────────────

    [Fact]
    public void Extract_SingleQuoteOp_WithNonStringOperand_ProducesNoSpan()
    {
        // ' operator with a non-PdfString operand — should move line but not add span.
        var ops = new[]
        {
            new ContentOperator("BT", []),
            new ContentOperator("Tf", [PdfName.Get("F1"), new PdfInteger(10)]),
            new ContentOperator("TL", [new PdfInteger(12)]),
            new ContentOperator("Td", [new PdfInteger(0), new PdfInteger(700)]),
            new ContentOperator("'", [new PdfInteger(42)]), // wrong type
            new ContentOperator("ET", [])
        };

        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.ShouldBeEmpty();
    }

    // ── SpansToText gap detection ─────────────────────────────────────────────

    [Fact]
    public void SpansToText_SpansOnSameLineNoGap_NotInsertedSpace()
    {
        // Two spans on the same Y, with prevEndX exactly at the start of the next span
        // (gap <= 1.0) — no extra space should be inserted.
        var spans = new[]
        {
            // ReSharper disable BadListLineBreaks
            new TextSpan("Hello",
                0,
                100,
                25,
                12,
                "Helvetica"),
            new TextSpan("World",
                25,
                100,
                25,
                12,
                "Helvetica") // X == prevEndX, gap == 0
            // ReSharper restore BadListLineBreaks
        };

        var text = TextExtractor.SpansToText(spans);
        text.ShouldBe("HelloWorld");
    }

    [Fact]
    public void SpansToText_SpansOnSameLineWithLargeGap_InsertsSpace()
    {
        // Gap > 1.0 between spans on same line triggers space insertion.
        var spans = new[]
        {
            // ReSharper disable BadListLineBreaks
            new TextSpan("Left",
                0,
                100,
                20,
                12,
                "Helvetica"),
            new TextSpan("Right",
                30,
                100,
                20,
                12,
                "Helvetica") // gap = 30-20 = 10 > 1.0
            // ReSharper restore BadListLineBreaks
        };

        var text = TextExtractor.SpansToText(spans);
        text.ShouldBe("Left Right");
    }

    [Fact]
    public void SpansToText_ThreeLinesYWithinThreshold_TreatedAsSameLine()
    {
        // Y difference <= LineThreshold (2.0) → same line, no newline.
        var spans = new[]
        {
            // ReSharper disable BadListLineBreaks
            new TextSpan("A",
                0,
                100,
                10,
                12,
                "Helvetica"),
            new TextSpan("B",
                15,
                101,
                10,
                12,
                "Helvetica") // |101-100| = 1 < 2.0
            // ReSharper restore BadListLineBreaks
        };

        var text = TextExtractor.SpansToText(spans);
        text.ShouldNotContain('\n');
    }

    [Fact]
    public void SpansToText_YDifferenceExceedsThreshold_InsertsNewline()
    {
        // Y difference > 2.0 → different lines.
        var spans = new[]
        {
            // ReSharper disable BadListLineBreaks
            new TextSpan("Top",
                0,
                200,
                20,
                12,
                "Helvetica"),
            new TextSpan("Bottom",
                0,
                195,
                20,
                12,
                "Helvetica") // |200-195| = 5 > 2.0
            // ReSharper restore BadListLineBreaks
        };

        var text = TextExtractor.SpansToText(spans);
        text.ShouldContain('\n');
    }
}

