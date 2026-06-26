using System.Xml.Linq;
using Shouldly;
using Unchained.Ooxml;
using Unchained.Ooxml.Text;
using Unchained.Ooxml.Xml;
using Unchained.Pptx.Parsing;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Parsing;

/// <summary>
///     Unit tests for <see cref="TextParser" /> driving body-property, paragraph-property, run,
///     field, line-break, bullet, and spacing branches by constructing minimal <c>&lt;a:txBody&gt;</c>
///     XML directly.
/// </summary>
public sealed class TextParserUnitTests
{
    private static readonly XNamespace A = DmlNames.Dml;

    [Fact]
    public void Parse_MissingTxBody_ReturnsNull()
    {
        var parent = new XElement(A + "wrap");
        TextParser.Parse(parent, DmlNames.TextBody).ShouldBeNull();
    }

    [Fact]
    public void ParseTextBody_BodyProperties_AreParsed()
    {
        var bodyPr = new XElement(
            DmlNames.BodyProperties,
            new XAttribute("anchor", "ctr"),
            new XAttribute("wrap", "none"),
            new XAttribute("marL", "91440"),
            new XAttribute("marR", "91440"),
            new XAttribute("marT", "45720"),
            new XAttribute("marB", "45720"),
            new XAttribute("numCol", "2"),
            new XAttribute("spcCol", "360000"),
            new XAttribute("vert", "vert270"),
            new XElement(A + "normAutofit"),
            new XElement(A + "prstTxWarp", new XAttribute("prst", "textArchUp"))
        );
        var txBody = new XElement(DmlNames.TextBody, bodyPr);

        var frame = TextParser.ParseTextBody(txBody);

        frame.Format.WrapText.ShouldBeFalse();
        frame.Format.ColumnCount.ShouldBe(2);
        frame.Format.Autofit.ShouldBe(TextAutofit.ShrinkText);
        frame.Format.Warp.ShouldNotBeNull();
        frame.Format.Warp!.Preset.ShouldBe("textArchUp");
    }

    [Fact]
    public void ParseTextBody_SpAutoFit_SetsResizeShape()
    {
        var txBody = new XElement(
            DmlNames.TextBody,
            new XElement(DmlNames.BodyProperties, new XElement(A + "spAutoFit"))
        );
        TextParser.ParseTextBody(txBody).Format.Autofit.ShouldBe(TextAutofit.ResizeShape);
    }

    [Fact]
    public void ParseTextBody_Run_WithFormatting_IsParsed()
    {
        var rPr = new XElement(
            DmlNames.RunProperties,
            new XAttribute("lang", "en-GB"),
            new XAttribute("sz", "1800"),
            new XAttribute("b", "1"),
            new XAttribute("i", "1"),
            new XAttribute("u", "sng"),
            new XAttribute("strike", "sngStrike")
        );
        var run = new XElement(DmlNames.Run, rPr, new XElement(DmlNames.Text, "Hello"));
        var para = new XElement(DmlNames.Paragraph, run);
        var txBody = new XElement(DmlNames.TextBody, para);

        var frame = TextParser.ParseTextBody(txBody);

        var r = frame.Paragraphs[0].Runs[0];
        r.Text.ShouldBe("Hello");
        r.Format.LanguageTag.ShouldBe("en-GB");
        r.Format.FontSizePoints.ShouldBe(18.0);
        r.Format.Bold.ShouldBe(InheritableBool.True);
        r.Format.Italic.ShouldBe(InheritableBool.True);
    }

    [Fact]
    public void ParseTextBody_FieldAndLineBreak_AreParsed()
    {
        var field = new XElement(
            DmlNames.Field,
            new XAttribute("type", "slidenum"),
            new XElement(DmlNames.Text, "3")
        );
        var br = new XElement(DmlNames.LineBreak);
        var para = new XElement(DmlNames.Paragraph, field, br);
        var txBody = new XElement(DmlNames.TextBody, para);

        var runs = TextParser.ParseTextBody(txBody).Paragraphs[0].Runs;
        runs.Count.ShouldBe(2);
        runs[0].Field.ShouldNotBeNull();
        runs[1].Text.ShouldBe("\n");
    }

    [Fact]
    public void ParseTextBody_ParagraphProperties_AreParsed()
    {
        var pPr = new XElement(
            DmlNames.ParagraphProperties,
            new XAttribute("algn", "ctr"),
            new XAttribute("marL", "457200"),
            new XAttribute("marR", "228600"),
            new XAttribute("indent", "-228600"),
            new XAttribute("lvl", "2"),
            new XAttribute("rtl", "1"),
            new XElement(DmlNames.LineSpacing, new XElement(A + "spcPct", new XAttribute(DmlNames.AttributeValue, "150000"))),
            new XElement(DmlNames.SpaceBefore, new XElement(A + "spcPts", new XAttribute(DmlNames.AttributeValue, "1200"))),
            new XElement(DmlNames.SpaceAfter, new XElement(A + "spcPts", new XAttribute(DmlNames.AttributeValue, "600")))
        );
        var para = new XElement(DmlNames.Paragraph, pPr, new XElement(DmlNames.Run, new XElement(DmlNames.Text, "x")));
        var txBody = new XElement(DmlNames.TextBody, para);

        var p = TextParser.ParseTextBody(txBody).Paragraphs[0];
        p.OutlineLevel.ShouldBe(2);
        p.RightToLeft.ShouldBeTrue();
        p.MarginLeft.ShouldNotBeNull();
        p.Indent.ShouldNotBeNull();
        p.Spacing.ShouldNotBeNull();
        p.SpaceBeforePoints.ShouldNotBeNull();
        p.SpaceAfterPoints.ShouldNotBeNull();
    }

    [Fact]
    public void ParseTextBody_CharBullet_IsParsed()
    {
        var pPr = new XElement(
            DmlNames.ParagraphProperties,
            new XElement(A + "buChar", new XAttribute("char", "•"))
        );
        var para = new XElement(DmlNames.Paragraph, pPr, new XElement(DmlNames.Run, new XElement(DmlNames.Text, "item")));
        var txBody = new XElement(DmlNames.TextBody, para);

        var bullet = TextParser.ParseTextBody(txBody).Paragraphs[0].Bullet;
        bullet.ShouldNotBeNull();
    }

    [Fact]
    public void ParseTextBody_NoBullet_IsParsed()
    {
        var pPr = new XElement(DmlNames.ParagraphProperties, new XElement(A + "buNone"));
        var para = new XElement(DmlNames.Paragraph, pPr, new XElement(DmlNames.Run, new XElement(DmlNames.Text, "x")));
        var txBody = new XElement(DmlNames.TextBody, para);

        Should.NotThrow(() => TextParser.ParseTextBody(txBody));
    }

    [
        Theory,
        InlineData("l"),
        InlineData("ctr"),
        InlineData("r"),
        InlineData("just")
    ]
    public void ParseTextBody_Alignments_AreParsed(string algn)
    {
        var pPr = new XElement(DmlNames.ParagraphProperties, new XAttribute("algn", algn));
        var para = new XElement(DmlNames.Paragraph, pPr, new XElement(DmlNames.Run, new XElement(DmlNames.Text, "x")));
        var txBody = new XElement(DmlNames.TextBody, para);

        TextParser.ParseTextBody(txBody).Paragraphs[0].Alignment.ShouldNotBeNull();
    }

    [Fact]
    public void ParseTextBody_AutoNumberBullet_IsParsed()
    {
        var pPr = new XElement(
            DmlNames.ParagraphProperties,
            new XElement(A + "buFont", new XAttribute("typeface", "Arial")),
            new XElement(A + "buClr", new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "FF0000"))),
            new XElement(A + "buSzPct", new XAttribute(DmlNames.AttributeValue, "75000")),
            new XElement(A + "buAutoNum", new XAttribute("type", "romanUcPeriod"), new XAttribute("startAt", "3"))
        );
        var para = new XElement(DmlNames.Paragraph, pPr, new XElement(DmlNames.Run, new XElement(DmlNames.Text, "x")));
        var txBody = new XElement(DmlNames.TextBody, para);

        var bullet = TextParser.ParseTextBody(txBody).Paragraphs[0].Bullet;
        bullet.Type.ShouldBe(BulletType.Numbered);
        bullet.Numbered.ShouldNotBeNull();
        bullet.Font.ShouldBe("Arial");
        bullet.SizePercent.ShouldBe(75.0);
    }

    [Fact]
    public void ParseTextBody_PictureBullet_IsParsed()
    {
        var pPr = new XElement(DmlNames.ParagraphProperties, new XElement(A + "buBlip"));
        var para = new XElement(DmlNames.Paragraph, pPr, new XElement(DmlNames.Run, new XElement(DmlNames.Text, "x")));
        var txBody = new XElement(DmlNames.TextBody, para);

        TextParser.ParseTextBody(txBody).Paragraphs[0].Bullet.Type.ShouldBe(BulletType.Picture);
    }

    [Fact]
    public void ParseTextBody_RunCapsUnderlineFontsAndFill_AreParsed()
    {
        var rPr = new XElement(
            DmlNames.RunProperties,
            new XAttribute("u", "dotted"),
            new XAttribute("strike", "dblStrike"),
            new XAttribute("cap", "all"),
            new XAttribute("spc", "150"),
            new XAttribute("baseline", "30000"),
            new XElement(DmlNames.LatinFont, new XAttribute("typeface", "Calibri")),
            new XElement(DmlNames.SolidFill, new XElement(DmlNames.SrgbColor, new XAttribute(DmlNames.AttributeValue, "00FF00")))
        );
        var run = new XElement(DmlNames.Run, rPr, new XElement(DmlNames.Text, "styled"));
        var para = new XElement(DmlNames.Paragraph, run);
        var txBody = new XElement(DmlNames.TextBody, para);

        var fmt = TextParser.ParseTextBody(txBody).Paragraphs[0].Runs[0].Format;
        fmt.LatinFont.ShouldBe("Calibri");
        fmt.Fill.ShouldNotBeNull();
        fmt.Capitalisation.ShouldBe(TextCapType.AllCaps);
    }

    [Fact]
    public void ParseTextBody_PointsLineSpacing_IsParsed()
    {
        var pPr = new XElement(
            DmlNames.ParagraphProperties,
            new XElement(DmlNames.LineSpacing, new XElement(A + "spcPts", new XAttribute(DmlNames.AttributeValue, "2400")))
        );
        var para = new XElement(DmlNames.Paragraph, pPr, new XElement(DmlNames.Run, new XElement(DmlNames.Text, "x")));
        var txBody = new XElement(DmlNames.TextBody, para);

        TextParser.ParseTextBody(txBody).Paragraphs[0].Spacing.ShouldNotBeNull();
    }
}
