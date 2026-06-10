using Unchained.Ooxml;
using Unchained.Pptx.Core.Xml;
using System.Xml.Linq;
using Unchained.Ooxml.Xml;
using Unchained.Ooxml.Text;

namespace Unchained.Pptx.Writing;

/// <summary>
/// Serializes <see cref="TextFrame"/> objects to DrawingML <c>&lt;p:txBody&gt;</c> /
/// <c>&lt;a:txBody&gt;</c> XML elements.
/// </summary>
internal static class TextWriter
{
    /// <summary>
    /// Returns a complete <c>&lt;p:txBody&gt;</c> element for the given <paramref name="frame"/>,
    /// using the PresentationML namespace for the root element name.
    /// </summary>
    public static XElement WriteAsShape(TextFrame frame) =>
        Write(frame, PmlNames.TextBody);

    /// <summary>
    /// Returns a complete <c>&lt;a:txBody&gt;</c> element for the given <paramref name="frame"/>,
    /// using the DrawingML namespace (used inside table cells).
    /// </summary>
    public static XElement WriteAsDml(TextFrame frame) =>
        Write(frame, DmlNames.TextBody);

    private static XElement Write(TextFrame frame, XName rootName)
    {
        var txBody = new XElement(rootName);
        txBody.Add(WriteBodyProperties(frame.Format));
        txBody.Add(new XElement(DmlNames.ListStyle));

        foreach (var para in frame.Paragraphs)
            txBody.Add(WriteParagraph(para));

        // Ensure at least one paragraph exists (required by OOXML spec)
        if (frame.Paragraphs.Count == 0)
            txBody.Add(new XElement(DmlNames.Paragraph,
                new XElement(DmlNames.EndParagraphRunProperties, new XAttribute(DmlNames.AttributeLanguage, "en-US"))));

        return txBody;
    }

    // ── Body properties ───────────────────────────────────────────────────────

    private static XElement WriteBodyProperties(TextFrameFormat format)
    {
        var bodyPr = new XElement(DmlNames.BodyProperties);

        if (!format.WrapText)
            bodyPr.Add(new XAttribute("wrap", "none"));

        bodyPr.Add(new XAttribute("anchor", AnchorToString(format.VerticalAnchor)));

        if (format.MarginLeft != Emu.FromPoints(TextConstants.DefaultMarginHorizontalPt))
            bodyPr.Add(new XAttribute("marL", format.MarginLeft.Value));
        if (format.MarginRight != Emu.FromPoints(TextConstants.DefaultMarginHorizontalPt))
            bodyPr.Add(new XAttribute("marR", format.MarginRight.Value));
        if (format.MarginTop != Emu.FromPoints(TextConstants.DefaultMarginVerticalPt))
            bodyPr.Add(new XAttribute("marT", format.MarginTop.Value));
        if (format.MarginBottom != Emu.FromPoints(TextConstants.DefaultMarginVerticalPt))
            bodyPr.Add(new XAttribute("marB", format.MarginBottom.Value));

        if (format.ColumnCount > 1)
        {
            bodyPr.Add(new XAttribute("numCol", format.ColumnCount));
            bodyPr.Add(new XAttribute("spcCol", format.ColumnSpacing.Value));
        }

        if (format.Direction != TextDirection.Horizontal)
            bodyPr.Add(new XAttribute("vert", DirectionToString(format.Direction)));

        // WordArt warp (<a:prstTxWarp>) precedes the autofit elements per the bodyPr schema.
        if (format.Warp is { } warp && !string.IsNullOrEmpty(warp.Preset))
            bodyPr.Add(new XElement(DmlNames.Dml + "prstTxWarp",
                new XAttribute("prst", warp.Preset),
                new XElement(DmlNames.Dml + "avLst")));

        // Autofit
        switch (format.Autofit)
        {
            case TextAutofit.ShrinkText:
                bodyPr.Add(new XElement(DmlNames.Dml + "normAutofit"));
                break;
            case TextAutofit.ResizeShape:
                bodyPr.Add(new XElement(DmlNames.Dml + "spAutoFit"));
                break;
            default:
                bodyPr.Add(new XElement(DmlNames.Dml + "noAutofit"));
                break;
        }

        return bodyPr;
    }

    // ── Paragraph ─────────────────────────────────────────────────────────────

    private static XElement WriteParagraph(Paragraph para)
    {
        var pEl = new XElement(DmlNames.Paragraph);

        var pPr = WriteParagraphProperties(para);
        if (pPr.HasElements || pPr.HasAttributes)
            pEl.Add(pPr);

        foreach (var run in para.Runs)
        {
            if (run.Field.HasValue)
                pEl.Add(WriteField(run));
            else if (run.Text == "\n")
                pEl.Add(new XElement(DmlNames.LineBreak));
            else
                pEl.Add(WriteRun(run));
        }

        pEl.Add(new XElement(DmlNames.EndParagraphRunProperties,
            new XAttribute(DmlNames.AttributeLanguage, "en-US"),
            new XAttribute(DmlNames.AttributeDirty, "0")));

        return pEl;
    }

    private static XElement WriteParagraphProperties(Paragraph para)
    {
        var pPr = new XElement(DmlNames.ParagraphProperties);

        if (para.Alignment.HasValue)
            pPr.Add(new XAttribute(DmlNames.AttributeAlignment, AlignmentToString(para.Alignment.Value)));

        if (para.MarginLeft.HasValue)
            pPr.Add(new XAttribute("marL", para.MarginLeft.Value.Value));

        if (para.MarginRight.HasValue)
            pPr.Add(new XAttribute("marR", para.MarginRight.Value.Value));

        if (para.Indent.HasValue)
            pPr.Add(new XAttribute("indent", para.Indent.Value.Value));

        if (para.OutlineLevel > 0)
            pPr.Add(new XAttribute("lvl", para.OutlineLevel));

        if (para.RightToLeft)
            pPr.Add(new XAttribute("rtl", "1"));

        if (para.SpaceBeforePoints.HasValue)
        {
            var spcBef = new XElement(DmlNames.SpaceBefore);
            spcBef.Add(new XElement(DmlNames.SpacingPoints,
                new XAttribute(DmlNames.AttributeValue, (int)(para.SpaceBeforePoints.Value * 100))));
            pPr.Add(spcBef);
        }

        if (para.SpaceAfterPoints.HasValue)
        {
            var spcAft = new XElement(DmlNames.SpaceAfter);
            spcAft.Add(new XElement(DmlNames.SpacingPoints,
                new XAttribute(DmlNames.AttributeValue, (int)(para.SpaceAfterPoints.Value * 100))));
            pPr.Add(spcAft);
        }

        if (para.Spacing.HasValue)
            pPr.Add(WriteLineSpacing(para.Spacing.Value));

        WriteBullet(pPr, para.Bullet);

        return pPr;
    }

    private static void WriteBullet(XElement pPr, BulletFormat bullet)
    {
        switch (bullet.Type)
        {
            case BulletType.None:
                pPr.Add(new XElement(DmlNames.BulletNone));
                break;
            case BulletType.Character:
                if (bullet.Character != null)
                    pPr.Add(new XElement(DmlNames.BulletChar,
                        new XAttribute("char", bullet.Character)));
                if (bullet.Font != null)
                    pPr.Add(new XElement(DmlNames.BulletFont,
                        new XAttribute(DmlNames.AttributeTypeface, bullet.Font)));
                if (bullet.Color.HasValue)
                {
                    var clr = new XElement(DmlNames.BulletColor);
                    clr.Add(ColorWriter.Write(bullet.Color.Value));
                    pPr.Add(clr);
                }
                if (bullet.SizePercent.HasValue)
                    pPr.Add(new XElement(DmlNames.BulletSizePercent,
                        new XAttribute(DmlNames.AttributeValue, (int)(bullet.SizePercent.Value * 1_000))));
                break;
            case BulletType.Numbered when bullet.Numbered != null:
                pPr.Add(new XElement(DmlNames.BulletAutoNumber,
                    new XAttribute("type", NumberedStyleToString(bullet.Numbered.Style)),
                    new XAttribute("startAt", bullet.Numbered.StartAt)));
                break;
        }
    }

    private static XElement WriteLineSpacing(LineSpacing spacing)
    {
        var lnSpc = new XElement(DmlNames.LineSpacing);
        if (spacing.Mode == LineSpacingMode.Points)
            lnSpc.Add(new XElement(DmlNames.SpacingPoints,
                new XAttribute(DmlNames.AttributeValue, (int)(spacing.Value * 100))));
        else
            lnSpc.Add(new XElement(DmlNames.SpacingPercent,
                new XAttribute(DmlNames.AttributeValue, (int)(spacing.Value * 1_000))));
        return lnSpc;
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    private static XElement WriteRun(Run run)
    {
        var rEl = new XElement(DmlNames.Run);
        rEl.Add(WriteRunProperties(run.Format));
        rEl.Add(new XElement(DmlNames.Text, run.Text));
        return rEl;
    }

    private static XElement WriteField(Run run)
    {
        var fEl = new XElement(DmlNames.Field,
            new XAttribute("id", Guid.NewGuid().ToString("B").ToUpperInvariant()),
            new XAttribute("type", FieldTypeToString(run.Field!.Value)));
        fEl.Add(WriteRunProperties(run.Format));
        fEl.Add(new XElement(DmlNames.Text, run.Text));
        return fEl;
    }

    private static XElement WriteRunProperties(RunFormat format)
    {
        var rPr = new XElement(DmlNames.RunProperties,
            new XAttribute(DmlNames.AttributeLanguage, format.LanguageTag ?? "en-US"),
            new XAttribute(DmlNames.AttributeDirty, "0"));

        if (format.FontSizePoints.HasValue)
            rPr.Add(new XAttribute(DmlNames.AttributeFontSize, (int)(format.FontSizePoints.Value * 100)));

        if (format.Bold.IsSet)
            rPr.Add(new XAttribute(DmlNames.AttributeBold, format.Bold.Value!.Value ? "1" : "0"));

        if (format.Italic.IsSet)
            rPr.Add(new XAttribute(DmlNames.AttributeItalic, format.Italic.Value!.Value ? "1" : "0"));

        if (format.Underline != TextUnderlineType.None)
            rPr.Add(new XAttribute(DmlNames.AttributeUnderline, UnderlineToString(format.Underline)));

        if (format.Strikethrough != TextStrikethrough.None)
            rPr.Add(new XAttribute(DmlNames.AttributeStrike, StrikethroughToString(format.Strikethrough)));

        if (format.Capitalisation != TextCapType.None)
            rPr.Add(new XAttribute("cap", CapTypeToString(format.Capitalisation)));

        if (format.CharacterSpacingPoints.HasValue)
            rPr.Add(new XAttribute("spc", (int)(format.CharacterSpacingPoints.Value * 100)));

        if (format.BaselineShiftPercent.HasValue)
            rPr.Add(new XAttribute("baseline", (int)(format.BaselineShiftPercent.Value * 1_000)));

        // WordArt glyph outline (<a:ln>) precedes the fill per the rPr schema order.
        if (format.Outline != null)
            LineWriter.Write(rPr, format.Outline);

        // Fill (text colour)
        if (format.Fill != null)
            FillWriter.Write(rPr, format.Fill);

        // Text effects (<a:effectLst>) follow the fill, before the font elements.
        if (EffectWriter.Write(format.Effects) is { } runEffects)
            rPr.Add(runEffects);

        if (format.LatinFont != null)
            rPr.Add(new XElement(DmlNames.LatinFont,
                new XAttribute(DmlNames.AttributeTypeface, format.LatinFont)));

        if (format.EastAsianFont != null)
            rPr.Add(new XElement(DmlNames.EastAsianFont,
                new XAttribute(DmlNames.AttributeTypeface, format.EastAsianFont)));

        if (format.ComplexScriptFont != null)
            rPr.Add(new XElement(DmlNames.ComplexScriptFont,
                new XAttribute(DmlNames.AttributeTypeface, format.ComplexScriptFont)));

        // Click hyperlink (<a:hlinkClick>) follows the font elements per the rPr schema order.
        // The relationship id is assigned by PresentationWriter before this runs.
        if (format.Hyperlink is { } link)
        {
            var hlink = new XElement(DmlNames.HyperlinkClick,
                new XAttribute(Core.Xml.PmlNames.Relationships + "id", link.RelationshipId));
            if (!string.IsNullOrEmpty(link.Tooltip))
                hlink.Add(new XAttribute("tooltip", link.Tooltip));
            rPr.Add(hlink);
        }

        return rPr;
    }

    // ── Enum → string ─────────────────────────────────────────────────────────

    private static string AnchorToString(TextAnchor anchor) => anchor switch
    {
        TextAnchor.Middle => "ctr",
        TextAnchor.Bottom => "b",
        _ => "t"
    };

    private static string DirectionToString(TextDirection direction) => direction switch
    {
        TextDirection.Vertical90 => "vert",
        TextDirection.Vertical270 => "vert270",
        TextDirection.Stacked => "mongolianVert",
        _ => "horz"
    };

    private static string AlignmentToString(TextAlignment alignment) => alignment switch
    {
        TextAlignment.Center => "ctr",
        TextAlignment.Right => "r",
        TextAlignment.Justify => "just",
        TextAlignment.JustifyLow => "justLow",
        TextAlignment.Distributed => "dist",
        TextAlignment.ThaiDistributed => "thaiDist",
        _ => "l"
    };

    private static string UnderlineToString(TextUnderlineType underline) => underline switch
    {
        TextUnderlineType.Single => "sng",
        TextUnderlineType.Double => "dbl",
        TextUnderlineType.Heavy => "heavy",
        TextUnderlineType.Dotted => "dotted",
        TextUnderlineType.DottedHeavy => "dottedHeavy",
        TextUnderlineType.Dash => "dash",
        TextUnderlineType.DashHeavy => "dashHeavy",
        TextUnderlineType.DashLong => "dashLong",
        TextUnderlineType.DashLongHeavy => "dashLongHeavy",
        TextUnderlineType.DotDash => "dotDash",
        TextUnderlineType.DotDashHeavy => "dotDashHeavy",
        TextUnderlineType.DotDotDash => "dotDotDash",
        TextUnderlineType.DotDotDashHeavy => "dotDotDashHeavy",
        TextUnderlineType.Wavy => "wavy",
        TextUnderlineType.WavyHeavy => "wavyHeavy",
        TextUnderlineType.WavyDouble => "wavyDbl",
        TextUnderlineType.Words => "words",
        _ => "none"
    };

    private static string StrikethroughToString(TextStrikethrough strike) => strike switch
    {
        TextStrikethrough.Single => "sngStrike",
        TextStrikethrough.Double => "dblStrike",
        _ => "noStrike"
    };

    private static string CapTypeToString(TextCapType cap) => cap switch
    {
        TextCapType.SmallCaps => "small",
        TextCapType.AllCaps => "all",
        _ => "none"
    };

    private static string NumberedStyleToString(NumberedBulletStyle style) => style switch
    {
        NumberedBulletStyle.ArabicPeriod => "arabicPeriod",
        NumberedBulletStyle.ArabicParenthesis => "arabicParenR",
        NumberedBulletStyle.RomanUpperCase => "romanUcPeriod",
        NumberedBulletStyle.RomanLowerCase => "romanLcPeriod",
        NumberedBulletStyle.LetterUpperCasePeriod => "alphaUcPeriod",
        NumberedBulletStyle.LetterLowerCasePeriod => "alphaLcPeriod",
        NumberedBulletStyle.LetterUpperCase => "alphaUcParenR",
        NumberedBulletStyle.LetterLowerCase => "alphaLcParenR",
        _ => "arabicPeriod"
    };

    private static string FieldTypeToString(FieldType fieldType) => fieldType switch
    {
        FieldType.SlideNumber => "slidenum",
        FieldType.Date => "datetime1",
        FieldType.Time => "datetime",
        FieldType.TotalSlides => "slidenum",
        _ => "slidenum"
    };
}
