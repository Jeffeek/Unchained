using Shouldly;
using Unchained.Drawing.Primitives.Fonts;
using Xunit;

namespace Unchained.Drawing.Primitives.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="TrueTypeMetrics" /> exercising its public surface in isolation:
///     the <see cref="TrueTypeMetrics.HelveticaFallback" /> constant and the failure behaviour of
///     <see cref="TrueTypeMetrics.Read" /> on non-font input. Parsing of real font files is covered
///     by the font-subsystem integration tests.
/// </summary>
public sealed class TrueTypeMetricsTests
{
    [Fact]
    public void HelveticaFallback_HasExpectedNormalisedMetrics()
    {
        var m = TrueTypeMetrics.HelveticaFallback;
        m.XMin.ShouldBe(-166);
        m.YMin.ShouldBe(-225);
        m.XMax.ShouldBe(1000);
        m.YMax.ShouldBe(931);
        m.Ascent.ShouldBe(800);
        m.Descent.ShouldBe(-200);
        m.CapHeight.ShouldBe(716);
        m.StemV.ShouldBe(80);
    }

    [Fact]
    public void Read_EmptyArray_ReturnsFallback() =>
        // < 12 bytes: Parse short-circuits to the Helvetica fallback rather than throwing.
        TrueTypeMetrics.Read([]).ShouldBe(TrueTypeMetrics.HelveticaFallback);

    [Fact]
    public void Read_GarbageBytes_ReturnsFallback()
    {
        var garbage = new byte[64];
        new Random(1).NextBytes(garbage);

        TrueTypeMetrics.Read(garbage).ShouldBe(TrueTypeMetrics.HelveticaFallback);
    }

    [Fact]
    public void FontMetrics_Record_EqualityByValue()
    {
        var a = new FontMetrics(
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8
        );
        var b = new FontMetrics(
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8
        );
        a.ShouldBe(b);
    }

    // ── Synthetic-font parsing ────────────────────────────────────────────────

    [Fact]
    public void Read_FontWithOs2V2_UsesTypographicMetricsAndCapHeight()
    {
        var font = SyntheticTrueType.Build(
            1000,
            2,
            800,
            -200,
            700,
            400,
            true,
            true
        );

        var m = TrueTypeMetrics.Read(font);

        m.ShouldNotBeNull();
        m.Ascent.ShouldBe(800);
        m.Descent.ShouldBe(-200);
        m.CapHeight.ShouldBe(700); // taken directly from OS/2 v2 sCapHeight
        m.StemV.ShouldBeGreaterThan(10);
    }

    [Fact]
    public void Read_FontWithOs2V1_EstimatesCapHeightFromAscent()
    {
        var font = SyntheticTrueType.Build(
            1000,
            1,
            750,
            -250,
            0,
            700,
            true,
            true
        );

        var m = TrueTypeMetrics.Read(font);

        m.ShouldNotBeNull();
        m.Ascent.ShouldBe(750);
        // CapHeight estimated as ~72% of ascent when OS/2 < v2.
        m.CapHeight.ShouldBe((int)(750 * 0.72));
    }

    [Fact]
    public void Read_FontWithoutOs2_FallsBackToHhea()
    {
        var font = SyntheticTrueType.Build(
            2048,
            0,
            0,
            0,
            0,
            0,
            false,
            true,
            1638,
            -410
        );

        var m = TrueTypeMetrics.Read(font);

        m.ShouldNotBeNull();
        // 2048 upem scaled to 1000: 1638 → ~800, -410 → ~-200.
        m.Ascent.ShouldBeInRange(790, 810);
        m.Descent.ShouldBeInRange(-210, -190);
    }

    [Fact]
    public void Read_FontWithNeitherOs2NorHhea_ReturnsFallback()
    {
        var font = SyntheticTrueType.Build(
            1000,
            0,
            0,
            0,
            0,
            0,
            false,
            false
        );

        TrueTypeMetrics.Read(font).ShouldBe(TrueTypeMetrics.HelveticaFallback);
    }

    [Fact]
    public void Read_NonThousandUnitsPerEm_ScalesMetricsToThousand()
    {
        // unitsPerEm 2048 → scale 1000/2048; a 1024-unit ascender becomes ~500.
        var font = SyntheticTrueType.Build(
            2048,
            2,
            1024,
            -512,
            1024,
            400,
            true,
            true
        );

        var m = TrueTypeMetrics.Read(font);
        m.ShouldNotBeNull();
        m.Ascent.ShouldBe(500);
        m.Descent.ShouldBe(-250);
    }

    [Fact]
    public void Read_ZeroUnitsPerEm_DefaultsToThousand()
    {
        // unitsPerEm 0 is invalid → parser defaults it to 1000 (scale 1, no division by zero).
        var font = SyntheticTrueType.Build(
            0,
            2,
            700,
            -200,
            650,
            400,
            true,
            true
        );

        var m = TrueTypeMetrics.Read(font);
        m.ShouldNotBeNull();
        m.Ascent.ShouldBe(700);
    }

    [Fact]
    public void Read_HeavyWeightClass_RaisesStemV()
    {
        // WeightClass 700 → pow(700/65, 2) + 50 = 165 (clamped within [10, 340]).
        var font = SyntheticTrueType.Build(
            1000,
            2,
            750,
            -250,
            700,
            700,
            true,
            true
        );

        var m = TrueTypeMetrics.Read(font);
        m.ShouldNotBeNull();
        m.StemV.ShouldBe(165);
    }
}
