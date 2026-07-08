using Shouldly;
using Unchained.Pdf.Core;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Core;

public sealed class CrossReferenceEntryTests
{
    [Fact]
    public void IsFree_FreeType_True() =>
        new CrossReferenceEntry(0, 65535, CrossReferenceEntryType.Free).IsFree.ShouldBeTrue();

    [Fact]
    public void IsFree_InUseType_False() =>
        new CrossReferenceEntry(100, 0, CrossReferenceEntryType.InUse).IsFree.ShouldBeFalse();

    [Fact]
    public void IsFree_CompressedType_False() =>
        new CrossReferenceEntry(0, 0, CrossReferenceEntryType.Compressed).IsFree.ShouldBeFalse();

    [Fact]
    public void Properties_StoredCorrectly()
    {
        var entry = new CrossReferenceEntry(1234567L, 3, CrossReferenceEntryType.InUse);
        entry.Offset.ShouldBe(1234567L);
        entry.Generation.ShouldBe(3);
        entry.Type.ShouldBe(CrossReferenceEntryType.InUse);
    }
}

public sealed class CrossReferenceTableTests
{
    private static CrossReferenceTable Table(params (int ObjNum, CrossReferenceEntry Entry)[] entries)
    {
        var d = new Dictionary<int, CrossReferenceEntry>();
        foreach (var (n, e) in entries) d[n] = e;
        return new CrossReferenceTable(d, 0);
    }

    [Fact]
    public void Count_ReturnsNumberOfEntries()
    {
        var table = Table(
            (1, new CrossReferenceEntry(10, 0, CrossReferenceEntryType.InUse)),
            (2, new CrossReferenceEntry(50, 0, CrossReferenceEntryType.InUse))
        );
        table.Count.ShouldBe(2);
    }

    [Fact]
    public void TryGetEntry_PresentObject_ReturnsTrueAndEntry()
    {
        var entry = new CrossReferenceEntry(999, 0, CrossReferenceEntryType.InUse);
        var table = Table((5, entry));
        var found = table.TryGetEntry(5, out var result);
        found.ShouldBeTrue();
        result.Offset.ShouldBe(999);
    }

    [Fact]
    public void TryGetEntry_AbsentObject_ReturnsFalse() =>
        Table().TryGetEntry(99, out _).ShouldBeFalse();

    [Fact]
    public void GetEntry_PresentObject_ReturnsEntry()
    {
        var entry = new CrossReferenceEntry(42, 0, CrossReferenceEntryType.InUse);
        var table = Table((1, entry));
        table.GetEntry(1).Offset.ShouldBe(42);
    }

    [Fact]
    public void GetEntry_AbsentObject_ThrowsPdfException()
    {
        var table = Table();
        Should.Throw<PdfException>(() => table.GetEntry(7));
    }

    [Fact]
    public void TrailerOffset_StoredCorrectly()
    {
        var table = new CrossReferenceTable(new Dictionary<int, CrossReferenceEntry>(), 512L);
        table.TrailerOffset.ShouldBe(512L);
    }

    [Fact]
    public void InUseObjectNumbers_ReturnsNonFreeInAscendingOrder()
    {
        var table = Table(
            (5, new CrossReferenceEntry(100, 0, CrossReferenceEntryType.InUse)),
            (2, new CrossReferenceEntry(50, 0, CrossReferenceEntryType.InUse)),
            (1, new CrossReferenceEntry(10, 0, CrossReferenceEntryType.Free))
        );
        var nums = table.InUseObjectNumbers.ToList();
        nums.Count.ShouldBe(2);
        nums[0].ShouldBe(2);
        nums[1].ShouldBe(5);
    }
}
