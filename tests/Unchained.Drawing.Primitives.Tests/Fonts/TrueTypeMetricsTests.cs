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
    public void Read_GarbageBytes_DoesNotThrow()
    {
        var garbage = new byte[64];
        new Random(1).NextBytes(garbage);

        // Whatever it returns, it must not throw on malformed input.
        Should.NotThrow(() => TrueTypeMetrics.Read(garbage));
    }

    [Fact]
    public void FontMetrics_Record_EqualityByValue()
    {
        var a = new FontMetrics(1,
            2,
            3,
            4,
            5,
            6,
            7,
            8);
        var b = new FontMetrics(1,
            2,
            3,
            4,
            5,
            6,
            7,
            8);
        a.ShouldBe(b);
    }
}
