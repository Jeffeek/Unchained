using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Text;
using Unchained.Pptx.Parsing;
using Xunit;
using TextWriter = Unchained.Pptx.Writing.TextWriter;

namespace Unchained.Pptx.Tests.UnitTests.Writing;

/// <summary>
///     Round-trips <see cref="TextFrame" /> objects through <see cref="TextWriter" /> and
///     <see cref="TextParser" />, exercising body properties, paragraph/run formatting, bullets,
///     line spacing, fields, and line breaks. Covers both serializers in one pass.
/// </summary>
public sealed class TextWriterTests
{
    private static TextFrame RoundTrip(TextFrame frame)
    {
        var xml = TextWriter.WriteAsShape(frame);
        return TextParser.ParseTextBody(xml);
    }

    [Fact]
    public void WriteAsShape_ProducesTxBodyRoot()
    {
        var frame = new TextFrame();
        var el = TextWriter.WriteAsShape(frame);
        el.Name.LocalName.ShouldBe("txBody");
    }

    [Fact]
    public void WriteAsDml_ProducesTxBodyRoot()
    {
        var frame = new TextFrame();
        var el = TextWriter.WriteAsDml(frame);
        el.Name.LocalName.ShouldBe("txBody");
    }

    [Fact]
    public void Empty_WritesPlaceholderParagraph()
    {
        var el = TextWriter.WriteAsShape(new TextFrame());
        el.Elements().Count(static e => e.Name.LocalName == "p").ShouldBe(1);
    }

    [Fact]
    public void RoundTrip_SingleRunText_Preserved()
    {
        var frame = new TextFrame();
        frame.Paragraphs.Add("Hello world");

        var result = RoundTrip(frame);
        result.PlainText.ShouldContain("Hello world");
    }

    [Fact]
    public void RoundTrip_RunFormatting_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add();
        var run = para.Runs.Add("Bold");
        run.Format.Bold = InheritableBool.True;
        run.Format.Italic = InheritableBool.True;
        run.Format.FontSizePoints = 24;
        run.Format.Underline = TextUnderlineType.Single;
        run.Format.LatinFont = "Calibri";

        var result = RoundTrip(frame);
        var parsedRun = result.Paragraphs[0].Runs[0];
        parsedRun.Format.Bold.Value.ShouldBe(true);
        parsedRun.Format.Italic.Value.ShouldBe(true);
        parsedRun.Format.FontSizePoints.ShouldBe(24);
        parsedRun.Format.Underline.ShouldBe(TextUnderlineType.Single);
        parsedRun.Format.LatinFont.ShouldBe("Calibri");
    }

    [Fact]
    public void RoundTrip_ParagraphAlignment_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add("Centered");
        para.Alignment = TextAlignment.Center;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Alignment.ShouldBe(TextAlignment.Center);
    }

    [Fact]
    public void RoundTrip_BodyProperties_Preserved()
    {
        var frame = new TextFrame
        {
            Format =
            {
                VerticalAnchor = TextAnchor.Middle,
                WrapText = false,
                ColumnCount = 2
            }
        };
        frame.Paragraphs.Add("x");

        var result = RoundTrip(frame);
        result.Format.VerticalAnchor.ShouldBe(TextAnchor.Middle);
        result.Format.WrapText.ShouldBeFalse();
        result.Format.ColumnCount.ShouldBe(2);
    }

    [Fact]
    public void RoundTrip_NumberedBullet_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add("Item");
        para.Bullet.Type = BulletType.Numbered;
        para.Bullet.Numbered = new NumberedBulletFormat
        {
            Style = NumberedBulletStyle.ArabicPeriod,
            StartAt = 1
        };

        var result = RoundTrip(frame);
        result.Paragraphs[0].Bullet.Type.ShouldBe(BulletType.Numbered);
    }

    [Fact]
    public void RoundTrip_LineSpacingPercent_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add("Spaced");
        para.Spacing = LineSpacing.FromPercent(150);

        var result = RoundTrip(frame);
        result.Paragraphs[0].Spacing.ShouldNotBeNull();
        result.Paragraphs[0].Spacing!.Value.Mode.ShouldBe(LineSpacingMode.Percent);
        result.Paragraphs[0].Spacing!.Value.Value.ShouldBe(150, 0.5);
    }

    [Fact]
    public void RoundTrip_SpaceBeforeAfter_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add("x");
        para.SpaceBeforePoints = 12;
        para.SpaceAfterPoints = 6;

        var result = RoundTrip(frame);
        result.Paragraphs[0].SpaceBeforePoints.ShouldNotBeNull();
        result.Paragraphs[0].SpaceBeforePoints!.Value.ShouldBe(12, 0.5);
        result.Paragraphs[0].SpaceAfterPoints.ShouldNotBeNull();
        result.Paragraphs[0].SpaceAfterPoints!.Value.ShouldBe(6, 0.5);
    }

    [Fact]
    public void RoundTrip_LineBreak_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add();
        para.Runs.Add("a");
        para.Runs.Add("\n");
        para.Runs.Add("b");

        var result = RoundTrip(frame);
        result.Paragraphs[0].Runs.Any(static r => r.Text == "\n").ShouldBeTrue();
    }

    [Fact]
    public void RoundTrip_Field_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add();
        var run = para.Runs.Add("3");
        run.Field = FieldType.SlideNumber;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Runs.Any(static r => r.Field.HasValue).ShouldBeTrue();
    }
}
