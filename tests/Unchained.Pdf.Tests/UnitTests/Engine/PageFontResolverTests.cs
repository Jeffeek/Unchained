using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Direct unit tests for <see cref="PageFontResolver" />: the resource-name → base-font map,
///     embedded font program extraction (including the Type0 → descendant CIDFont descriptor walk),
///     composite-font metadata (Identity encoding, explicit CIDToGIDMap, /W widths and /DW default),
///     Type3 glyph procedures and encodings, and ToUnicode CMap parsing (bfchar and bfrange).
/// </summary>
public sealed class PageFontResolverTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfDictionary Page(PdfObject fontDict)
    {
        var resources = new PdfDictionary(new Dictionary<string, PdfObject> { ["Font"] = fontDict });
        return new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );
    }

    private static PdfDictionary FontDict(params (string Name, PdfObject Font)[] fonts)
    {
        var d = new Dictionary<string, PdfObject>();
        foreach (var (name, font) in fonts) d[name] = font;
        return new PdfDictionary(d);
    }

    private static PdfStream Stream(string content, params (string Key, PdfObject Value)[] entries)
    {
        var d = new Dictionary<string, PdfObject> { ["Length"] = new PdfInteger(content.Length) };
        foreach (var (k, v) in entries) d[k] = v;
        return new PdfStream(new PdfDictionary(d), Encoding.Latin1.GetBytes(content));
    }

    // ── ResolveFontNames ──────────────────────────────────────────────────────

    [Fact]
    public void ResolveFontNames_MapsResourceNameToBaseFont()
    {
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["BaseFont"] = PdfName.Get("Helvetica") });
        var result = PageFontResolver.ResolveFontNames(Page(FontDict(("F1", font))), Core());
        result["F1"].ShouldBe("Helvetica");
    }

    [Fact]
    public void ResolveFontNames_NoFontDict_ReturnsEmpty()
    {
        var page = new PdfDictionary(new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page });
        PageFontResolver.ResolveFontNames(page, Core()).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveFontNames_FontWithoutBaseFont_IsSkipped()
    {
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Type1") });
        PageFontResolver.ResolveFontNames(Page(FontDict(("F1", font))), Core()).ContainsKey("F1").ShouldBeFalse();
    }

    // ── GetEmbeddedFontBytes ──────────────────────────────────────────────────

    [Fact]
    public void GetEmbeddedFontBytes_SimpleFontWithFontFile2_DecodesBytes()
    {
        var descriptor = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["FontFile2"] = Stream("TRUETYPEDATA") }
        );
        var font = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("TrueType"),
                ["FontDescriptor"] = descriptor
            }
        );
        var result = PageFontResolver.GetEmbeddedFontBytes(Page(FontDict(("F1", font))), Core());
        result["F1"].ShouldNotBeNull();
        Encoding.Latin1.GetString(result["F1"]!).ShouldBe("TRUETYPEDATA");
    }

    [Fact]
    public void GetEmbeddedFontBytes_NoDescriptor_YieldsNull()
    {
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Type1") });
        PageFontResolver.GetEmbeddedFontBytes(Page(FontDict(("F1", font))), Core())["F1"].ShouldBeNull();
    }

    [Fact]
    public void GetEmbeddedFontBytes_DescriptorWithoutFontFile_YieldsNull()
    {
        var font = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["FontDescriptor"] = new PdfDictionary() }
        );
        PageFontResolver.GetEmbeddedFontBytes(Page(FontDict(("F1", font))), Core())["F1"].ShouldBeNull();
    }

    [Fact]
    public void GetEmbeddedFontBytes_NullFontEntry_YieldsNull() =>
        PageFontResolver.GetEmbeddedFontBytes(Page(FontDict(("F1", PdfNull.Instance))), Core())["F1"].ShouldBeNull();

    [Fact]
    public void GetEmbeddedFontBytes_Type0_WalksDescendantDescriptor()
    {
        var descriptor = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["FontFile3"] = Stream("CFFDATA") }
        );
        var cidFont = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("CIDFontType0"),
                ["FontDescriptor"] = descriptor
            }
        );
        var type0 = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("Type0"),
                ["DescendantFonts"] = new PdfArray([cidFont])
            }
        );
        var result = PageFontResolver.GetEmbeddedFontBytes(Page(FontDict(("F1", type0))), Core());
        Encoding.Latin1.GetString(result["F1"]!).ShouldBe("CFFDATA");
    }

    // ── GetCompositeFonts ─────────────────────────────────────────────────────

    [Fact]
    public void GetCompositeFonts_IdentityEncoding_NoExplicitCidMap()
    {
        var cidFont = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("CIDFontType2"),
                ["DW"] = new PdfInteger(500)
            }
        );
        var type0 = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("Type0"),
                ["Encoding"] = PdfName.Get("Identity-H"),
                ["DescendantFonts"] = new PdfArray([cidFont])
            }
        );
        var info = PageFontResolver.GetCompositeFonts(Page(FontDict(("F1", type0))), Core())["F1"];
        info.IdentityEncoding.ShouldBeTrue();
        info.IdentityCidToGid.ShouldBeTrue();
        info.DefaultWidth.ShouldBe(500.0);
    }

    [Fact]
    public void GetCompositeFonts_ExplicitCidToGidMapStream_IsParsed()
    {
        // CIDToGIDMap stream: 2-byte big-endian GIDs. CID0 → 0 (skipped), CID1 → 0x0005.
        var mapBytes = new byte[] { 0x00, 0x00, 0x00, 0x05 };
        var c2G = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["Length"] = new PdfInteger(mapBytes.Length) }),
            mapBytes
        );
        var cidFont = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("CIDFontType2"),
                ["CIDToGIDMap"] = c2G,
                ["W"] = new PdfArray([new PdfInteger(1), new PdfArray([new PdfInteger(600)])])
            }
        );
        var type0 = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("Type0"),
                ["Encoding"] = PdfName.Get("Identity-H"),
                ["DescendantFonts"] = new PdfArray([cidFont])
            }
        );
        var info = PageFontResolver.GetCompositeFonts(Page(FontDict(("F1", type0))), Core())["F1"];
        info.IdentityCidToGid.ShouldBeFalse();
        info.CidToGid.ShouldNotBeNull();
        info.CidToGid![1].ShouldBe(5);
        info.Widths[1].ShouldBe(600.0);
    }

    [Fact]
    public void GetCompositeFonts_WidthRangeForm_IsParsed()
    {
        // /W "first last width" range form: CIDs 10..12 all width 250.
        var cidFont = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("CIDFontType2"),
                ["W"] = new PdfArray([new PdfInteger(10), new PdfInteger(12), new PdfInteger(250)])
            }
        );
        var type0 = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("Type0"),
                ["DescendantFonts"] = new PdfArray([cidFont])
            }
        );
        var info = PageFontResolver.GetCompositeFonts(Page(FontDict(("F1", type0))), Core())["F1"];
        info.Widths[10].ShouldBe(250.0);
        info.Widths[12].ShouldBe(250.0);
    }

    [Fact]
    public void GetCompositeFonts_NonType0_IsSkipped()
    {
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Type1") });
        PageFontResolver.GetCompositeFonts(Page(FontDict(("F1", font))), Core()).ContainsKey("F1").ShouldBeFalse();
    }

    // ── GetToUnicodeMaps ──────────────────────────────────────────────────────

    [Fact]
    public void GetToUnicodeMaps_BfChar_IsParsed()
    {
        const string cmap =
            "/CIDInit /ProcSet findresource begin\n" +
            "1 beginbfchar\n" +
            "<0041> <0041>\n" +
            "endbfchar\n" +
            "end";
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["ToUnicode"] = Stream(cmap) });
        var result = PageFontResolver.GetToUnicodeMaps(Page(FontDict(("F1", font))), Core());
        result["F1"][0x41].ShouldBe("A");
    }

    [Fact]
    public void GetToUnicodeMaps_BfRange_IsParsed()
    {
        const string cmap =
            "2 beginbfrange\n" +
            "<0041> <0043> <0041>\n" +
            "endbfrange";
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["ToUnicode"] = Stream(cmap) });
        var result = PageFontResolver.GetToUnicodeMaps(Page(FontDict(("F1", font))), Core())["F1"];
        result[0x41].ShouldBe("A");
        result[0x42].ShouldBe("B");
        result[0x43].ShouldBe("C");
    }

    [Fact]
    public void GetToUnicodeMaps_NoToUnicode_IsSkipped()
    {
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Type1") });
        PageFontResolver.GetToUnicodeMaps(Page(FontDict(("F1", font))), Core()).ContainsKey("F1").ShouldBeFalse();
    }

    // ── GetType3Fonts ─────────────────────────────────────────────────────────

    [Fact]
    public void GetType3Fonts_WithCharProcsAndDifferences_IsParsed()
    {
        var charProcs = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["a1"] = Stream("0 0 100 100 re f") }
        );
        var encoding = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Differences"] = new PdfArray([new PdfInteger(65), PdfName.Get("a1")])
            }
        );
        var font = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("Type3"),
                ["FontMatrix"] = new PdfArray(
                    [
                        new PdfReal(0.001), new PdfInteger(0), new PdfInteger(0),
                        new PdfReal(0.001), new PdfInteger(0), new PdfInteger(0)
                    ]
                ),
                ["Encoding"] = encoding,
                ["CharProcs"] = charProcs,
                ["FirstChar"] = new PdfInteger(65),
                ["Widths"] = new PdfArray([new PdfInteger(500)])
            }
        );
        var info = PageFontResolver.GetType3Fonts(Page(FontDict(("F1", font))), Core())["F1"];
        info.FirstChar.ShouldBe(65);
        info.Encoding[65].ShouldBe("a1");
        info.CharProcs.ContainsKey("a1").ShouldBeTrue();
        info.Widths[0].ShouldBe(500.0);
        info.FontMatrix[0].ShouldBe(0.001, 1e-9);
    }

    [Fact]
    public void GetType3Fonts_NamedEncoding_PopulatesAsciiFallback()
    {
        var font = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Subtype"] = PdfName.Get("Type3"),
                ["Encoding"] = PdfName.Get("WinAnsiEncoding"),
                ["CharProcs"] = new PdfDictionary()
            }
        );
        var info = PageFontResolver.GetType3Fonts(Page(FontDict(("F1", font))), Core())["F1"];
        info.Encoding['A'].ShouldBe("A");
    }

    [Fact]
    public void GetType3Fonts_NonType3_IsSkipped()
    {
        var font = new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Type1") });
        PageFontResolver.GetType3Fonts(Page(FontDict(("F1", font))), Core()).ContainsKey("F1").ShouldBeFalse();
    }
}
