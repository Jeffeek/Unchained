using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Document;

public sealed class ObjectGraphBuilderTests
{
    [Fact]
    public void Add_FirstObject_GetsObjectNumber1()
    {
        var builder = new ObjectGraphBuilder();
        var obj = builder.Add(PdfNull.Instance);
        obj.ObjectNumber.ShouldBe(1);
    }

    [Fact]
    public void Add_MultipleObjects_NumbersAreSequential()
    {
        var builder = new ObjectGraphBuilder();
        var a = builder.Add(PdfNull.Instance);
        var b = builder.Add(PdfBoolean.True);
        var c = builder.Add(new PdfInteger(42));
        a.ObjectNumber.ShouldBe(1);
        b.ObjectNumber.ShouldBe(2);
        c.ObjectNumber.ShouldBe(3);
    }

    [Fact]
    public void Add_ObjectAppearsInObjects()
    {
        var builder = new ObjectGraphBuilder();
        var obj = builder.Add(new PdfInteger(7));
        builder.Objects.ShouldContain(obj);
    }

    [Fact]
    public void NextNumber_ReservesNumberWithoutAddingToList()
    {
        var builder = new ObjectGraphBuilder();
        var reserved = builder.NextNumber();
        builder.Objects.ShouldBeEmpty();
        reserved.ShouldBe(1);
    }

    [Fact]
    public void NextNumber_ThenAdd_NumbersAreSequential()
    {
        var builder = new ObjectGraphBuilder();
        var reserved = builder.NextNumber(); // 1 reserved
        var obj = builder.Add(PdfNull.Instance); // should get 2
        reserved.ShouldBe(1);
        obj.ObjectNumber.ShouldBe(2);
    }

    [Fact]
    public void AddAt_FillsReservedSlot()
    {
        var builder = new ObjectGraphBuilder();
        var reserved = builder.NextNumber();
        var filled = builder.AddAt(reserved, new PdfInteger(99));
        filled.ObjectNumber.ShouldBe(reserved);
        builder.Objects.ShouldContain(filled);
    }

    [Fact]
    public void MaxObjectNumber_ReflectsAllAllocatedNumbers()
    {
        var builder = new ObjectGraphBuilder();
        builder.Add(PdfNull.Instance);
        builder.NextNumber();
        builder.MaxObjectNumber.ShouldBe(2);
    }

    [Fact]
    public void StartAt_OverridesDefaultStartNumber()
    {
        var builder = new ObjectGraphBuilder(startAt: 10);
        var obj = builder.Add(PdfNull.Instance);
        obj.ObjectNumber.ShouldBe(10);
    }

    [Fact]
    public void Finalize_ProducesParseableDocument()
    {
        var builder = new ObjectGraphBuilder();
        var pagesNum = builder.NextNumber();
        var pagesRef = new PdfIndirectReference(pagesNum, 0);
        var pageObj = builder.Add(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Page,
            [PdfName.Parent.Value] = pagesRef,
            [PdfName.MediaBox.Value] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(595), new PdfInteger(842)])
        }));
        builder.AddAt(
            pagesNum,
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                [PdfName.Type.Value] = PdfName.Pages,
                [PdfName.Kids.Value] = new PdfArray([pageObj.ToReference()]),
                [PdfName.Count.Value] = new PdfInteger(1)
            }));
        var catalogObj = builder.Add(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Type.Value] = PdfName.Catalog,
            [PdfName.Pages.Value] = pagesRef
        }));

        using var doc = ObjectGraphBuilder.Finalize(builder, catalogObj.ToReference());

        doc.PageCount.ShouldBe(1);
    }

    [Fact]
    public void SerializeToDocument_WithPrebuiltList_ProducesParseableDocument()
    {
        var objects = new List<PdfIndirectObject>
        {
            new(
                1,
                0,
                new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    [PdfName.Type.Value] = PdfName.Catalog,
                    [PdfName.Pages.Value] = new PdfIndirectReference(2, 0)
                })),
            new(
                2,
                0,
                new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    [PdfName.Type.Value] = PdfName.Pages,
                    [PdfName.Kids.Value] = new PdfArray([new PdfIndirectReference(3, 0)]),
                    [PdfName.Count.Value] = new PdfInteger(1)
                })),
            new(
                3,
                0,
                new PdfDictionary(new Dictionary<string, PdfObject>
                {
                    [PdfName.Type.Value] = PdfName.Page,
                    [PdfName.Parent.Value] = new PdfIndirectReference(2, 0),
                    [PdfName.MediaBox.Value] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(595), new PdfInteger(842)])
                }))
        };
        var trailer = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [PdfName.Size.Value] = new PdfInteger(4),
            [PdfName.Root.Value] = new PdfIndirectReference(1, 0)
        });

        using var doc = ObjectGraphBuilder.SerializeToDocument(objects, trailer);

        doc.PageCount.ShouldBe(1);
    }
}
