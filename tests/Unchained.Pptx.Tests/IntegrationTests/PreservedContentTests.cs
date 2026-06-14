using System.IO.Compression;
using System.IO.Packaging;
using System.Text;
using Shouldly;
using Unchained.Pptx.Core.Xml;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Verbatim round-trip of content Unchained does not model: the VBA macro project
///     (<c>vbaProject.bin</c>) and digital-signature parts (M-G).
/// </summary>
public sealed class PreservedContentTests : PptxTestBase
{
    /// <summary>Produces a base PPTX (1 slide) as bytes.</summary>
    private async Task<byte[]> BaseDeckAsync()
    {
        var doc = PptxFixtures.WithSlides(1);
        using var ms = new MemoryStream();
        await Processor.SaveAsync(doc, ms);
        return ms.ToArray();
    }

    /// <summary>Injects a vbaProject.bin part + presentation relationship into a deck.</summary>
    private static byte[] InjectVba(byte[] pptx, byte[] vbaBytes)
    {
        using var ms = new MemoryStream();
        ms.Write(pptx, 0, pptx.Length);
        ms.Position = 0;

        using (var pkg = Package.Open(ms, FileMode.Open, FileAccess.ReadWrite))
        {
            var vbaUri = new Uri("/ppt/vbaProject.bin", UriKind.Relative);
            var part = pkg.CreatePart(vbaUri, PmlNames.ContentTypeVbaProject);
            using (var s = part.GetStream(FileMode.Create))
                s.Write(vbaBytes, 0, vbaBytes.Length);

            var presUri = new Uri("/ppt/presentation.xml", UriKind.Relative);
            var pres = pkg.GetPart(presUri);
            pres.CreateRelationship(
                new Uri("vbaProject.bin", UriKind.Relative),
                TargetMode.Internal,
                PmlNames.RelTypeVbaProject
            );
        }

        return ms.ToArray();
    }

    /// <summary>Injects a signature origin part + one signature part into a deck.</summary>
    private static byte[] InjectSignature(byte[] pptx, byte[] signatureXml)
    {
        using var ms = new MemoryStream();
        ms.Write(pptx, 0, pptx.Length);
        ms.Position = 0;

        using (var pkg = Package.Open(ms, FileMode.Open, FileAccess.ReadWrite))
        {
            var originUri = new Uri("/_xmlsignatures/origin.sigs", UriKind.Relative);
            var origin = pkg.CreatePart(originUri, PmlNames.ContentTypeDigitalSignatureOrigin);
            using (origin.GetStream(FileMode.Create))
            {
                /* empty origin marker */
            }

            pkg.CreateRelationship(originUri, TargetMode.Internal, PmlNames.RelTypeDigitalSignatureOrigin);

            var sigUri = new Uri("/_xmlsignatures/sig1.xml", UriKind.Relative);
            var sig = pkg.CreatePart(sigUri, PmlNames.ContentTypeDigitalSignature);
            using (var s = sig.GetStream(FileMode.Create))
                s.Write(signatureXml, 0, signatureXml.Length);

            origin.CreateRelationship(
                new Uri("sig1.xml", UriKind.Relative),
                TargetMode.Internal,
                PmlNames.RelTypeDigitalSignature
            );
        }

        return ms.ToArray();
    }

    private static bool PartExists(byte[] pptx, string partName)
    {
        using var ms = new MemoryStream(pptx);
        using var archive = new ZipArchive(ms);
        return archive.GetEntry(partName) != null;
    }

    private static byte[] PartBytes(byte[] pptx, string partName)
    {
        using var ms = new MemoryStream(pptx);
        using var archive = new ZipArchive(ms);
        var entry = archive.GetEntry(partName)!;
        using var s = entry.Open();
        using var outMs = new MemoryStream();
        s.CopyTo(outMs);
        return outMs.ToArray();
    }

    [Fact]
    public async Task Vba_RoundTrips_AndFlagsMacros()
    {
        var vbaBytes = new byte[] { 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 1, 2, 3 };
        var withVba = InjectVba(await BaseDeckAsync(), vbaBytes);

        var doc = await Processor.LoadAsync(withVba);
        doc.HasMacros.ShouldBeTrue();

        using var outMs = new MemoryStream();
        await Processor.SaveAsync(doc, outMs);
        var saved = outMs.ToArray();

        PartExists(saved, "ppt/vbaProject.bin").ShouldBeTrue("VBA project must survive round-trip");
        PartBytes(saved, "ppt/vbaProject.bin").ShouldBe(vbaBytes, "VBA bytes must be byte-identical");
    }

    [Fact]
    public async Task Vba_PresentationUsesMacroEnabledContentType()
    {
        var withVba = InjectVba(await BaseDeckAsync(), [0xCF, 0x11, 0xE0, 0xA1]);

        var doc = await Processor.LoadAsync(withVba);
        using var outMs = new MemoryStream();
        await Processor.SaveAsync(doc, outMs);
        var saved = outMs.ToArray();

        var ct = Encoding.UTF8.GetString(PartBytes(saved, "[Content_Types].xml"));
        ct.ShouldContain(
            "presentation.macroEnabled.main+xml",
            customMessage: "a deck with macros must declare the macro-enabled content type"
        );
    }

    [Fact]
    public async Task Signatures_RoundTripVerbatim()
    {
        var sigXml = "<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><SignatureValue>AAAA</SignatureValue></Signature>"u8.ToArray();
        var withSig = InjectSignature(await BaseDeckAsync(), sigXml);

        var doc = await Processor.LoadAsync(withSig);
        using var outMs = new MemoryStream();
        await Processor.SaveAsync(doc, outMs);
        var saved = outMs.ToArray();

        PartExists(saved, "_xmlsignatures/origin.sigs").ShouldBeTrue("signature origin must survive");
        PartExists(saved, "_xmlsignatures/sig1.xml").ShouldBeTrue("signature part must survive");
        PartBytes(saved, "_xmlsignatures/sig1.xml").ShouldBe(sigXml, "signature XML must be byte-identical");
    }

    [Fact]
    public async Task NoPreservedContent_ForPlainDeck()
    {
        var doc = await Processor.LoadAsync(await BaseDeckAsync());
        doc.HasMacros.ShouldBeFalse();

        using var outMs = new MemoryStream();
        await Processor.SaveAsync(doc, outMs);
        PartExists(outMs.ToArray(), "ppt/vbaProject.bin").ShouldBeFalse();
    }
}
