using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Document;

public sealed class PdfObjectRemapperTests
{
    [Fact]
    public void Remap_IndirectReference_OffsetApplied()
    {
        var result = (PdfIndirectReference)PdfObjectRemapper.Remap(new PdfIndirectReference(3, 0), offset: 10);
        result.ObjectNumber.ShouldBe(13);
        result.Generation.ShouldBe(0);
    }

    [Fact]
    public void Remap_IndirectReference_GenerationPreserved()
    {
        var result = (PdfIndirectReference)PdfObjectRemapper.Remap(new PdfIndirectReference(1, 2), offset: 5);
        result.ObjectNumber.ShouldBe(6);
        result.Generation.ShouldBe(2);
    }

    [Fact]
    public void Remap_IndirectObject_NumberAndValueBothOffset()
    {
        var inner = new PdfIndirectReference(5, 0);
        var io = new PdfIndirectObject(2, 0, new PdfArray([inner]));
        var result = (PdfIndirectObject)PdfObjectRemapper.Remap(io, offset: 100);
        result.ObjectNumber.ShouldBe(102);
        var remappedArr = (PdfArray)result.Value;
        ((PdfIndirectReference)remappedArr[0]).ObjectNumber.ShouldBe(105);
    }

    [Fact]
    public void Remap_Array_AllReferencesOffset()
    {
        var arr = new PdfArray([
            new PdfIndirectReference(1, 0),
            new PdfIndirectReference(2, 0),
            new PdfInteger(42)
        ]);
        var result = (PdfArray)PdfObjectRemapper.Remap(arr, offset: 10);
        ((PdfIndirectReference)result[0]).ObjectNumber.ShouldBe(11);
        ((PdfIndirectReference)result[1]).ObjectNumber.ShouldBe(12);
        ((PdfInteger)result[2]).Value.ShouldBe(42);
    }

    [Fact]
    public void Remap_Dictionary_AllReferencesOffset()
    {
        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Ref"] = new PdfIndirectReference(3, 0),
            ["Lit"] = PdfName.Get("Foo")
        });
        var result = (PdfDictionary)PdfObjectRemapper.Remap(dict, offset: 7);
        ((PdfIndirectReference)result["Ref"]!).ObjectNumber.ShouldBe(10);
        ((PdfName)result["Lit"]!).Value.ShouldBe("Foo");
    }

    [Fact]
    public void Remap_Stream_DictionaryRemapped_DataPreserved()
    {
        var data = new byte[] { 1, 2, 3 };
        var streamDict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Length.Value] = new PdfIndirectReference(5, 0)
        });
        var stream = new PdfStream(streamDict, data);

        var result = (PdfStream)PdfObjectRemapper.Remap(stream, offset: 20);

        ((PdfIndirectReference)result.Dictionary[PdfName.Length]!).ObjectNumber.ShouldBe(25);
        result.Data.Span.SequenceEqual(data).ShouldBeTrue();
    }

    [
        Theory,
        InlineData(true),
        InlineData(false)
    ]
    public void Remap_PdfBoolean_ReturnsSameInstance(bool value)
    {
        var original = PdfBoolean.FromBool(value);
        PdfObjectRemapper.Remap(original, offset: 99).ShouldBeSameAs(original);
    }

    [Fact]
    public void Remap_PdfNull_ReturnsSameInstance() =>
        PdfObjectRemapper.Remap(PdfNull.Instance, offset: 5).ShouldBeSameAs(PdfNull.Instance);

    [Fact]
    public void Remap_PdfName_ReturnsSameInstance()
    {
        var name = PdfName.Get("Test");
        PdfObjectRemapper.Remap(name, offset: 3).ShouldBeSameAs(name);
    }

    [Fact]
    public void Remap_PdfInteger_ReturnsSameInstance()
    {
        var integer = new PdfInteger(42);
        PdfObjectRemapper.Remap(integer, offset: 5).ShouldBeSameAs(integer);
    }

    [Fact]
    public void Remap_NestedDictionaryInArray_DeepRemapCorrect()
    {
        var inner = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Ref"] = new PdfIndirectReference(1, 0)
        });
        var arr = new PdfArray([inner]);
        var result = (PdfArray)PdfObjectRemapper.Remap(arr, offset: 50);
        var resultDict = (PdfDictionary)result[0];
        ((PdfIndirectReference)resultDict["Ref"]!).ObjectNumber.ShouldBe(51);
    }

    [Fact]
    public void Remap_ZeroOffset_ReferencesUnchanged()
    {
        var ref1 = new PdfIndirectReference(7, 0);
        var result = (PdfIndirectReference)PdfObjectRemapper.Remap(ref1, offset: 0);
        result.ObjectNumber.ShouldBe(7);
    }
}
