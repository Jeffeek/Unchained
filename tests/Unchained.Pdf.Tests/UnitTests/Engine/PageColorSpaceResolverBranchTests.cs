using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Document;
using Unchained.Pdf.Engine.PageResources;
using Unchained.Pdf.Tests.Shared;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Engine;

/// <summary>
///     Additional <see cref="PageColorSpaceResolver" /> tests covering branches the base
///     <see cref="PageColorSpaceResolverTests" /> does not reach: ICCBased N=1/4 channel
///     inference, explicit /Alternate, Indexed stream lookup, and the bare /Pattern name skip.
/// </summary>
public sealed class PageColorSpaceResolverBranchTests
{
    private static PdfDocumentCore Core() => PdfDocumentCore.Parse(PdfFixtures.SinglePage());

    private static PdfDictionary PageWith(string name, PdfObject space)
    {
        var resources = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["ColorSpace"] = new PdfDictionary(new Dictionary<string, PdfObject> { [name] = space })
            }
        );
        return new PdfDictionary(
            new Dictionary<string, PdfObject> { ["Type"] = PdfName.Page, ["Resources"] = resources }
        );
    }

    [Fact]
    public void IccBased_N1_InfersDeviceGray()
    {
        var iccStream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["N"] = new PdfInteger(1) }),
            ReadOnlyMemory<byte>.Empty
        );
        var space = new PdfArray([PdfName.Get("ICCBased"), iccStream]);
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", space), Core());
        result["CS0"].AlternateSpace.ShouldBe("DeviceGray");
    }

    [Fact]
    public void IccBased_N4_InfersDeviceCmyk()
    {
        var iccStream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject> { ["N"] = new PdfInteger(4) }),
            ReadOnlyMemory<byte>.Empty
        );
        var space = new PdfArray([PdfName.Get("ICCBased"), iccStream]);
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", space), Core());
        result["CS0"].AlternateSpace.ShouldBe("DeviceCMYK");
    }

    [Fact]
    public void IccBased_ExplicitAlternate_UsesAlternateName()
    {
        var iccStream = new PdfStream(
            new PdfDictionary(
                new Dictionary<string, PdfObject>
                {
                    ["N"] = new PdfInteger(3),
                    ["Alternate"] = PdfName.Get("DeviceCMYK")
                }
            ),
            ReadOnlyMemory<byte>.Empty
        );
        var space = new PdfArray([PdfName.Get("ICCBased"), iccStream]);
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", space), Core());
        result["CS0"].AlternateSpace.ShouldBe("DeviceCMYK");
    }

    [Fact]
    public void Indexed_StreamLookup_BuildsPalette()
    {
        var lookupStream = new PdfStream(
            new PdfDictionary(new Dictionary<string, PdfObject>()),
            new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 } // 2 RGB entries
        );
        var space = new PdfArray(
            [
                PdfName.Get("Indexed"),
                PdfName.Get("DeviceRGB"),
                new PdfInteger(1),
                lookupStream
            ]
        );
        var result = PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", space), Core());
        result["CS0"].Kind.ShouldBe("Indexed");
        result["CS0"].IndexedBaseChannels.ShouldBe(3);
    }

    [Fact]
    public void Indexed_EmptyLookup_IsSkipped()
    {
        var space = new PdfArray(
            [
                PdfName.Get("Indexed"),
                PdfName.Get("DeviceRGB"),
                new PdfInteger(0),
                PdfString.FromLatin1(string.Empty)
            ]
        );
        PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", space), Core()).ContainsKey("CS0").ShouldBeFalse();
    }

    [Fact]
    public void PatternName_IsSkipped() =>
        PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", PdfName.Get("Pattern")), Core())
            .ContainsKey("CS0")
            .ShouldBeFalse();

    [Fact]
    public void EmptyArray_IsSkipped() =>
        PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", new PdfArray([])), Core())
            .ContainsKey("CS0")
            .ShouldBeFalse();

    [Fact]
    public void UnknownArrayKind_IsSkipped()
    {
        var space = new PdfArray([PdfName.Get("Bogus"), new PdfDictionary()]);
        PageColorSpaceResolver.GetColorSpaces(PageWith("CS0", space), Core()).ContainsKey("CS0").ShouldBeFalse();
    }
}
