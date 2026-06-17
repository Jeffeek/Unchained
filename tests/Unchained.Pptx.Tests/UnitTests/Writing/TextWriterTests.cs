using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Drawing;
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

    [Fact]
    public void RoundTrip_BodyMargins_Preserved()
    {
        var frame = new TextFrame
        {
            Format =
            {
                MarginLeft = Emu.FromPoints(20),
                MarginRight = Emu.FromPoints(21),
                MarginTop = Emu.FromPoints(22),
                MarginBottom = Emu.FromPoints(23)
            }
        };
        frame.Paragraphs.Add("x");

        var result = RoundTrip(frame);
        result.Format.MarginLeft.Value.ShouldBe(Emu.FromPoints(20).Value);
        result.Format.MarginRight.Value.ShouldBe(Emu.FromPoints(21).Value);
        result.Format.MarginTop.Value.ShouldBe(Emu.FromPoints(22).Value);
        result.Format.MarginBottom.Value.ShouldBe(Emu.FromPoints(23).Value);
    }

    [
        Theory,
        InlineData(TextDirection.Vertical90),
        InlineData(TextDirection.Vertical270),
        InlineData(TextDirection.Stacked)
    ]
    public void RoundTrip_TextDirection_Preserved(TextDirection direction)
    {
        var frame = new TextFrame { Format = { Direction = direction } };
        frame.Paragraphs.Add("x");

        var result = RoundTrip(frame);
        result.Format.Direction.ShouldBe(direction);
    }

    [
        Theory,
        InlineData(TextAutofit.ShrinkText),
        InlineData(TextAutofit.ResizeShape),
        InlineData(TextAutofit.None)
    ]
    public void RoundTrip_Autofit_Preserved(TextAutofit autofit)
    {
        var frame = new TextFrame { Format = { Autofit = autofit } };
        frame.Paragraphs.Add("x");

        var result = RoundTrip(frame);
        result.Format.Autofit.ShouldBe(autofit);
    }

    [Fact]
    public void RoundTrip_Warp_Preserved()
    {
        var frame = new TextFrame { Format = { Warp = new TextWarpFormat { Preset = "textArchUp" } } };
        frame.Paragraphs.Add("Curved");

        var result = RoundTrip(frame);
        result.Format.Warp.ShouldNotBeNull();
        result.Format.Warp.Preset.ShouldBe("textArchUp");
    }

    [
        Theory,
        InlineData(TextAlignment.Left),
        InlineData(TextAlignment.Center),
        InlineData(TextAlignment.Right),
        InlineData(TextAlignment.Justify),
        InlineData(TextAlignment.JustifyLow),
        InlineData(TextAlignment.Distributed),
        InlineData(TextAlignment.ThaiDistributed)
    ]
    public void RoundTrip_AllAlignments_Preserved(TextAlignment alignment)
    {
        var frame = new TextFrame();
        frame.Paragraphs.Add("text").Alignment = alignment;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Alignment.ShouldBe(alignment);
    }

    [
        Theory,
        InlineData(TextUnderlineType.Double),
        InlineData(TextUnderlineType.Heavy),
        InlineData(TextUnderlineType.Wavy),
        InlineData(TextUnderlineType.DotDash),
        InlineData(TextUnderlineType.Words)
    ]
    public void RoundTrip_UnderlineVariants_Preserved(TextUnderlineType underline)
    {
        var frame = new TextFrame();
        frame.Paragraphs.Add().Runs.Add("u").Format.Underline = underline;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Runs[0].Format.Underline.ShouldBe(underline);
    }

    [
        Theory,
        InlineData(TextStrikethrough.Single),
        InlineData(TextStrikethrough.Double)
    ]
    public void RoundTrip_Strikethrough_Preserved(TextStrikethrough strike)
    {
        var frame = new TextFrame();
        frame.Paragraphs.Add().Runs.Add("s").Format.Strikethrough = strike;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Runs[0].Format.Strikethrough.ShouldBe(strike);
    }

    [
        Theory,
        InlineData(TextCapType.SmallCaps),
        InlineData(TextCapType.AllCaps)
    ]
    public void RoundTrip_Capitalisation_Preserved(TextCapType cap)
    {
        var frame = new TextFrame();
        frame.Paragraphs.Add().Runs.Add("c").Format.Capitalisation = cap;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Runs[0].Format.Capitalisation.ShouldBe(cap);
    }

    [Fact]
    public void RoundTrip_CharacterSpacingAndBaseline_Preserved()
    {
        var frame = new TextFrame();
        var run = frame.Paragraphs.Add().Runs.Add("x");
        run.Format.CharacterSpacingPoints = 2.5;
        run.Format.BaselineShiftPercent = 30;

        var result = RoundTrip(frame);
        var rf = result.Paragraphs[0].Runs[0].Format;
        rf.CharacterSpacingPoints!.Value.ShouldBe(2.5, 0.01);
        rf.BaselineShiftPercent!.Value.ShouldBe(30, 0.01);
    }

    [Fact]
    public void RoundTrip_FontVariants_Preserved()
    {
        var frame = new TextFrame();
        var run = frame.Paragraphs.Add().Runs.Add("x");
        run.Format.LatinFont = "Arial";
        run.Format.EastAsianFont = "MS Gothic";
        run.Format.ComplexScriptFont = "Arabic Typesetting";

        var result = RoundTrip(frame);
        var rf = result.Paragraphs[0].Runs[0].Format;
        rf.LatinFont.ShouldBe("Arial");
        rf.EastAsianFont.ShouldBe("MS Gothic");
        rf.ComplexScriptFont.ShouldBe("Arabic Typesetting");
    }

    [Fact]
    public void RoundTrip_RunFillColor_Preserved()
    {
        var frame = new TextFrame();
        var run = frame.Paragraphs.Add().Runs.Add("x");
        run.Format.Fill = new FillFormat();
        run.Format.Fill.SetSolid(ColorSpec.FromRgb(0x12, 0x34, 0x56));

        var result = RoundTrip(frame);
        var rf = result.Paragraphs[0].Runs[0].Format;
        rf.Fill.ShouldNotBeNull();
        rf.Fill.Solid!.Color.Resolve(null).ShouldBe(0xFF123456u);
    }

    [Fact]
    public void RoundTrip_CharacterBullet_PreservesCharFontColorSize()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add("Bulleted");
        para.Bullet.Type = BulletType.Character;
        para.Bullet.Character = "•";
        para.Bullet.Font = "Wingdings";
        para.Bullet.Color = ColorSpec.FromRgb(0xAA, 0xBB, 0xCC);
        para.Bullet.SizePercent = 120;

        var result = RoundTrip(frame);
        var b = result.Paragraphs[0].Bullet;
        b.Type.ShouldBe(BulletType.Character);
        b.Character.ShouldBe("•");
        b.Font.ShouldBe("Wingdings");
        b.Color.ShouldNotBeNull();
        b.SizePercent!.Value.ShouldBe(120, 0.5);
    }

    [Fact]
    public void RoundTrip_NoneBullet_Preserved()
    {
        var frame = new TextFrame();
        frame.Paragraphs.Add("x").Bullet.Type = BulletType.None;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Bullet.Type.ShouldBe(BulletType.None);
    }

    [
        Theory,
        InlineData(NumberedBulletStyle.ArabicPeriod),
        InlineData(NumberedBulletStyle.RomanUpperCase),
        InlineData(NumberedBulletStyle.LetterLowerCasePeriod)
    ]
    public void RoundTrip_NumberedBulletStyles_Preserved(NumberedBulletStyle style)
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add("Item");
        para.Bullet.Type = BulletType.Numbered;
        para.Bullet.Numbered = new NumberedBulletFormat { Style = style, StartAt = 2 };

        var result = RoundTrip(frame);
        result.Paragraphs[0].Bullet.Type.ShouldBe(BulletType.Numbered);
        result.Paragraphs[0].Bullet.Numbered.ShouldNotBeNull();
    }

    [Fact]
    public void RoundTrip_LineSpacingPoints_Preserved()
    {
        var frame = new TextFrame();
        frame.Paragraphs.Add("x").Spacing = LineSpacing.FromPoints(18);

        var result = RoundTrip(frame);
        result.Paragraphs[0].Spacing.ShouldNotBeNull();
        result.Paragraphs[0].Spacing!.Value.Mode.ShouldBe(LineSpacingMode.Points);
        result.Paragraphs[0].Spacing!.Value.Value.ShouldBe(18, 0.5);
    }

    [Fact]
    public void RoundTrip_ParagraphIndentMarginsAndLevel_Preserved()
    {
        var frame = new TextFrame();
        var para = frame.Paragraphs.Add("x");
        para.MarginLeft = Emu.FromPoints(10);
        para.MarginRight = Emu.FromPoints(11);
        para.Indent = Emu.FromPoints(5);
        para.OutlineLevel = 2;
        para.RightToLeft = true;

        var result = RoundTrip(frame);
        var rp = result.Paragraphs[0];
        rp.MarginLeft!.Value.Value.ShouldBe(Emu.FromPoints(10).Value);
        rp.MarginRight!.Value.Value.ShouldBe(Emu.FromPoints(11).Value);
        rp.Indent!.Value.Value.ShouldBe(Emu.FromPoints(5).Value);
        rp.OutlineLevel.ShouldBe(2);
        rp.RightToLeft.ShouldBeTrue();
    }

    [Fact]
    public void RoundTrip_DateField_Preserved()
    {
        var frame = new TextFrame();
        var run = frame.Paragraphs.Add().Runs.Add("2026");
        run.Field = FieldType.Date;

        var result = RoundTrip(frame);
        result.Paragraphs[0].Runs.Any(static r => r.Field == FieldType.Date).ShouldBeTrue();
    }
}
