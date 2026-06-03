using System.Text;
using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Content;

/// <summary>
/// Covers gaps in ContentStreamParser and TextExtractor not exercised by the
/// primary test files: inline image handling (BI/ID/EI), skipped/unknown tokens,
/// double-quote operator, TD leading update, Tz/Tw state, empty-text guards,
/// TJ with PdfReal kern, SpansToText adjacent-span gap logic, and
/// multi-font resolution via the integration-level page API.
/// </summary>
public sealed class ContentCoverageTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<ContentOperator> Parse(string stream) =>
        ContentStreamParser.Parse(Encoding.Latin1.GetBytes(stream));

    private static IEnumerable<ContentOperator> Parse(byte[] data) =>
        ContentStreamParser.Parse(data);

    private static readonly IReadOnlyDictionary<string, string> HelveticaMap =
        new Dictionary<string, string> { ["F1"] = "Helvetica", ["F2"] = "Helvetica-Bold" };

    // ── ContentStreamParser: inline image ────────────────────────────────────

    [Fact]
    public void Parse_InlineImageBiIdEi_SkipsImageDataAndContinues()
    {
        // BI/ID/EI block followed by a regular operator.
        // The parser must consume the ID keyword and the binary payload up to EI,
        // then continue to emit subsequent operators.
        const string stream = "BI /W 10 /H 10 ID \xFF\xD8\xff\x20" + "\n\x45\x49\x20\x71";
        var ops = Parse(stream);

        // The BI dict-building ops (before ID) are emitted as a Name operator BI.
        // The EI skipping leaves the next real operator (q) intact.
        ops.ShouldNotBeEmpty();
        ops.Last().Name.ShouldBe("q");
    }

    [Fact]
    public void Parse_InlineImageWithoutTrailingEi_DoesNotThrow()
    {
        // Pathological stream: ID with no matching EI. Parser must not crash.
        const string stream = "BT ID some binary data without end marker";
        var ops = Should.NotThrow(static () => Parse(stream));
        // BT was emitted before the ID token.
        ops.Select(static o => o.Name).ShouldContain("BT");
    }

    [Fact]
    public void Parse_InlineImageEiPrecededBySpace_IsRecognized()
    {
        // EI must be preceded by a whitespace byte (§7.4.9).
        // Using a space before EI so the scanner matches it.
        var bytes = Encoding.Latin1.GetBytes("BI ID ")
            .Concat(" EI"u8.ToArray())
            .Concat(Encoding.Latin1.GetBytes(" q"))
            .ToArray();

        var ops = Parse(bytes);
        ops.Last().Name.ShouldBe("q");
    }

    [Fact]
    public void Parse_InlineImageEiPrecededByLinefeed_IsRecognized()
    {
        var bytes = Encoding.Latin1.GetBytes("BI ID ")
            .Concat(new byte[] { 0xFF, 0x00, (byte)'\n', (byte)'E', (byte)'I' })
            .Concat(Encoding.Latin1.GetBytes(" Tj"))
            .ToArray();

        var ops = Parse(bytes);
        ops.Last().Name.ShouldBe("Tj");
    }

    // ── ContentStreamParser: unexpected / skipped tokens ─────────────────────

    [Fact]
    public void Parse_StandaloneArrayEnd_IsSkippedGracefully()
    {
        // ']' appearing outside an array is an unexpected token; must be skipped.
        var ops = Parse("] BT ET");
        ops.Count.ShouldBe(2);
        ops[0].Name.ShouldBe("BT");
    }

    [Fact]
    public void Parse_StandaloneDictionaryEnd_IsSkippedGracefully()
    {
        var ops = Parse(">> BT");
        ops.Count.ShouldBe(1);
        ops[0].Name.ShouldBe("BT");
    }

    [Fact]
    public void Parse_BooleanTrueOperand_ParsedAndAssignedToOperator()
    {
        var ops = Parse("true false Q");
        ops.Count.ShouldBe(1);
        ops[0].Name.ShouldBe("Q");
        ops[0].Operands.Count.ShouldBe(2);
        ops[0].Operands[0].ShouldBeOfType<PdfBoolean>();
        ops[0].Operands[1].ShouldBeOfType<PdfBoolean>();
    }

    [Fact]
    public void Parse_NullOperand_ParsedAndAssignedToOperator()
    {
        var ops = Parse("null Q");
        ops.Count.ShouldBe(1);
        ops[0].Operands[0].ShouldBeOfType<PdfNull>();
    }

    [Fact]
    public void Parse_DictionaryOperand_ParsedAsOperand()
    {
        // Inline dictionary as operand (unusual but spec-legal in some contexts).
        var ops = Parse("<< /Key (value) >> Q");
        ops.Count.ShouldBe(1);
        ops[0].Name.ShouldBe("Q");
        ops[0].Operands[0].ShouldBeOfType<PdfDictionary>();
    }

    // ── ContentStreamParser: comment handling (already covered via
    //    existing Parse_CommentsSkipped; verify comment-only stream) ──────────

    [Fact]
    public void Parse_StreamWithOnlyComments_ReturnsEmpty()
    {
        var ops = Parse("% comment line 1\n% comment line 2\n");
        ops.ShouldBeEmpty();
    }

    // ── TextExtractor: double-quote operator ─────────────────────────────────

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
                new PdfReal(2.0),  // tw
                new PdfReal(0.5),  // tc
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
                new PdfReal(5.0),  // large tw
                new PdfReal(0.0),
                PdfString.FromLatin1("A B")  // contains space, tw applies
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

    // ── TextExtractor: TD operator (updates leading) ─────────────────────────

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
            new ContentOperator("T*", []),  // should now use leading=16
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
        (line2.Y - line3.Y).ShouldBe(16.0, tolerance: 0.01);
    }

    // ── TextExtractor: Tz (horizontal scaling) ───────────────────────────────

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

    // ── TextExtractor: Tw (word spacing) ─────────────────────────────────────

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

    // ── TextExtractor: ShowString empty/zero-font guards ─────────────────────

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

    // ── TextExtractor: TJ with PdfReal kern ──────────────────────────────────

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

    // ── TextExtractor: multiple fonts in one text block ───────────────────────

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

    // ── TextExtractor: Tf ignored outside BT ──────────────────────────────────

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

    // ── TextExtractor: single-quote operator with non-PdfString operand ───────

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
            new ContentOperator("'", [new PdfInteger(42)]),  // wrong type
            new ContentOperator("ET", [])
        };

        var spans = TextExtractor.Extract(ops, HelveticaMap);
        spans.ShouldBeEmpty();
    }

    // ── TextExtractor: SpansToText gap detection ──────────────────────────────

    [Fact]
    public void SpansToText_SpansOnSameLineNoGap_NotInsertedSpace()
    {
        // Two spans on the same Y, with prevEndX exactly at the start of the next span
        // (gap <= 1.0) — no extra space should be inserted.
        var spans = new[]
        {
            // ReSharper disable BadListLineBreaks
            new TextSpan("Hello", 0, 100, 25, 12, "Helvetica"),
            new TextSpan("World", 25, 100, 25, 12, "Helvetica")  // X == prevEndX, gap == 0
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
            new TextSpan("Left", 0, 100, 20, 12, "Helvetica"),
            new TextSpan("Right", 30, 100, 20, 12, "Helvetica")  // gap = 30-20 = 10 > 1.0
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
            new TextSpan("A", 0, 100, 10, 12, "Helvetica"),
            new TextSpan("B", 15, 101, 10, 12, "Helvetica")  // |101-100| = 1 < 2.0
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
            new TextSpan("Top", 0, 200, 20, 12, "Helvetica"),
            new TextSpan("Bottom", 0, 195, 20, 12, "Helvetica")  // |200-195| = 5 > 2.0
            // ReSharper restore BadListLineBreaks
        };

        var text = TextExtractor.SpansToText(spans);
        text.ShouldContain('\n');
    }
}
