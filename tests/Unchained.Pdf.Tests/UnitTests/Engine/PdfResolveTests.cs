using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Unit tests for the <see cref="PdfResolve" /> extension helpers — numeric conversions,
///     object/dictionary resolution, form bbox/matrix reads, and colour-space name resolution.
///     A minimal single-page core supplies the resolver context (no indirect refs are followed
///     for the direct-object cases).
/// </summary>
public sealed class PdfResolveTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    // ── Numeric conversions ───────────────────────────────────────────────────

    [Fact]
    public void ReadIntOrReal_Integer_ReturnsValue() =>
        new PdfInteger(42).ReadIntOrReal().ShouldBe(42.0);

    [Fact]
    public void ReadIntOrReal_Real_ReturnsValue() =>
        new PdfReal(3.5).ReadIntOrReal().ShouldBe(3.5);

    [Fact]
    public void ReadIntOrReal_NonNumeric_ReturnsFallback() =>
        PdfName.Page.ReadIntOrReal(9.0).ShouldBe(9.0);

    [Fact]
    public void ReadIntOrRealNullable_NonNumeric_ReturnsNull() =>
        PdfNull.Instance.ReadIntOrRealNullable().ShouldBeNull();

    [Fact]
    public void ReadInt_Real_Truncates() =>
        new PdfReal(7.9).ReadInt().ShouldBe(7);

    [Fact]
    public void ReadFloat_Integer_Converts() =>
        new PdfInteger(5).ReadFloat().ShouldBe(5f);

    [Fact]
    public void ReadLong_Integer_Converts() =>
        new PdfInteger(123).ReadLong().ShouldBe(123L);

    [Fact]
    public void ReadDoubleArray_ParsesElements()
    {
        var arr = new PdfArray([new PdfInteger(1), new PdfReal(2.5), new PdfInteger(3)]);
        arr.ReadDoubleArray().ShouldBe([1.0, 2.5, 3.0]);
    }

    [Fact]
    public void ReadDoubleArray_NonArray_ReturnsNull() =>
        new PdfInteger(1).ReadDoubleArray().ShouldBeNull();

    [Fact]
    public void ReadFloatArray_ParsesElements()
    {
        var arr = new PdfArray([new PdfReal(1.5), new PdfReal(2.5)]);
        arr.ReadFloatArray().ShouldBe([1.5f, 2.5f]);
    }

    // ── Object / dictionary resolution ────────────────────────────────────────

    [Fact]
    public void ResolveDict_DirectDictionary_ReturnsIt()
    {
        var dict = new PdfDictionary();
        Core().ResolveDict(dict).ShouldBeSameAs(dict);
    }

    [Fact]
    public void ResolveDict_NonDictionary_ReturnsNull() =>
        Core().ResolveDict(new PdfInteger(1)).ShouldBeNull();

    [Fact]
    public void ResolveAny_DirectObject_ReturnsSame()
    {
        var obj = new PdfInteger(5);
        Core().ResolveAny(obj).ShouldBeSameAs(obj);
    }

    [Fact]
    public void ResolveDictOrStreamDict_Stream_ReturnsStreamDict()
    {
        var dict = new PdfDictionary(new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page });
        var stream = new PdfStream(dict, ReadOnlyMemory<byte>.Empty);
        Core().ResolveDictOrStreamDict(stream).ShouldBeSameAs(dict);
    }

    [Fact]
    public void ResolveDictOrStreamDict_Dictionary_ReturnsIt()
    {
        var dict = new PdfDictionary();
        Core().ResolveDictOrStreamDict(dict).ShouldBeSameAs(dict);
    }

    // ── Form bbox / matrix ────────────────────────────────────────────────────

    [Fact]
    public void GetFormBBox_ReadsArray()
    {
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["BBox"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(200)])
            }
        );
        var form = new PdfStream(dict, ReadOnlyMemory<byte>.Empty);
        form.GetFormBBox().ShouldBe([0, 0, 100, 200]);
    }

    [Fact]
    public void GetFormBBox_Missing_ReturnsUnitBox()
    {
        var form = new PdfStream(new PdfDictionary(), ReadOnlyMemory<byte>.Empty);
        form.GetFormBBox().ShouldBe([0, 0, 1, 1]);
    }

    [Fact]
    public void GetFormMatrix_ReadsArray()
    {
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Matrix"] = new PdfArray(
                    [
                        new PdfInteger(2), new PdfInteger(0), new PdfInteger(0),
                        new PdfInteger(2), new PdfInteger(5), new PdfInteger(6)
                    ]
                )
            }
        );
        var form = new PdfStream(dict, ReadOnlyMemory<byte>.Empty);
        form.GetFormMatrix().ShouldBe([2, 0, 0, 2, 5, 6]);
    }

    [Fact]
    public void GetFormMatrix_Missing_ReturnsIdentity()
    {
        var form = new PdfStream(new PdfDictionary(), ReadOnlyMemory<byte>.Empty);
        form.GetFormMatrix().ShouldBe([1, 0, 0, 1, 0, 0]);
    }

    // ── Colour-space name resolution ──────────────────────────────────────────

    [Fact]
    public void ReadColorSpace_DirectName_ReturnsName()
    {
        var dict = new PdfDictionary(new Dictionary<string, PdfObject> { ["ColorSpace"] = PdfName.Get("DeviceRGB") });
        Core().ReadColorSpace(dict).ShouldBe("DeviceRGB");
    }

    [Fact]
    public void ReadColorSpace_IndexedArray_ReturnsBaseSpace()
    {
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfArray([PdfName.Get("Indexed"), PdfName.Get("DeviceRGB")])
            }
        );
        Core().ReadColorSpace(dict).ShouldBe("DeviceRGB");
    }

    [Fact]
    public void ReadColorSpace_Missing_ReturnsNull() =>
        Core().ReadColorSpace(new PdfDictionary()).ShouldBeNull();

    [Fact]
    public void ResolveBaseSpaceName_CalRgb_MapsToDeviceRgb()
    {
        var obj = new PdfArray([PdfName.Get("CalRGB"), new PdfDictionary()]);
        Core().ResolveBaseSpaceName(obj).ShouldBe("DeviceRGB");
    }

    [Fact]
    public void ResolveBaseSpaceName_CalGray_MapsToDeviceGray()
    {
        var obj = new PdfArray([PdfName.Get("CalGray"), new PdfDictionary()]);
        Core().ResolveBaseSpaceName(obj).ShouldBe("DeviceGray");
    }

    [Fact]
    public void ResolveBaseSpaceName_DirectName_ReturnsName() =>
        Core().ResolveBaseSpaceName(PdfName.Get("DeviceCMYK")).ShouldBe("DeviceCMYK");

    // ── Additional branch coverage ────────────────────────────────────────────

    [Fact]
    public void ResolveDictOrStreamDict_NonDictNonStream_ReturnsNull() =>
        Core().ResolveDictOrStreamDict(new PdfInteger(7)).ShouldBeNull();

    [Fact]
    public void ResolveDictOrStreamDict_Null_ReturnsNull() =>
        Core().ResolveDictOrStreamDict(null).ShouldBeNull();

    private static PdfStream IccStream(int n)
    {
        var dict = new PdfDictionary(new Dictionary<string, PdfObject> { ["N"] = new PdfInteger(n) });
        return new PdfStream(dict, ReadOnlyMemory<byte>.Empty);
    }

    [
        Theory,
        InlineData(1, "DeviceGray"),
        InlineData(3, "DeviceRGB"),
        InlineData(4, "DeviceCMYK")
    ]
    public void ReadColorSpace_IccBasedDirectStream_MapsByChannelCount(int n, string expected)
    {
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfArray([PdfName.Get("ICCBased"), IccStream(n)])
            }
        );
        Core().ReadColorSpace(dict).ShouldBe(expected);
    }

    [Fact]
    public void ReadColorSpace_IccBasedUnknownChannelCount_ReturnsNull()
    {
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfArray([PdfName.Get("ICCBased"), IccStream(2)])
            }
        );
        Core().ReadColorSpace(dict).ShouldBeNull();
    }

    [Fact]
    public void ReadColorSpace_UnknownArrayKind_ReturnsNull()
    {
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfArray([PdfName.Get("Separation"), PdfName.Get("DeviceRGB")])
            }
        );
        Core().ReadColorSpace(dict).ShouldBeNull();
    }

    [
        Theory,
        InlineData(1, "DeviceGray"),
        InlineData(3, "DeviceRGB"),
        InlineData(4, "DeviceCMYK")
    ]
    public void ResolveBaseSpaceName_IccBasedDirectStream_MapsByChannelCount(int n, string expected)
    {
        var obj = new PdfArray([PdfName.Get("ICCBased"), IccStream(n)]);
        Core().ResolveBaseSpaceName(obj).ShouldBe(expected);
    }

    [Fact]
    public void ResolveBaseSpaceName_Lab_MapsToDeviceRgb() =>
        Core().ResolveBaseSpaceName(new PdfArray([PdfName.Get("Lab"), new PdfDictionary()])).ShouldBe("DeviceRGB");

    [Fact]
    public void ResolveBaseSpaceName_UnknownArrayKind_ReturnsNull() =>
        Core().ResolveBaseSpaceName(new PdfArray([PdfName.Get("Pattern")])).ShouldBeNull();

    [Fact]
    public void ResolveBaseSpaceName_NonNameNonArray_ReturnsNull() =>
        Core().ResolveBaseSpaceName(new PdfInteger(3)).ShouldBeNull();

    [Fact]
    public void ResolveBaseSpaceName_Null_ReturnsNull() =>
        Core().ResolveBaseSpaceName(null).ShouldBeNull();
}
