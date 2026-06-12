using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Core;

public sealed class PdfBooleanTests
{
    [Fact]
    public void True_IsSingleton() =>
        PdfBoolean.True.ShouldBeSameAs(PdfBoolean.True);

    [Fact]
    public void False_IsSingleton() =>
        PdfBoolean.False.ShouldBeSameAs(PdfBoolean.False);

    [Fact]
    public void TrueAndFalse_AreDifferentInstances() =>
        PdfBoolean.True.ShouldNotBeSameAs(PdfBoolean.False);

    [
        Theory,
        InlineData(true),
        InlineData(false)
    ]
    public void FromBool_ReturnsSingleton(bool value) =>
        PdfBoolean.FromBool(value).ShouldBeSameAs(value ? PdfBoolean.True : PdfBoolean.False);

    [Fact]
    public void Value_True_IsTrue() => PdfBoolean.True.Value.ShouldBeTrue();

    [Fact]
    public void Value_False_IsFalse() => PdfBoolean.False.Value.ShouldBeFalse();

    [Fact]
    public void ToString_True_ReturnsTrue() => PdfBoolean.True.ToString().ShouldBe("true");

    [Fact]
    public void ToString_False_ReturnsFalse() => PdfBoolean.False.ToString().ShouldBe("false");
}

public sealed class PdfIntegerTests
{
    [
        Theory,
        InlineData(0),
        InlineData(42),
        InlineData(-7),
        InlineData(long.MaxValue),
        InlineData(long.MinValue)
    ]
    public void Value_StoredCorrectly(long value) =>
        new PdfInteger(value).Value.ShouldBe(value);

    [Fact]
    public void ToString_ReturnsDecimalString() =>
        new PdfInteger(-123).ToString().ShouldBe("-123");
}

public sealed class PdfRealTests
{
    [
        Theory,
        InlineData(0.0),
        InlineData(3.14),
        InlineData(-0.002)
    ]
    public void Value_StoredCorrectly(double value) =>
        new PdfReal(value).Value.ShouldBe(value);
}

public sealed class PdfStringTests
{
    [Fact]
    public void IsHex_DefaultsFalse()
    {
        var s = new PdfString("Hi"u8.ToArray());
        s.IsHex.ShouldBeFalse();
    }

    [Fact]
    public void IsHex_TrueWhenSet()
    {
        var s = new PdfString("Hi"u8.ToArray(), true);
        s.IsHex.ShouldBeTrue();
    }

    [Fact]
    public void Bytes_StoredCorrectly()
    {
        var data = "ABC"u8.ToArray();
        var s = new PdfString(data);
        s.Bytes.ToArray().ShouldBe(data);
    }

    [Fact]
    public void FromLatin1_EncodesCorrectly()
    {
        var s = PdfString.FromLatin1("Hello");
        Encoding.Latin1.GetString(s.Bytes.Span).ShouldBe("Hello");
        s.IsHex.ShouldBeFalse();
    }

    [Fact]
    public void FromUtf16_EncodesAsBigEndian()
    {
        var s = PdfString.FromUtf16("A");
        s.Bytes.Length.ShouldBe(2);
        s.Bytes.Span[0].ShouldBe((byte)0x00);
        s.Bytes.Span[1].ShouldBe((byte)0x41);
    }
}

public sealed class PdfNameTests
{
    [Fact]
    public void Get_SameStringTwice_ReturnsSameInstance()
    {
        var a = PdfName.Get("Foo");
        var b = PdfName.Get("Foo");
        a.ShouldBeSameAs(b);
    }

    [Fact]
    public void Get_DifferentStrings_ReturnDifferentInstances() =>
        PdfName.Get("Foo").ShouldNotBeSameAs(PdfName.Get("Bar"));

    [Fact]
    public void Value_DoesNotContainSlash() =>
        PdfName.Type.Value.ShouldBe("Type");

    [Fact]
    public void ToString_HasLeadingSlash() =>
        PdfName.Type.ToString().ShouldBe("/Type");

    [Fact]
    public void Equals_SameInstance_True() =>
        PdfName.Type.Equals(PdfName.Type).ShouldBeTrue();

    [Fact]
    public void Equals_DifferentName_False() =>
        PdfName.Type.Equals(PdfName.Page).ShouldBeFalse();

    [Fact]
    public void Equals_Null_False() =>
        PdfName.Type.Equals(null).ShouldBeFalse();

    [Fact]
    public void GetHashCode_SameValue_Equal() =>
        PdfName.Get("X").GetHashCode().ShouldBe(PdfName.Get("X").GetHashCode());

    [
        Theory,
        InlineData("Type"),
        InlineData("Subtype"),
        InlineData("Page"),
        InlineData("Pages"),
        InlineData("Catalog"),
        InlineData("Kids"),
        InlineData("Count"),
        InlineData("MediaBox"),
        InlineData("Root"),
        InlineData("Info"),
        InlineData("Size"),
        InlineData("Length"),
        InlineData("Filter"),
        InlineData("Prev")
    ]
    public void PreInterned_MatchesGet(string name) =>
        PdfName.Get(name).ShouldBeSameAs(PdfName.Get(name));
}

public sealed class PdfArrayTests
{
    [Fact]
    public void Empty_HasZeroCount() => PdfArray.Empty.Count.ShouldBe(0);

    [Fact]
    public void Empty_IsSingleton() => PdfArray.Empty.ShouldBeSameAs(PdfArray.Empty);

    [Fact]
    public void Elements_StoredInOrder()
    {
        var items = new PdfObject[] { new PdfInteger(1), new PdfInteger(2), new PdfInteger(3) };
        var array = new PdfArray(items);
        array.Elements.ShouldBe(items);
    }

    [Fact]
    public void Count_MatchesElementCount()
    {
        var array = new PdfArray([new PdfInteger(1), new PdfInteger(2)]);
        array.Count.ShouldBe(2);
    }

    [Fact]
    public void Indexer_ReturnsCorrectElement()
    {
        var first = new PdfInteger(10);
        var array = new PdfArray([first, new PdfInteger(20)]);
        array[0].ShouldBeSameAs(first);
    }
}

public sealed class PdfStreamTests
{
    [Fact]
    public void Dictionary_StoredCorrectly()
    {
        var dict = new PdfDictionary();
        var stream = new PdfStream(dict, ReadOnlyMemory<byte>.Empty);
        stream.Dictionary.ShouldBeSameAs(dict);
    }

    [Fact]
    public void Data_StoredCorrectly()
    {
        var data = new byte[] { 1, 2, 3 };
        var stream = new PdfStream(new PdfDictionary(), data);
        stream.Data.ToArray().ShouldBe(data);
    }

    [Fact]
    public void DeclaredLength_ReadsFromLengthEntry()
    {
        var dict = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Length.Value] = new PdfInteger(42)
        });
        var stream = new PdfStream(dict, ReadOnlyMemory<byte>.Empty);
        stream.DeclaredLength.ShouldBe(42);
    }

    [Fact]
    public void DeclaredLength_FallsBackToDataLength_WhenLengthAbsent()
    {
        var data = new byte[7];
        var stream = new PdfStream(new PdfDictionary(), data);
        stream.DeclaredLength.ShouldBe(7);
    }
}

public sealed class PdfNullTests
{
    [Fact]
    public void Instance_IsSingleton() =>
        PdfNull.Instance.ShouldBeSameAs(PdfNull.Instance);

    [Fact]
    public void ToString_ReturnsNull() =>
        PdfNull.Instance.ToString().ShouldBe("null");
}

public sealed class PdfIndirectReferenceTests
{
    [Fact]
    public void Properties_StoredCorrectly()
    {
        var r = new PdfIndirectReference(5, 2);
        r.ObjectNumber.ShouldBe(5);
        r.Generation.ShouldBe(2);
    }

    [Fact]
    public void Equals_SameValues_True() =>
        new PdfIndirectReference(1, 0).Equals(new PdfIndirectReference(1, 0)).ShouldBeTrue();

    [Fact]
    public void Equals_DifferentObjectNumber_False() =>
        new PdfIndirectReference(1, 0).Equals(new PdfIndirectReference(2, 0)).ShouldBeFalse();

    [Fact]
    public void Equals_DifferentGeneration_False() =>
        new PdfIndirectReference(1, 0).Equals(new PdfIndirectReference(1, 1)).ShouldBeFalse();

    [Fact]
    public void Equals_Null_False() =>
        new PdfIndirectReference(1, 0).Equals(null).ShouldBeFalse();

    [Fact]
    public void GetHashCode_SameValues_Equal() =>
        new PdfIndirectReference(3, 0).GetHashCode()
            .ShouldBe(new PdfIndirectReference(3, 0).GetHashCode());

    [Fact]
    public void ToString_PdfSyntax() =>
        new PdfIndirectReference(5, 2).ToString().ShouldBe("5 2 R");
}

public sealed class PdfIndirectObjectTests
{
    [Fact]
    public void Properties_StoredCorrectly()
    {
        var value = new PdfInteger(99);
        var obj = new PdfIndirectObject(3, 1, value);
        obj.ObjectNumber.ShouldBe(3);
        obj.Generation.ShouldBe(1);
        obj.Value.ShouldBeSameAs(value);
    }

    [Fact]
    public void ToReference_MatchesObjectNumberAndGeneration()
    {
        var obj = new PdfIndirectObject(7, 0, PdfNull.Instance);
        var r = obj.ToReference();
        r.ObjectNumber.ShouldBe(7);
        r.Generation.ShouldBe(0);
    }
}
