using Shouldly;
using Unchained.Drawing;
using Xunit;

namespace Unchained.Drawing.Primitives.Tests;

/// <summary>Unit tests for <see cref="Vector2D" /> — pure 2D vector arithmetic.</summary>
public sealed class Vector2DTests
{
    [Fact]
    public void Magnitude_ThreeFourFive_IsFive() =>
        Vector2D.Magnitude(3, 4).ShouldBe(5);

    [Fact]
    public void Magnitude_Zero_IsZero() =>
        Vector2D.Magnitude(0, 0).ShouldBe(0);

    [Fact]
    public void Magnitude_NegativeComponents_IsPositive() =>
        Vector2D.Magnitude(-3, -4).ShouldBe(5);

    [Fact]
    public void Distance_BetweenTwoPoints_IsEuclidean() =>
        Vector2D.Distance(1, 2, 4, 6).ShouldBe(5); // dx=3, dy=4

    [Fact]
    public void Distance_SamePoint_IsZero() =>
        Vector2D.Distance(7, 7, 7, 7).ShouldBe(0);
}
