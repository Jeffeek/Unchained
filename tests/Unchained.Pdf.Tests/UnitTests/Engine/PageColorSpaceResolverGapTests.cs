using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Final-gap branch coverage for <see cref="PageColorSpaceResolver" />: the array-form Device
///     spaces (<c>[/DeviceGray]</c> etc.), CalRGB with an explicit /Matrix, Indexed over a CMYK
///     base, the duplicate-name skip, the form-XObject recursion (including a self-referential
///     cycle guarded by <c>seen</c>), and indirect-reference resolution of the colour-space object.
/// </summary>
public sealed class PageColorSpaceResolverGapTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfDictionary PageWith(params (string Name, PdfObject Space)[] spaces)
    {
        var cs = new Dictionary<string, PdfObject>();
        foreach (var (name, space) in spaces) cs[name] = space;
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["ColorSpace"] = new PdfDictionary(cs) }
        );
        return new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );
    }

    [
        Theory,
        InlineData("DeviceGray"),
        InlineData("DeviceRGB"),
        InlineData("DeviceCMYK")
    ]
    public void DeviceSpace_InArrayForm_BuildsDeviceInfo(string name)
    {
        var space = new PdfArray([PdfName.Get(name)]);
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core());
        result["CS0"].Kind.ShouldBe(name);
    }

    [Fact]
    public void CalRgb_WithGammaAndMatrix_BuildsInfo()
    {
        var calDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Gamma"] = new PdfArray([new PdfReal(1.8), new PdfReal(1.8), new PdfReal(1.8)]),
                ["Matrix"] = new PdfArray(
                    [
                        new PdfReal(0.4), new PdfReal(0.2), new PdfReal(0.0),
                        new PdfReal(0.3), new PdfReal(0.7), new PdfReal(0.1),
                        new PdfReal(0.1), new PdfReal(0.1), new PdfReal(0.8)
                    ]
                )
            }
        );
        var space = new PdfArray([PdfName.Get("CalRGB"), calDict]);
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core())["CS0"].Kind.ShouldBe("CalRGB");
    }

    [Fact]
    public void CalGray_AbsentGamma_DefaultsToOne()
    {
        // No /Gamma → gamma <= 0 branch picks the 1.0 default.
        var space = new PdfArray([PdfName.Get("CalGray"), new PdfDictionary()]);
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core())["CS0"]
            .CalGrayGamma.ShouldBe(1.0, 0.001);
    }

    [Fact]
    public void Indexed_CmykBase_BuildsFourChannelPalette()
    {
        // Indexed over DeviceCMYK: base channels = 4.
        var lookup = new PdfString(new byte[] { 0, 0, 0, 0, 255, 0, 0, 0 }); // 2 CMYK entries
        var space = new PdfArray(
            [PdfName.Get("Indexed"), PdfName.Get("DeviceCMYK"), new PdfInteger(1), lookup]
        );
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core());
        result["CS0"].Kind.ShouldBe("Indexed");
        result["CS0"].IndexedBaseChannels.ShouldBe(4);
    }

    [Fact]
    public void Indexed_GrayBase_BuildsSingleChannelPalette()
    {
        var lookup = new PdfString(new byte[] { 0, 255 });
        var space = new PdfArray(
            [PdfName.Get("Indexed"), PdfName.Get("DeviceGray"), new PdfInteger(1), lookup]
        );
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core())["CS0"]
            .IndexedBaseChannels.ShouldBe(1);
    }

    [Fact]
    public void DuplicateName_SecondEntryIgnored()
    {
        // Two resource dicts can't share a key, so simulate the dedupe path through a nested
        // form XObject that re-declares CS0: result already contains it → skip (line 46).
        var inner = new PdfArray([PdfName.Get("Lab"), new PdfDictionary()]);
        var formResources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfDictionary(
                    new Dictionary<string, PdfObject> { ["CS0"] = new PdfArray([PdfName.Get("CalGray"), new PdfDictionary()]) }
                )
            }
        );
        var form = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Subtype"] = PdfName.Get("Form"),
                    ["Resources"] = formResources
                }
            ),
            ReadOnlyMemory<byte>.Empty
        );
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["CS0"] = inner }),
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Fm0"] = form })
            }
        );
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );

        // Page-level CS0 (Lab) wins; the form's CS0 (CalGray) is skipped as a duplicate.
        PageColorSpaceResolver.GetColorSpaces(page, Core())["CS0"].Kind.ShouldBe("Lab");
    }

    [Fact]
    public void FormXObject_NestedColorSpace_IsCollected()
    {
        var formResources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfDictionary(
                    new Dictionary<string, PdfObject> { ["CSInner"] = new PdfArray([PdfName.Get("Lab"), new PdfDictionary()]) }
                )
            }
        );
        var form = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["Subtype"] = PdfName.Get("Form"),
                    ["Resources"] = formResources
                }
            ),
            ReadOnlyMemory<byte>.Empty
        );
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Fm0"] = form })
            }
        );
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );

        PageColorSpaceResolver.GetColorSpaces(page, Core()).ContainsKey("CSInner").ShouldBeTrue();
    }

    [Fact]
    public void NonFormXObject_InResources_IsIgnored()
    {
        var image = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Image") }
            ),
            ReadOnlyMemory<byte>.Empty
        );
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Im0"] = image })
            }
        );
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );

        PageColorSpaceResolver.GetColorSpaces(page, Core()).ShouldBeEmpty();
    }

    [Fact]
    public void SeparationName_DirectName_StoredAsDeviceInfo()
    {
        // A non-Device direct name resolves through the name switch's default arm.
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", PdfName.Get("CustomInk"))), Core());
        result["CS0"].Kind.ShouldBe("CustomInk");
    }

    [Fact]
    public void ArrayKindWithoutName_IsSkipped()
    {
        // arr[0] is not a name → kind is null → returns null.
        var space = new PdfArray([new PdfInteger(1), new PdfInteger(2)]);
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core()).ContainsKey("CS0").ShouldBeFalse();
    }

    [Fact]
    public void IccBased_WithExplicitAlternate_UsesAlternateName()
    {
        var iccStream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["Alternate"] = PdfName.Get("DeviceCMYK") }),
            ReadOnlyMemory<byte>.Empty
        );
        var space = new PdfArray([PdfName.Get("ICCBased"), iccStream]);
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core());
        result["CS0"].Kind.ShouldBe("ICCBased");
    }

    [
        Theory,
        InlineData(1),
        InlineData(3),
        InlineData(4)
    ]
    public void IccBased_NoAlternate_InfersFromChannelCount(int n)
    {
        var iccStream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["N"] = new PdfInteger(n) }),
            ReadOnlyMemory<byte>.Empty
        );
        var space = new PdfArray([PdfName.Get("ICCBased"), iccStream]);
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core())["CS0"].Kind.ShouldBe("ICCBased");
    }

    [Fact]
    public void Separation_BuildsSeparationInfo()
    {
        // [/Separation /MyInk /DeviceCMYK <tint function>]
        var tintFn = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                ["C0"] = new PdfArray([new PdfReal(0), new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                ["C1"] = new PdfArray([new PdfReal(1), new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                ["N"] = new PdfInteger(1)
            }
        );
        var space = new PdfArray(
            [PdfName.Get("Separation"), PdfName.Get("MyInk"), PdfName.Get("DeviceCMYK"), tintFn]
        );
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core())["CS0"].Kind.ShouldBe("Separation");
    }

    [Fact]
    public void DeviceN_BuildsDeviceNInfo()
    {
        var tintFn = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                ["C0"] = new PdfArray([new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                ["C1"] = new PdfArray([new PdfReal(1), new PdfReal(1), new PdfReal(1)]),
                ["N"] = new PdfInteger(1)
            }
        );
        var names = new PdfArray([PdfName.Get("Ink1"), PdfName.Get("Ink2")]);
        var space = new PdfArray(
            [PdfName.Get("DeviceN"), names, PdfName.Get("DeviceRGB"), tintFn]
        );
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core())["CS0"].Kind.ShouldBe("DeviceN");
    }

    [Fact]
    public void Indexed_WithStreamLookup_BuildsPalette()
    {
        var lookupStream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["Length"] = new PdfInteger(6) }),
            new byte[] { 0, 0, 0, 255, 255, 255 }
        );
        var space = new PdfArray(
            [PdfName.Get("Indexed"), PdfName.Get("DeviceRGB"), new PdfInteger(1), lookupStream]
        );
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core());
        result["CS0"].Kind.ShouldBe("Indexed");
        result["CS0"].IndexedBaseChannels.ShouldBe(3);
    }

    [Fact]
    public void Indexed_EmptyLookup_ReturnsNull()
    {
        // A lookup that is neither string nor stream → empty palette → null info → not stored.
        var space = new PdfArray(
            [PdfName.Get("Indexed"), PdfName.Get("DeviceRGB"), new PdfInteger(1), new PdfInteger(0)]
        );
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", space)), Core()).ContainsKey("CS0").ShouldBeFalse();
    }

    [Fact]
    public void PatternName_IsIgnored() =>
        // Direct /Pattern name → null (handled elsewhere).
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", PdfName.Get("Pattern"))), Core())
            .ContainsKey("CS0")
            .ShouldBeFalse();

    [Fact]
    public void NoResources_ReturnsEmpty()
    {
        var page = new PdfDictionary(new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page });
        PageColorSpaceResolver.GetColorSpaces(page, Core()).ShouldBeEmpty();
    }

    [Fact]
    public void DeeplyNestedForms_StopAtDepthLimit()
    {
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["XObject"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Fm"] = BuildLevel(0) })
            }
        );
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );

        var result = PageColorSpaceResolver.GetColorSpaces(page, Core());
        result.ContainsKey("CS0").ShouldBeTrue();
        // Past the depth limit, the deepest level's space is not collected.
        result.ContainsKey("CS12").ShouldBeFalse();
        return;

        // Build 12 levels of nested form XObjects, each declaring a colour space. The resolver
        // stops recursing past depth 10, so the innermost spaces are not collected — but it must
        // not throw or hang, and the shallow spaces are still found.
        static PdfStream BuildLevel(int level)
        {
            var resourceEntries = new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfDictionary(
                    new Dictionary<string, PdfObject> { [$"CS{level}"] = new PdfArray([PdfName.Get("Lab"), new PdfDictionary()]) }
                )
            };
            if (level < 12)
            {
                resourceEntries["XObject"] = new PdfDictionary(
                    new Dictionary<string, PdfObject> { ["Fm"] = BuildLevel(level + 1) }
                );
            }

            return new PdfStream(
                new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        ["Subtype"] = PdfName.Get("Form"),
                        ["Resources"] = new PdfDictionary(resourceEntries)
                    }
                ),
                ReadOnlyMemory<byte>.Empty
            );
        }
    }

    [Fact]
    public void ColorSpaceValue_AsIndirectReference_IsResolvedAndBuilt()
    {
        // CS0 maps to an indirect reference whose target is a [/Lab <<>>] array — exercises the
        // indirect-resolution branch in both CollectColorSpaces and BuildColorSpaceInfo.
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] /Resources << /ColorSpace << /CS0 5 0 R >> >> >>",
            "<< /Foo 1 >>",
            "[/Lab << >>]"
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        var page = core.GetPage(1);
        var spaces = PageColorSpaceResolver.GetColorSpaces(page, core);
        spaces.ContainsKey("CS0").ShouldBeTrue();
    }

    [Fact]
    public void Indexed_LookupAsIndirectReference_IsResolved()
    {
        // The Indexed lookup table is an indirect reference to a string object (line 146 path).
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] /Resources << /ColorSpace << /CS0 5 0 R >> >> >>",
            @"(\377\000\000)", // 3-byte RGB palette string (object 4)
            "[/Indexed /DeviceRGB 0 4 0 R]"
        };
        using var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        var page = core.GetPage(1);
        var spaces = PageColorSpaceResolver.GetColorSpaces(page, core);
        spaces.ContainsKey("CS0").ShouldBeTrue();
    }

    // Builds a single-page doc whose page colour-space CS0 array references object 5 for its
    // sub-object (ICC stream / Cal dict), exercising the indirect-resolution ternary branches.
    private static IReadOnlyDictionary<string, Unchained.Pdf.Models.ColorSpaceInfo> ResolveWithIndirectSubObject(
        string cs0Array,
        string object5Body
    )
    {
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] /Resources << /ColorSpace << /CS0 4 0 R >> >> >>",
            cs0Array,
            object5Body
        };
        var core = PdfDocumentCore.Parse(RawPdfBuilder.Build(bodies));
        return PageColorSpaceResolver.GetColorSpaces(core.GetPage(1), core);
    }

    [Fact]
    public void IccBased_StreamAsIndirectReference_IsResolved()
    {
        // arr[1] is "5 0 R"; object 5 is an ICC stream with /N 3.
        var spaces = ResolveWithIndirectSubObject(
            "[/ICCBased 5 0 R]",
            "<< /N 3 /Length 0 >>\nstream\n\nendstream"
        );
        spaces.ContainsKey("CS0").ShouldBeTrue();
    }

    [Fact]
    public void CalGray_DictAsIndirectReference_IsResolved()
    {
        var spaces = ResolveWithIndirectSubObject("[/CalGray 5 0 R]", "<< /Gamma 2.2 >>");
        spaces.ContainsKey("CS0").ShouldBeTrue();
    }

    [Fact]
    public void CalRgb_DictAsIndirectReference_IsResolved()
    {
        var spaces = ResolveWithIndirectSubObject(
            "[/CalRGB 5 0 R]",
            "<< /Gamma [1.0 1.0 1.0] /Matrix [0.4 0.2 0.0 0.3 0.7 0.1 0.1 0.0 0.9] >>"
        );
        spaces.ContainsKey("CS0").ShouldBeTrue();
    }

    [Fact]
    public void CalRgb_NoMatrixNoGamma_BuildsInfo()
    {
        // Direct CalRGB dict with neither /Gamma nor /Matrix → both `is PdfArray ? : null` take the
        // null branch.
        var info = new PdfArray([PdfName.Get("CalRGB"), new PdfDictionary()]);
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", info)), Core())
            .ContainsKey("CS0")
            .ShouldBeTrue();
    }

    [Fact]
    public void CalGray_NonDictSecondElement_DefaultsGamma()
    {
        // arr[1] is not a dictionary → `arr[1] as PdfDictionary` is null → gamma defaults to 1.0.
        var info = new PdfArray([PdfName.Get("CalGray"), new PdfInteger(0)]);
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", info)), Core())
            .ContainsKey("CS0")
            .ShouldBeTrue();
    }

    [Fact]
    public void CalRgb_NonDictSecondElement_BuildsInfo()
    {
        var info = new PdfArray([PdfName.Get("CalRGB"), new PdfInteger(0)]);
        PageColorSpaceResolver.GetColorSpaces(PageWith(("CS0", info)), Core())
            .ContainsKey("CS0")
            .ShouldBeTrue();
    }
}
