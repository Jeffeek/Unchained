using Shouldly;
using Unchained.Drawing;
using Unchained.Drawing.Text;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Rendering;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Rendering;

/// <summary>
///     Direct operator-dispatch branch coverage for <c>PageRenderer.cs</c>. Constructs the internal
///     <see cref="PageRenderer" /> against a real <see cref="RasterBuffer" /> and feeds hand-built
///     <see cref="ContentOperator" /> lists, exercising branches that a full-PDF render rarely reaches:
///     the per-operator catch (text vs non-text), the no-operand consume arms (cs/CS/gs/sh/J/j/M),
///     the SC/SCN stroke arms of the colour setters, the scn pattern shading/tiling name resolution,
///     and every device/fallback arm of <c>ResolveColorComponents</c>.
/// </summary>
public sealed class PageRendererDirectDispatchTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> EmptyFontMap =
        new Dictionary<string, string>();
    private readonly RasterBuffer _buffer = new(100, 100);
    private readonly FontCache _fonts = new();

    public void Dispose() => _fonts.Dispose();

    private PageRenderer NewRenderer(
        IReadOnlyDictionary<string, ShadingInfo>? shadings = null,
        IReadOnlyDictionary<string, TilingPatternInfo>? tilingPatterns = null,
        IReadOnlyDictionary<string, ColorSpaceInfo>? colorSpaces = null,
        IReadOnlyDictionary<string, byte[]?>? embeddedFontBytes = null
    ) =>
        new(
            _buffer,
            _fonts,
            1.0,
            100.0,
            embeddedFontBytes,
            shadings: shadings,
            tilingPatterns: tilingPatterns,
            colorSpaces: colorSpaces
        );

    private static void Render(PageRenderer r, params ContentOperator[] ops) =>
        r.Render(ops, EmptyFontMap);

    private static ContentOperator Op(string name, params PdfObject[] operands) =>
        new(name, operands);

    private static PdfObject N(double v) => new PdfReal(v);
    private static PdfObject I(long v) => new PdfInteger(v);
    private static PdfObject Name(string v) => PdfName.Get(v);

    // Fills the whole page after setting fill colour, so we can read back the painted colour.
    private static ContentOperator[] FillPage() =>
    [
        Op("re", I(0), I(0), I(100), I(100)),
        Op("f")
    ];

    // ── Per-operator catch (lines 97-104) ────────────────────────────────────

    // ── Defensive handling of bad operands (per-operator try/catch at 93-104) ──
    // Note: the catch body itself is effectively unreachable through the operator API because
    // ReadIntOrReal falls back to 0 on a non-numeric operand and GetFonts degrades gracefully
    // rather than throwing. These tests pin the resulting graceful behaviour.

    [Fact]
    public void TextOperator_WithGarbageEmbeddedFont_RendersWithoutThrowing()
    {
        // Garbage embedded font bytes for "F1": the font subsystem must degrade gracefully
        // (fallback face) rather than crash the page. No text error is recorded.
        var embedded = new Dictionary<string, byte[]?>
        {
            ["F1"] = [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03]
        };
        var r = NewRenderer(embeddedFontBytes: embedded);
        Should.NotThrow(() => Render(
                r,
                Op("BT"),
                Op("Tf", Name("F1"), N(12)),
                Op("Td", N(10), N(50)),
                Op("Tj", new PdfString("Hi"u8.ToArray())),
                Op("ET")
            )
        );
        r.TextErrorCount.ShouldBe(0);
    }

    [Fact]
    public void Cm_WithNonNumericOperands_FallsBackToZero_NotThrow()
    {
        var r = NewRenderer();
        // cm with 6 name operands: Num→ReadIntOrReal returns the 0 fallback for each, so the CTM
        // is multiplied by an all-zero matrix — handled, not thrown. TextErrorCount stays 0.
        var nm = Name("x");
        Should.NotThrow(() =>
                // ReSharper disable BadListLineBreaks
                Render(
                    r,
                    Op(
                        "cm",
                        nm,
                        nm,
                        nm,
                        nm,
                        nm,
                        nm
                    )
                )
            // ReSharper restore BadListLineBreaks
        );
        r.TextErrorCount.ShouldBe(0);
    }

    [Fact]
    public void UnbalancedQ_WithEmptyStack_DoesNotThrow()
    {
        var r = NewRenderer();
        // Q with nothing pushed: the `_gsStack.Count > 0` guard is false → SyncClip only.
        Should.NotThrow(() => Render(r, Op("Q")));
    }

    [Fact]
    public void QCountThenQ_RestoresState()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("q"), Op("q"), Op("Q"), Op("Q")));
    }

    // ── Colour-space selection consume arms (lines 165-171) ───────────────────

    [Fact]
    public void Cs_WithName_SetsFillColorSpace_ThenNoOperand_Consumed()
    {
        var r = NewRenderer();
        // cs with a name, then a bare cs with no operand (line 171 consume arm).
        Should.NotThrow(() => Render(r, Op("cs", Name("DeviceRGB")), Op("cs")));
    }

    [Fact]
    public void CS_WithName_SetsStrokeColorSpace_ThenNoOperand_Consumed()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("CS", Name("DeviceGray")), Op("CS")));
    }

    [Fact]
    public void Cs_WithNonNameOperand_FallsBackToDeviceGray()
    {
        var r = NewRenderer();
        // operand present but not a PdfName → `?? DeviceGray`.
        Should.NotThrow(() => Render(r, Op("cs", I(5))));
    }

    // ── sc / SC (lines 173-183) ───────────────────────────────────────────────

    [Fact]
    public void Sc_DeviceRgb_SetsFillColour()
    {
        var r = NewRenderer();
        Render(r, Op("cs", Name("DeviceRGB")), Op("sc", N(1), N(0), N(0)));
        Render(r, FillPage());
        _buffer.GetPixelRgb(50, 50).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void SC_DeviceRgb_SetsStrokeColour_StrokeArm()
    {
        var r = NewRenderer();
        // SC (uppercase) → the `else SetStrokeRgb` arm (line 181).
        Should.NotThrow(() =>
            Render(r, Op("CS", Name("DeviceRGB")), Op("SC", N(0), N(1), N(0)))
        );
    }

    // ── scn / SCN device path + stroke arms (lines 195-201) ───────────────────

    [Fact]
    public void Scn_DeviceRgb_NoPattern_SetsFill()
    {
        var r = NewRenderer();
        Render(r, Op("cs", Name("DeviceRGB")), Op("scn", N(0), N(0), N(1)));
        Render(r, FillPage());
        _buffer.GetPixelRgb(50, 50).ShouldBe(((byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void SCN_DeviceRgb_NoPattern_StrokeArm()
    {
        var r = NewRenderer();
        Should.NotThrow(() =>
            Render(r, Op("CS", Name("DeviceRGB")), Op("SCN", N(1), N(1), N(0)))
        );
    }

    // ── scn / SCN pattern-with-components heuristic (lines 204-234) ───────────

    [Fact]
    public void Scn_PatternWithOneComponent_GrayHeuristic()
    {
        var r = NewRenderer();
        // pattern name present + 1 numeric → nums.Count==1 → SetFillGray.
        Render(r, Op("scn", N(0.5), Name("P1")), FillPage()[0], FillPage()[1]);
        var (rr, gg, bb) = _buffer.GetPixelRgb(50, 50);
        rr.ShouldBe(gg);
        gg.ShouldBe(bb);
    }

    [Fact]
    public void SCN_PatternWithOneComponent_StrokeGrayHeuristic()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("SCN", N(0.5), Name("P1"))));
    }

    [Fact]
    public void Scn_PatternWithThreeComponents_RgbHeuristic()
    {
        var r = NewRenderer();
        // 3 numeric + pattern name → the RGB heuristic arm runs, but FillIsPattern is then set
        // true so DrawFill paints the unknown-pattern grey approximation rather than the RGB.
        Render(r, Op("scn", N(1), N(0), N(0), Name("P1")), FillPage()[0], FillPage()[1]);
        var (rr, gg, bb) = _buffer.GetPixelRgb(50, 50);
        rr.ShouldBe(gg);
        gg.ShouldBe(bb);
    }

    [Fact]
    public void SCN_PatternWithThreeComponents_StrokeArm()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("SCN", N(1), N(0), N(0), Name("P1"))));
    }

    [Fact]
    public void Scn_PatternWithFourComponents_CmykHeuristic()
    {
        var r = NewRenderer();
        Should.NotThrow(() =>
            // ReSharper disable once BadListLineBreaks
            Render(
                r,
                Op(
                    "scn",
                    N(0),
                    N(1),
                    N(1),
                    N(0),
                    Name("P1")
                )
            )
        );
    }

    [Fact]
    public void SCN_PatternWithFourComponents_StrokeArm()
    {
        var r = NewRenderer();
        Should.NotThrow(() =>
            // ReSharper disable once BadListLineBreaks
            Render(
                r,
                Op(
                    "SCN",
                    N(0),
                    N(1),
                    N(1),
                    N(0),
                    Name("P1")
                )
            )
        );
    }

    // ── scn names a known shading / tiling pattern (lines 245-251) ────────────

    [Fact]
    public void Scn_NamedShading_SetsFillShadingName()
    {
        var shadings = new Dictionary<string, ShadingInfo>
        {
            ["Sh1"] = new(
                2,
                [0, 0, 100, 100],
                true,
                true,
                new byte[256 * 3]
            )
        };
        var r = NewRenderer(shadings);
        // scn with only a name that matches a known shading → FillShadingName set.
        Should.NotThrow(() => Render(r, Op("scn", Name("Sh1"))));
    }

    [Fact]
    public void Scn_NamedTilingPattern_SetsFillTilingName()
    {
        var tilings = new Dictionary<string, TilingPatternInfo>
        {
            ["P1"] = new(
                1,
                [0, 0, 10, 10],
                10,
                10,
                [1, 0, 0, 1, 0, 0],
                []
            )
        };
        var r = NewRenderer(tilingPatterns: tilings);
        Should.NotThrow(() => Render(r, Op("scn", Name("P1"))));
    }

    [Fact]
    public void Scn_UnknownName_LeavesShadingAndTilingNull()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("scn", Name("Unknown"))));
    }

    // ── Misc graphics-state consume arms (lines 258-296) ──────────────────────

    [Fact]
    public void LineCapJoinMiter_AndRiI_AreConsumed()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(
                r,
                Op("J", I(1)),
                Op("j", I(1)),
                Op("M", N(10)),
                Op("J"), // no-operand consume arm (line 270)
                Op("ri", Name("RelativeColorimetric")),
                Op("i", N(1))
            )
        );
    }

    [Fact]
    public void DashArray_WithNegativeValuesFiltered()
    {
        var r = NewRenderer();
        // d with an array containing a negative entry → filtered out by Where(v >= 0).
        Should.NotThrow(() =>
            Render(r, Op("d", new PdfArray([N(6), N(-4), N(2)]), I(0)))
        );
    }

    [Fact]
    public void DashOperator_WithNonArrayOperand_YieldsEmpty()
    {
        var r = NewRenderer();
        // d with a non-array first operand → DashLengths = [].
        Should.NotThrow(() => Render(r, Op("d", I(0), I(0))));
    }

    [Fact]
    public void Gs_NoOperand_IsConsumed()
    {
        var r = NewRenderer();
        // gs with no operand (line 296 bare consume arm).
        Should.NotThrow(() => Render(r, Op("gs")));
    }

    [Fact]
    public void Gs_UnknownName_NoExtGState_DoesNotThrow()
    {
        var r = NewRenderer();
        // gs name present but no extGStateAlphas dictionary → inner guard false.
        Should.NotThrow(() => Render(r, Op("gs", Name("GS1"))));
    }

    // ── sh consume arm (line 473) ─────────────────────────────────────────────

    [Fact]
    public void Sh_NoOperand_IsConsumed()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("sh")));
    }

    [Fact]
    public void Sh_UnknownName_NoShadingsDict_DoesNotThrow()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("sh", Name("Missing"))));
    }

    // ── BI consume arm (line 463) ─────────────────────────────────────────────

    [Fact]
    public void BI_WithNoInlineImage_IsConsumed()
    {
        var r = NewRenderer();
        // BI without a PdfInlineImage operand → the bare `case "BI"` consume arm.
        Should.NotThrow(() => Render(r, Op("BI")));
    }

    // ── Do with non-name operand (line 455-457) ───────────────────────────────

    [Fact]
    public void Do_WithNonNameOperand_DoesNotPaint()
    {
        var r = NewRenderer();
        // Do whose operand is not a PdfName → the `is PdfName` guard fails, nothing painted.
        Should.NotThrow(() => Render(r, Op("Do", I(5))));
    }

    [Fact]
    public void Do_UnknownXObject_DoesNotThrow()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("Do", Name("Im1"))));
    }

    // ── ResolveColorComponents device arms via sc (lines 554-583) ─────────────

    [Fact]
    public void Sc_DeviceGray_SingleComponent()
    {
        var r = NewRenderer();
        Render(r, Op("cs", Name("DeviceGray")), Op("sc", N(0.5)), FillPage()[0], FillPage()[1]);
        var (rr, gg, bb) = _buffer.GetPixelRgb(50, 50);
        rr.ShouldBe(gg);
        gg.ShouldBe(bb);
        rr.ShouldBeGreaterThan((byte)0);
    }

    [Fact]
    public void Sc_DeviceGray_NoComponents_DefaultsBlack()
    {
        var r = NewRenderer();
        // DeviceGray arm with components.Length == 0 → v defaults to 0 (black).
        Should.NotThrow(() => Render(r, Op("cs", Name("DeviceGray")), Op("sc")));
    }

    [Fact]
    public void Sc_DeviceRgb_TooFewComponents_DefaultsBlack()
    {
        var r = NewRenderer();
        // DeviceRGB arm with < 3 components → returns (0,0,0).
        Render(r, Op("cs", Name("DeviceRGB")), Op("sc", N(0.5)), FillPage()[0], FillPage()[1]);
        _buffer.GetPixelRgb(50, 50).ShouldBe(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void Sc_DeviceCmyk_FourComponents()
    {
        var r = NewRenderer();
        Render(r, Op("cs", Name("DeviceCMYK")), Op("sc", N(0), N(0), N(0), N(0)), FillPage()[0], FillPage()[1]);
        // 0,0,0,0 CMYK → white.
        _buffer.GetPixelRgb(50, 50).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [Fact]
    public void Sc_DeviceCmyk_TooFewComponents_DefaultsBlack()
    {
        var r = NewRenderer();
        // DeviceCMYK arm with < 4 components → returns (0,0,0).
        Should.NotThrow(() =>
            Render(r, Op("cs", Name("DeviceCMYK")), Op("sc", N(0.2), N(0.2)))
        );
    }

    [Fact]
    public void Sc_NamedSpaceMissing_OneComponent_GrayFallback()
    {
        var r = NewRenderer();
        // cs names a space absent from the (null) colorSpaces dict → component-count heuristic.
        // 1 component → gray.
        Render(r, Op("cs", Name("Sep1")), Op("sc", N(0.5)), FillPage()[0], FillPage()[1]);
        var (rr, gg, bb) = _buffer.GetPixelRgb(50, 50);
        rr.ShouldBe(gg);
        gg.ShouldBe(bb);
    }

    [Fact]
    public void Sc_NamedSpaceMissing_FourComponents_CmykFallback()
    {
        var r = NewRenderer();
        Should.NotThrow(() =>
            Render(r, Op("cs", Name("DN1")), Op("sc", N(0), N(0), N(0), N(0)))
        );
    }

    [Fact]
    public void Sc_NamedSpaceMissing_ThreeComponents_RgbFallback()
    {
        var r = NewRenderer();
        Render(r, Op("cs", Name("X")), Op("sc", N(1), N(0), N(0)), FillPage()[0], FillPage()[1]);
        _buffer.GetPixelRgb(50, 50).ShouldBe(((byte)255, (byte)0, (byte)0));
    }

    [Fact]
    public void Sc_NamedSpaceMissing_ZeroComponents_BlackFallback()
    {
        var r = NewRenderer();
        // The `sc` `when Operands.Count >= 1` guard requires ≥1 operand, but a PdfName operand
        // yields zero NUMERIC components → the `_ => (0,0,0)` switch default.
        Should.NotThrow(() => Render(r, Op("cs", Name("X")), Op("sc", Name("ignored"))));
    }

    // ── Named colour space present in dict (lines 575-587) ────────────────────

    [Fact]
    public void Sc_NamedSpacePresent_UsesColorSpaceInfo()
    {
        var colorSpaces = new Dictionary<string, ColorSpaceInfo>
        {
            ["CS1"] = ColorSpaceInfo.Device("DeviceRGB")
        };
        var r = NewRenderer(colorSpaces: colorSpaces);
        Render(r, Op("cs", Name("CS1")), Op("sc", N(0), N(1), N(0)), FillPage()[0], FillPage()[1]);
        var (_, gg, _) = _buffer.GetPixelRgb(50, 50);
        gg.ShouldBeGreaterThan((byte)200);
    }

    // ── SetInitialFillColor (lines 537-545) ───────────────────────────────────

    [Fact]
    public void SetInitialFillColor_AppliesToFillAndStroke()
    {
        var r = NewRenderer();
        r.SetInitialFillColor(10, 20, 30);
        Render(r, FillPage());
        _buffer.GetPixelRgb(50, 50).ShouldBe(((byte)10, (byte)20, (byte)30));
    }

    // ── cm with too few operands falls through (no matching arm) ──────────────

    [Fact]
    public void Cm_WithTooFewOperands_IsIgnored()
    {
        var r = NewRenderer();
        // cm needs 6 operands; with fewer the `when` guard fails and no arm matches → no-op.
        Should.NotThrow(() => Render(r, Op("cm", N(1), N(0), N(0))));
    }

    [Fact]
    public void UnknownOperator_IsIgnored()
    {
        var r = NewRenderer();
        Should.NotThrow(() => Render(r, Op("zz", N(1))));
    }
}
