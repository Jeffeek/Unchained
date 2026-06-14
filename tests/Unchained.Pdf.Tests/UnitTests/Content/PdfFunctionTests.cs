using Shouldly;
using Unchained.Pdf.Content;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Content;

/// <summary>
///     Unit tests for <see cref="PdfFunction" /> — the type 2 (exponential) and type 3 (stitching)
///     function evaluators used by shadings. Functions are built from inline dictionaries, so the
///     <see cref="PdfDocumentCore" /> argument is only dereferenced for indirect references (none here);
///     a minimal single-page core satisfies the signature.
/// </summary>
public sealed class PdfFunctionTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfDictionary Dict(IReadOnlyDictionary<string, PdfObject> entries) => new(entries);

    private static PdfArray Array(params double[] values) =>
        new(values.Select(static PdfObject (v) => new PdfReal(v)).ToList());

    [Fact]
    public void Build_NonFunctionObject_ReturnsNull() => PdfFunction.Build(new PdfInteger(5), Core()).ShouldBeNull();

    [Fact]
    public void Build_NullObject_ReturnsNull() => PdfFunction.Build(null, Core()).ShouldBeNull();

    [Fact]
    public void Type2_Eval_AtStart_ReturnsC0()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0, 0.0, 0.0),
                ["C1"] = Array(1.0, 0.5, 0.25),
                ["N"] = new PdfReal(1.0)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();

        var result = fn.Eval(0.0);
        result[0].ShouldBe(0.0, 0.0001);
        result[1].ShouldBe(0.0, 0.0001);
        result[2].ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public void Type2_Eval_AtEnd_ReturnsC1()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0, 0.0, 0.0),
                ["C1"] = Array(1.0, 0.5, 0.25),
                ["N"] = new PdfReal(1.0)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();

        var result = fn.Eval(1.0);
        result[0].ShouldBe(1.0, 0.0001);
        result[1].ShouldBe(0.5, 0.0001);
        result[2].ShouldBe(0.25, 0.0001);
    }

    [Fact]
    public void Type2_Eval_Linear_Midpoint_Interpolates()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0),
                ["C1"] = Array(1.0),
                ["N"] = new PdfReal(1.0)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();

        fn.Eval(0.5)[0].ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public void Type2_Eval_ExponentN2_AppliesPower()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0),
                ["C1"] = Array(1.0),
                ["N"] = new PdfReal(2.0)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();

        // 0.5^2 = 0.25.
        fn.Eval(0.5)[0].ShouldBe(0.25, 0.0001);
    }

    [Fact]
    public void Type2_Eval_ClampsToDomain()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0),
                ["C1"] = Array(1.0),
                ["N"] = new PdfReal(1.0)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();

        fn.Eval(-5.0)[0].ShouldBe(0.0, 0.0001);
        fn.Eval(5.0)[0].ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public void Type2_Defaults_C0AndC1WhenAbsent()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();

        // Defaults: C0 = [0], C1 = [1].
        fn.Eval(0.0)[0].ShouldBe(0.0, 0.0001);
        fn.Eval(1.0)[0].ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public void Type3_Stitching_RoutesToSubFunctions()
    {
        var sub0 = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0),
                ["C1"] = Array(1.0),
                ["N"] = new PdfReal(1.0)
            }
        );
        var sub1 = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(10.0),
                ["C1"] = Array(20.0),
                ["N"] = new PdfReal(1.0)
            }
        );
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(3),
                ["Domain"] = Array(0, 1),
                ["Functions"] = new PdfArray([sub0, sub1]),
                ["Bounds"] = Array(0.5),
                ["Encode"] = Array(0, 1, 0, 1)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();

        // x=0.25 falls in the first sub-function's range [0,0.5].
        fn.Eval(0.25)[0].ShouldBeInRange(0.0, 1.0);
        // x=0.75 falls in the second sub-function's range [0.5,1]; outputs are 10–20.
        fn.Eval(0.75)[0].ShouldBeInRange(10.0, 20.0);
    }

    [Fact]
    public void Type3_NoSubFunctions_ReturnsNull()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(3),
                ["Domain"] = Array(0, 1),
                ["Functions"] = new PdfArray([]),
                ["Bounds"] = Array(),
                ["Encode"] = Array()
            }
        );
        PdfFunction.Build(dict, Core()).ShouldBeNull();
    }

    [Fact]
    public void UnsupportedType_WithC0C1_FallsBackToInterpolation()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(0),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0),
                ["C1"] = Array(1.0)
            }
        );
        var fn = PdfFunction.Build(dict, Core());
        fn.ShouldNotBeNull();
        fn.Eval(0.5)[0].ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public void UnsupportedType_WithoutC0C1_ReturnsNull()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(4),
                ["Domain"] = Array(0, 1)
            }
        );
        PdfFunction.Build(dict, Core()).ShouldBeNull();
    }

    [Fact]
    public void Build_FromStream_UsesStreamDictionary()
    {
        var dict = Dict(
            new Dictionary<string, PdfObject>
            {
                ["FunctionType"] = new PdfInteger(2),
                ["Domain"] = Array(0, 1),
                ["C0"] = Array(0.0),
                ["C1"] = Array(1.0),
                ["N"] = new PdfReal(1.0)
            }
        );
        var stream = new PdfStream(dict, ReadOnlyMemory<byte>.Empty);
        var fn = PdfFunction.Build(stream, Core());
        fn.ShouldNotBeNull();
        fn.Eval(1.0)[0].ShouldBe(1.0, 0.0001);
    }
}
