using Shouldly;
using Xunit;

namespace Unchained.Drawing.Primitives.Tests;

/// <summary>Unit tests for <see cref="Matrix2D" /> — pure 2D affine-matrix arithmetic (PDF row-major [a b c d e f]).</summary>
public sealed class Matrix2DTests
{
    [Fact]
    public void Identity_IsExpectedSixElements() =>
        Matrix2D.Identity().ShouldBe([1, 0, 0, 1, 0, 0]);

    [Fact]
    public void Translate_PlacesOffsetsInEAndF() =>
        Matrix2D.Translate(5, 7).ShouldBe([1, 0, 0, 1, 5, 7]);

    [Fact]
    public void Transform_ThroughIdentity_ReturnsSamePoint()
    {
        var (x, y) = Matrix2D.Transform(Matrix2D.Identity(), 3, 4);
        x.ShouldBe(3);
        y.ShouldBe(4);
    }

    [Fact]
    public void Transform_ThroughTranslation_AddsOffset()
    {
        var (x, y) = Matrix2D.Transform(Matrix2D.Translate(10, 20), 3, 4);
        x.ShouldBe(13);
        y.ShouldBe(24);
    }

    [Fact]
    public void Multiply_IdentityWithMatrix_ReturnsOriginal()
    {
        double[] m = [2, 0, 0, 3, 5, 7];
        Matrix2D.Multiply(Matrix2D.Identity(), m).ShouldBe(m);
        Matrix2D.Multiply(m, Matrix2D.Identity()).ShouldBe(m);
    }

    [Fact]
    public void Multiply_ComposesTranslations()
    {
        // m1 applied first, then m2: translate(1,2) then translate(3,4) => translate(4,6)
        var result = Matrix2D.Multiply(Matrix2D.Translate(1, 2), Matrix2D.Translate(3, 4));
        result.ShouldBe([1, 0, 0, 1, 4, 6]);
    }

    [Fact]
    public void Multiply_ScaleThenTranslate_AppliesScaleFirst()
    {
        // scale(2,3) first, then translate(5,7). A point (1,1) -> (2,3) -> (7,10).
        double[] scale = [2, 0, 0, 3, 0, 0];
        var combined = Matrix2D.Multiply(scale, Matrix2D.Translate(5, 7));
        var (x, y) = Matrix2D.Transform(combined, 1, 1);
        x.ShouldBe(7);
        y.ShouldBe(10);
    }
}
