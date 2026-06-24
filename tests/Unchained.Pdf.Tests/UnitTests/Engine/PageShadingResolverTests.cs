using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for <see cref="PageShadingResolver" />. Builds page dictionaries with
///     <c>/Shading</c> and <c>/Pattern</c> resources to exercise axial/radial ramp pre-sampling,
///     PatternType-2 shading patterns, and PatternType-1 tiling pattern parsing.
/// </summary>
public sealed class PageShadingResolverTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfDictionary PageWithShading(params (string Name, PdfObject Shading)[] shadings)
    {
        var entries = new Dictionary<string, PdfObject>();
        foreach (var (name, sh) in shadings) entries[name] = sh;
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Shading"] = new PdfDictionary(entries) }
        );
        return new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );
    }

    private static PdfDictionary ExpFn() => new(
        new Dictionary<string, PdfObject>
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["C0"] = new PdfArray([new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
            ["C1"] = new PdfArray([new PdfReal(1), new PdfReal(1), new PdfReal(1)]),
            ["N"] = new PdfReal(1.0)
        }
    );

    private static PdfDictionary AxialShading() => new(
        new Dictionary<string, PdfObject>
        {
            ["ShadingType"] = new PdfInteger(2),
            ["ColorSpace"] = PdfName.Get("DeviceRGB"),
            ["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(0), new PdfInteger(100)]),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["Function"] = ExpFn(),
            ["Extend"] = new PdfArray([PdfBoolean.True, PdfBoolean.True])
        }
    );

    [Fact]
    public void AxialShading_BuildsRampAndExtendFlags()
    {
        var result = PageShadingResolver.GetShadings(PageWithShading(("Sh1", AxialShading())), Core());
        result.ContainsKey("Sh1").ShouldBeTrue();
        var sh = result["Sh1"];
        sh.ShadingType.ShouldBe(2);
        sh.ExtendStart.ShouldBeTrue();
        sh.ExtendEnd.ShouldBeTrue();
        sh.ColorRamp.Length.ShouldBe(256 * 3);
        // Ramp goes black (t=0) to white (t=1).
        sh.ColorAt(0.0).R.ShouldBeLessThan((byte)20);
        sh.ColorAt(1.0).R.ShouldBeGreaterThan((byte)235);
    }

    [Fact]
    public void RadialShading_BuildsRamp()
    {
        var radial = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ShadingType"] = new PdfInteger(3),
                ["ColorSpace"] = PdfName.Get("DeviceGray"),
                ["Coords"] = new PdfArray(
                    [
                        new PdfInteger(50), new PdfInteger(50), new PdfInteger(0),
                        new PdfInteger(50), new PdfInteger(50), new PdfInteger(40)
                    ]
                ),
                ["Function"] = new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        ["FunctionType"] = new PdfInteger(2),
                        ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                        ["C0"] = new PdfArray([new PdfReal(0)]),
                        ["C1"] = new PdfArray([new PdfReal(1)]),
                        ["N"] = new PdfReal(1.0)
                    }
                )
            }
        );
        var result = PageShadingResolver.GetShadings(PageWithShading(("R1", radial)), Core());
        result["R1"].ShadingType.ShouldBe(3);
        result["R1"].Coords.Length.ShouldBe(6);
    }

    [Fact]
    public void ShadingType1_Unsupported_IsSkipped()
    {
        var fnBased = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["ShadingType"] = new PdfInteger(1) }
        );
        PageShadingResolver.GetShadings(PageWithShading(("S", fnBased)), Core()).ContainsKey("S").ShouldBeFalse();
    }

    [Fact]
    public void AxialShading_MissingCoords_IsSkipped()
    {
        var bad = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ShadingType"] = new PdfInteger(2),
                ["ColorSpace"] = PdfName.Get("DeviceRGB")
            }
        );
        PageShadingResolver.GetShadings(PageWithShading(("S", bad)), Core()).ContainsKey("S").ShouldBeFalse();
    }

    [Fact]
    public void PatternType2_ShadingPattern_IsCollectedAsShading()
    {
        var patternDict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Get("Pattern"),
                ["PatternType"] = new PdfInteger(2),
                ["Shading"] = AxialShading()
            }
        );
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Pattern"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["P1"] = patternDict })
            }
        );
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );

        var result = PageShadingResolver.GetShadings(page, Core());
        result.ContainsKey("P1").ShouldBeTrue();
        result["P1"].ShadingType.ShouldBe(2);
    }

    [Fact]
    public void NoResources_ReturnsEmpty()
    {
        var page = new PdfDictionary(new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page });
        PageShadingResolver.GetShadings(page, Core()).ShouldBeEmpty();
    }

    // ── Tiling patterns ────────────────────────────────────────────────────────

    [Fact]
    public void GetTilingPatterns_ParsesPatternType1()
    {
        var cell = "1 0 0 rg 0 0 5 5 re f"u8.ToArray();
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Get("Pattern"),
                ["PatternType"] = new PdfInteger(1),
                ["PaintType"] = new PdfInteger(1),
                ["BBox"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(10), new PdfInteger(10)]),
                ["XStep"] = new PdfInteger(10),
                ["YStep"] = new PdfInteger(10),
                ["Length"] = new PdfInteger(cell.Length)
            }
        );
        var stream = new PdfStream(dict, cell);
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Pattern"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["P1"] = stream })
            }
        );
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );

        var result = PageShadingResolver.GetTilingPatterns(page, Core());
        result.ContainsKey("P1").ShouldBeTrue();
        result["P1"].PaintType.ShouldBe(1);
        result["P1"].XStep.ShouldBe(10, 0.01);
        result["P1"].Operators.ShouldContain(static o => o.Name == "re");
    }

    [Fact]
    public void GetTilingPatterns_NoPattern_ReturnsEmpty()
    {
        var page = new PdfDictionary(new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page });
        PageShadingResolver.GetTilingPatterns(page, Core()).ShouldBeEmpty();
    }

    // ── Additional branch coverage ───────────────────────────────────────────────

    [Fact]
    public void AxialShading_CmykColorSpace_BuildsRamp()
    {
        var cmyk = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ShadingType"] = new PdfInteger(2),
                ["ColorSpace"] = PdfName.Get("DeviceCMYK"),
                ["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(0), new PdfInteger(100)]),
                ["Function"] = new PdfDictionary(
                    new Dictionary<string, PdfObject>
                    {
                        ["FunctionType"] = new PdfInteger(2),
                        ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                        ["C0"] = new PdfArray([new PdfReal(0), new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                        ["C1"] = new PdfArray([new PdfReal(1), new PdfReal(0), new PdfReal(0), new PdfReal(0)]),
                        ["N"] = new PdfReal(1.0)
                    }
                )
            }
        );
        var result = PageShadingResolver.GetShadings(PageWithShading(("C1", cmyk)), Core());
        result.ContainsKey("C1").ShouldBeTrue();
        // Full cyan at t=1 → low red.
        result["C1"].ColorAt(1.0).R.ShouldBeLessThan((byte)80);
    }

    [Fact]
    public void NestedFormXObject_Shading_IsCollected()
    {
        var formResources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Shading"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["Inner"] = AxialShading() })
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

        PageShadingResolver.GetShadings(page, Core()).ContainsKey("Inner").ShouldBeTrue();
    }

    [Fact]
    public void NonFormXObject_IsIgnoredDuringShadingCollection()
    {
        var image = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["Subtype"] = PdfName.Get("Image") }),
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

        PageShadingResolver.GetShadings(page, Core()).ShouldBeEmpty();
    }

    [Fact]
    public void NestedFormXObject_TilingPattern_IsCollected()
    {
        var cell = "0 0 5 5 re f"u8.ToArray();
        var tile = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["PatternType"] = new PdfInteger(1),
                    ["PaintType"] = new PdfInteger(2),
                    ["BBox"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(5), new PdfInteger(5)]),
                    ["XStep"] = new PdfInteger(5),
                    ["YStep"] = new PdfInteger(5),
                    ["Length"] = new PdfInteger(cell.Length)
                }
            ),
            cell
        );
        var formResources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Pattern"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["TP"] = tile })
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

        PageShadingResolver.GetTilingPatterns(page, Core()).ContainsKey("TP").ShouldBeTrue();
    }

    [Fact]
    public void AxialShading_DeviceGray_SingleChannelRamp()
    {
        // DeviceGray colour space with a single-component function → the (_, 1) ramp arm runs.
        var grayFn = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                ["C0"] = new PdfArray([new PdfReal(0)]),
                ["C1"] = new PdfArray([new PdfReal(1)]),
                ["N"] = new PdfReal(1.0)
            }
        );
        var shading = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ShadingType"] = new PdfInteger(2),
                ["ColorSpace"] = PdfName.Get("DeviceGray"),
                ["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]),
                ["Function"] = grayFn
            }
        );
        var result = PageShadingResolver.GetShadings(PageWithShading(("G1", shading)), Core());
        result["G1"].ShadingType.ShouldBe(2);
    }

    [Fact]
    public void AxialShading_NoFunction_UsesMidGreyRamp()
    {
        // No /Function → fn is null → the ramp falls back to [0.5,0.5,0.5] mid-grey per entry.
        var shading = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ShadingType"] = new PdfInteger(2),
                ["ColorSpace"] = PdfName.Get("DeviceRGB"),
                ["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)])
            }
        );
        var result = PageShadingResolver.GetShadings(PageWithShading(("N1", shading)), Core());
        result["N1"].ShadingType.ShouldBe(2);
    }

    [Fact]
    public void RadialShading_TooFewCoords_IsSkipped()
    {
        var shading = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ShadingType"] = new PdfInteger(3),
                ["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(0)]), // need 6
                ["Function"] = ExpFn()
            }
        );
        PageShadingResolver.GetShadings(PageWithShading(("R", shading)), Core()).ContainsKey("R").ShouldBeFalse();
    }

    [Fact]
    public void MeshShadingType4_NonStream_IsSkipped()
    {
        // ShadingType 4 declared as a plain dictionary (not a stream) → returns null.
        var shading = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["ShadingType"] = new PdfInteger(4) }
        );
        PageShadingResolver.GetShadings(PageWithShading(("M", shading)), Core()).ContainsKey("M").ShouldBeFalse();
    }

    [Fact]
    public void Extend_NonArray_DefaultsToFalseFalse()
    {
        // /Extend present but not an array → ReadExtend returns (false,false).
        var shading = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ShadingType"] = new PdfInteger(2),
                ["ColorSpace"] = PdfName.Get("DeviceRGB"),
                ["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]),
                ["Function"] = ExpFn(),
                ["Extend"] = new PdfInteger(0)
            }
        );
        var sh = PageShadingResolver.GetShadings(PageWithShading(("E", shading)), Core())["E"];
        sh.ExtendStart.ShouldBeFalse();
        sh.ExtendEnd.ShouldBeFalse();
    }

    [Fact]
    public void GetTilingPatterns_UndecodableStream_IsSkipped()
    {
        // A PatternType-1 stream whose /Filter throws on decode → the catch `continue` skips it.
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Type"] = PdfName.Get("Pattern"),
                ["PatternType"] = new PdfInteger(1),
                ["PaintType"] = new PdfInteger(1),
                ["BBox"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(10), new PdfInteger(10)]),
                ["XStep"] = new PdfInteger(10),
                ["YStep"] = new PdfInteger(10),
                ["Filter"] = PdfName.Get("JPXDecode"),
                ["Length"] = new PdfInteger(3)
            }
        );
        var stream = new PdfStream(dict, new byte[] { 1, 2, 3 });
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Pattern"] = new PdfDictionary(new Dictionary<string, PdfObject> { ["P1"] = stream })
            }
        );
        var page = new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );
        PageShadingResolver.GetTilingPatterns(page, Core()).ContainsKey("P1").ShouldBeFalse();
    }
}
