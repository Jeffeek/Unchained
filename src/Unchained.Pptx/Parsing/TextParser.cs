using System.Xml.Linq;
using Unchained.Ooxml;
using Unchained.Ooxml.Xml;
using Unchained.Ooxml.Text;
using Unchained.Ooxml.Drawing;

namespace Unchained.Pptx.Parsing;

/// <summary>
/// Parses DrawingML text body elements (<c>&lt;a:txBody&gt;</c> / <c>&lt;p:txBody&gt;</c>)
/// into <see cref="TextFrame"/> objects.
/// </summary>
internal static class TextParser
{
    /// <summary>
    /// Reads a <c>&lt;p:txBody&gt;</c> or <c>&lt;a:txBody&gt;</c> element and returns
    /// a populated <see cref="TextFrame"/>. Returns <see langword="null"/> when the element
    /// is absent.
    /// </summary>
    public static TextFrame? Parse(XElement parent, XName textBodyName)
    {
        var txBody = parent.Element(textBodyName);
        return txBody == null ? null : ParseTextBody(txBody);
    }

    /// <summary>Parses a text body element directly.</summary>
    public static TextFrame ParseTextBody(XElement txBody)
    {
        var frame = new TextFrame();
        ParseBodyProperties(txBody.Element(DmlNames.BodyProperties), frame.Format);

        foreach (var pEl in txBody.Elements(DmlNames.Paragraph))
        {
            var para = ParseParagraph(pEl);
            frame.Paragraphs.Add(para);
        }

        return frame;
    }

    // ── Sub-parsers ───────────────────────────────────────────────────────────

    private static void ParseBodyProperties(XElement? bodyPr, TextFrameFormat format)
    {
        if (bodyPr == null) return;

        var anchor = bodyPr.GetAttr("anchor");
        if (anchor != null)
            format.VerticalAnchor = ParseAnchor(anchor);

        var wrap = bodyPr.GetAttr("wrap");
        if (wrap != null)
            format.WrapText = wrap != "none";

        var marL = bodyPr.GetAttrLong("marL");
        if (marL.HasValue) format.MarginLeft = new Emu(marL.Value);

        var marR = bodyPr.GetAttrLong("marR");
        if (marR.HasValue) format.MarginRight = new Emu(marR.Value);

        var marT = bodyPr.GetAttrLong("marT");
        if (marT.HasValue) format.MarginTop = new Emu(marT.Value);

        var marB = bodyPr.GetAttrLong("marB");
        if (marB.HasValue) format.MarginBottom = new Emu(marB.Value);

        var numCol = bodyPr.GetAttrInt("numCol");
        if (numCol.HasValue) format.ColumnCount = numCol.Value;

        var spcCol = bodyPr.GetAttrLong("spcCol");
        if (spcCol.HasValue) format.ColumnSpacing = new Emu(spcCol.Value);

        var vert = bodyPr.GetAttr("vert");
        if (vert != null) format.Direction = ParseTextDirection(vert);

        // Autofit
        if (bodyPr.Element(DmlNames.Dml + "normAutofit") != null)
            format.Autofit = TextAutofit.ShrinkText;
        else if (bodyPr.Element(DmlNames.Dml + "spAutoFit") != null)
            format.Autofit = TextAutofit.ResizeShape;
        else
            format.Autofit = TextAutofit.None;

        // WordArt text warp (<a:prstTxWarp prst="...">).
        var warp = bodyPr.Element(DmlNames.Dml + "prstTxWarp");
        var warpPreset = warp?.GetAttr("prst");
        if (!string.IsNullOrEmpty(warpPreset))
            format.Warp = new Unchained.Ooxml.Drawing.TextWarpFormat { Preset = warpPreset };
    }

    private static Paragraph ParseParagraph(XElement pEl)
    {
        var para = new Paragraph();

        var pPr = pEl.Element(DmlNames.ParagraphProperties);
        if (pPr != null)
            ParseParagraphProperties(pPr, para);

        foreach (var child in pEl.Elements())
        {
            if (child.Name == DmlNames.Run)
                para.Runs.Add(ParseRun(child));
            else if (child.Name == DmlNames.Field)
                para.Runs.Add(ParseField(child));
            else if (child.Name == DmlNames.LineBreak)
                para.Runs.Add(new Run { Text = "\n" } );
        }

        return para;
    }

    private static void ParseParagraphProperties(XElement pPr, Paragraph para)
    {
        var algn = pPr.GetAttr(DmlNames.AttributeAlignment);
        if (algn != null) para.Alignment = ParseAlignment(algn);

        var marL = pPr.GetAttrLong("marL");
        if (marL.HasValue) para.MarginLeft = new Emu(marL.Value);

        var marR = pPr.GetAttrLong("marR");
        if (marR.HasValue) para.MarginRight = new Emu(marR.Value);

        var indent = pPr.GetAttrLong("indent");
        if (indent.HasValue) para.Indent = new Emu(indent.Value);

        var lvl = pPr.GetAttrInt("lvl");
        if (lvl.HasValue) para.OutlineLevel = lvl.Value;

        var rtl = pPr.GetAttrBool("rtl");
        if (rtl.HasValue) para.RightToLeft = rtl.Value;

        // Line spacing
        var lnSpc = pPr.Element(DmlNames.LineSpacing);
        if (lnSpc != null) para.Spacing = ParseSpacing(lnSpc);

        // Space before / after
        var spcBef = pPr.Element(DmlNames.SpaceBefore);
        if (spcBef != null) para.SpaceBeforePoints = ParseSpacingPoints(spcBef);

        var spcAft = pPr.Element(DmlNames.SpaceAfter);
        if (spcAft != null) para.SpaceAfterPoints = ParseSpacingPoints(spcAft);

        // Bullets
        ParseBullet(pPr, para.Bullet);
    }

    private static Run ParseRun(XElement rEl)
    {
        var run = new Run
        {
            Text = rEl.Element(DmlNames.Text)?.Value ?? string.Empty
        };
        var rPr = rEl.Element(DmlNames.RunProperties);
        if (rPr != null)
            ParseRunProperties(rPr, run.Format);
        return run;
    }

    private static Run ParseField(XElement fEl)
    {
        var run = new Run
        {
            Text = fEl.Element(DmlNames.Text)?.Value ?? string.Empty,
            Field = ParseFieldType(fEl.GetAttr("type"))
        };
        var rPr = fEl.Element(DmlNames.RunProperties);
        if (rPr != null)
            ParseRunProperties(rPr, run.Format);
        return run;
    }

    private static void ParseRunProperties(XElement rPr, RunFormat format)
    {
        var lang = rPr.GetAttr(DmlNames.AttributeLanguage);
        if (lang != null) format.LanguageTag = lang;

        var sz = rPr.GetAttrInt(DmlNames.AttributeFontSize);
        if (sz.HasValue) format.FontSizePoints = sz.Value / 100.0;

        var bold = rPr.GetAttrBool(DmlNames.AttributeBold);
        format.Bold = InheritableBool.From(bold);

        var italic = rPr.GetAttrBool(DmlNames.AttributeItalic);
        format.Italic = InheritableBool.From(italic);

        var underline = rPr.GetAttr(DmlNames.AttributeUnderline);
        if (underline != null) format.Underline = ParseUnderline(underline);

        var strike = rPr.GetAttr(DmlNames.AttributeStrike);
        if (strike != null) format.Strikethrough = ParseStrikethrough(strike);

        var caps = rPr.GetAttr("cap");
        if (caps != null) format.Capitalisation = ParseCapType(caps);

        var latin = rPr.Element(DmlNames.LatinFont);
        if (latin != null) format.LatinFont = latin.GetAttr(DmlNames.AttributeTypeface);

        var ea = rPr.Element(DmlNames.EastAsianFont);
        if (ea != null) format.EastAsianFont = ea.GetAttr(DmlNames.AttributeTypeface);

        var cs = rPr.Element(DmlNames.ComplexScriptFont);
        if (cs != null) format.ComplexScriptFont = cs.GetAttr(DmlNames.AttributeTypeface);

        var spc = rPr.GetAttrInt("spc");
        if (spc.HasValue) format.CharacterSpacingPoints = spc.Value / 100.0;

        var baseline = rPr.GetAttrInt("baseline");
        if (baseline.HasValue) format.BaselineShiftPercent = baseline.Value / 1_000.0;

        // Text fill colour
        var solidFill = rPr.Element(DmlNames.SolidFill);
        if (solidFill != null)
        {
            format.Fill ??= new FillFormat();
            format.Fill.SetSolid(ColorParser.Parse(solidFill));
        }

        // WordArt: glyph outline (<a:ln>) and text effects (<a:effectLst>).
        if (rPr.Element(DmlNames.Line) != null)
        {
            var outline = new LineFormat();
            LineParser.Parse(rPr, outline);
            format.Outline = outline;
        }
        EffectParser.Parse(rPr, format.Effects);

        // Click hyperlink (<a:hlinkClick>) — capture relationship id + tooltip; the URL/slide
        // target is resolved against the slide's relationships in a second pass (SlideParser).
        var hlink = rPr.Element(DmlNames.HyperlinkClick);
        if (hlink != null)
        {
            format.Hyperlink = new RunHyperlink
            {
                RelationshipId = (string?)hlink.Attribute(Core.Xml.PmlNames.Relationships + "id") ?? string.Empty,
                Tooltip = (string?)hlink.Attribute("tooltip"),
            };
        }
    }

    private static void ParseBullet(XElement pPr, BulletFormat bullet)
    {
        if (pPr.Element(DmlNames.BulletNone) != null)
        {
            bullet.Type = BulletType.None;
            return;
        }

        var buChar = pPr.Element(DmlNames.BulletChar);
        if (buChar != null)
        {
            bullet.Type = BulletType.Character;
            bullet.Character = buChar.GetAttr("char");
        }

        var buAutoNum = pPr.Element(DmlNames.BulletAutoNumber);
        if (buAutoNum != null)
        {
            bullet.Type = BulletType.Numbered;
            bullet.Numbered = new NumberedBulletFormat
            {
                Style = ParseNumberedBulletStyle(buAutoNum.GetAttr("type", "arabicPeriod")),
                StartAt = buAutoNum.GetAttrInt("startAt", 1)
            };
        }

        var buBlip = pPr.Element(DmlNames.BulletBlip);
        if (buBlip != null)
            bullet.Type = BulletType.Picture;

        var buFont = pPr.Element(DmlNames.BulletFont);
        if (buFont != null)
            bullet.Font = buFont.GetAttr(DmlNames.AttributeTypeface);

        var buClr = pPr.Element(DmlNames.BulletColor);
        if (buClr != null)
        {
            var color = ColorParser.Parse(buClr);
            bullet.Color = color;
        }

        var buSzPct = pPr.Element(DmlNames.BulletSizePercent);
        if (buSzPct != null)
        {
            var val = buSzPct.GetAttrInt(DmlNames.AttributeValue);
            if (val.HasValue)
                bullet.SizePercent = val.Value / 1_000.0;
        }
    }

    // ── Spacing ───────────────────────────────────────────────────────────────

    private static LineSpacing? ParseSpacing(XElement lnSpcEl)
    {
        var pts = lnSpcEl.Element(DmlNames.SpacingPoints);
        if (pts != null)
        {
            var val = pts.GetAttrInt(DmlNames.AttributeValue);
            if (val.HasValue)
                return LineSpacing.FromPoints(val.Value / 100.0);
        }

        var pct = lnSpcEl.Element(DmlNames.SpacingPercent);
        if (pct != null)
        {
            var val = pct.GetAttrInt(DmlNames.AttributeValue);
            if (val.HasValue)
                return LineSpacing.FromPercent(val.Value / 1_000.0);
        }

        return null;
    }

    private static double? ParseSpacingPoints(XElement spcEl)
    {
        var pts = spcEl.Element(DmlNames.SpacingPoints);
        if (pts != null)
        {
            var val = pts.GetAttrInt(DmlNames.AttributeValue);
            if (val.HasValue) return val.Value / 100.0;
        }

        return null;
    }

    // ── Enum parsers ──────────────────────────────────────────────────────────

    private static TextAnchor ParseAnchor(string value) => value switch
    {
        "t" => TextAnchor.Top,
        "ctr" => TextAnchor.Middle,
        "b" => TextAnchor.Bottom,
        _ => TextAnchor.Top
    };

    private static TextDirection ParseTextDirection(string value) => value switch
    {
        "vert" or "eaVert" => TextDirection.Vertical90,
        "vert270" => TextDirection.Vertical270,
        "mongolianVert" => TextDirection.Stacked,
        _ => TextDirection.Horizontal
    };

    private static TextAlignment ParseAlignment(string value) => value switch
    {
        "l" => TextAlignment.Left,
        "ctr" => TextAlignment.Center,
        "r" => TextAlignment.Right,
        "just" => TextAlignment.Justify,
        "justLow" => TextAlignment.JustifyLow,
        "dist" => TextAlignment.Distributed,
        "thaiDist" => TextAlignment.ThaiDistributed,
        _ => TextAlignment.Left
    };

    private static TextUnderlineType ParseUnderline(string value) => value switch
    {
        "sng" => TextUnderlineType.Single,
        "dbl" => TextUnderlineType.Double,
        "heavy" => TextUnderlineType.Heavy,
        "dotted" => TextUnderlineType.Dotted,
        "dottedHeavy" => TextUnderlineType.DottedHeavy,
        "dash" => TextUnderlineType.Dash,
        "dashHeavy" => TextUnderlineType.DashHeavy,
        "dashLong" => TextUnderlineType.DashLong,
        "dashLongHeavy" => TextUnderlineType.DashLongHeavy,
        "dotDash" => TextUnderlineType.DotDash,
        "dotDashHeavy" => TextUnderlineType.DotDashHeavy,
        "dotDotDash" => TextUnderlineType.DotDotDash,
        "dotDotDashHeavy" => TextUnderlineType.DotDotDashHeavy,
        "wavy" => TextUnderlineType.Wavy,
        "wavyHeavy" => TextUnderlineType.WavyHeavy,
        "wavyDbl" => TextUnderlineType.WavyDouble,
        "words" => TextUnderlineType.Words,
        _ => TextUnderlineType.None
    };

    private static TextStrikethrough ParseStrikethrough(string value) => value switch
    {
        "sngStrike" => TextStrikethrough.Single,
        "dblStrike" => TextStrikethrough.Double,
        _ => TextStrikethrough.None
    };

    private static TextCapType ParseCapType(string value) => value switch
    {
        "small" => TextCapType.SmallCaps,
        "all" => TextCapType.AllCaps,
        _ => TextCapType.None
    };

    private static NumberedBulletStyle ParseNumberedBulletStyle(string value) => value switch
    {
        "arabicPeriod" => NumberedBulletStyle.ArabicPeriod,
        "arabicParenR" => NumberedBulletStyle.ArabicParenthesis,
        "arabic1Minus" or "arabic2Minus" => NumberedBulletStyle.Arabic,
        "romanUcPeriod" => NumberedBulletStyle.RomanUpperCase,
        "romanLcPeriod" => NumberedBulletStyle.RomanLowerCase,
        "alphaUcPeriod" => NumberedBulletStyle.LetterUpperCasePeriod,
        "alphaLcPeriod" => NumberedBulletStyle.LetterLowerCasePeriod,
        "alphaUcParenR" => NumberedBulletStyle.LetterUpperCase,
        "alphaLcParenR" => NumberedBulletStyle.LetterLowerCase,
        _ => NumberedBulletStyle.Arabic
    };

    private static FieldType? ParseFieldType(string? value) => value switch
    {
        "slidenum" => FieldType.SlideNumber,
        "datetime" or "datetime1" or "datetime2" or "datetime3" => FieldType.Date,
        _ => null
    };
}
