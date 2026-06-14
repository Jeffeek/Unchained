using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Core;

/// <summary>
///     Direct unit tests for stream encryption/decryption round-trips using a fixed key,
///     isolating <see cref="PdfEncryptionContext" /> from the surrounding PDF serialization.
/// </summary>
public sealed class PdfEncryptionContextTests
{
    private static byte[] Key(int length)
    {
        var key = new byte[length];
        for (var i = 0; i < length; i++)
            key[i] = (byte)i;
        return key;
    }

    [Fact]
    public void EncryptThenDecryptStream_AES256_Roundtrip()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256);
        var plain = "Hello, encrypted world!"u8.ToArray();
        var encrypted = ctx.EncryptStream(plain, 5, 0);
        var decrypted = ctx.DecryptStream(encrypted, 5, 0);

        decrypted.ShouldBe(plain);
    }

    [Fact]
    public void EncryptThenDecryptStream_AES128_Roundtrip()
    {
        var ctx = new PdfEncryptionContext(Key(16), PdfEncryptionAlgorithm.Aes128);
        var plain = "AES-128 payload"u8.ToArray();
        var encrypted = ctx.EncryptStream(plain, 7, 0);
        ctx.DecryptStream(encrypted, 7, 0).ShouldBe(plain);
    }

    [Fact]
    public void EncryptThenDecryptStream_Rc4_Roundtrip()
    {
        var ctx = new PdfEncryptionContext(Key(16), PdfEncryptionAlgorithm.Rc4_128);
        var plain = "RC4 payload bytes"u8.ToArray();
        var encrypted = ctx.EncryptStream(plain, 3, 0);
        ctx.DecryptStream(encrypted, 3, 0).ShouldBe(plain);
    }

    [Fact]
    public void DecryptStream_Empty_ReturnsEmpty()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256);
        ctx.DecryptStream(ReadOnlySpan<byte>.Empty, 1, 0).ShouldBeEmpty();
    }

    [Fact]
    public void Algorithm_And_Permissions_Exposed()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256, PdfPermissions.Print);
        ctx.Algorithm.ShouldBe(PdfEncryptionAlgorithm.Aes256);
        ctx.Permissions.ShouldBe(PdfPermissions.Print);
    }

    [Fact]
    public void Permissions_DefaultsToAll()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256);
        ctx.Permissions.ShouldBe(PdfPermissions.All);
    }

    [Fact]
    public void EncryptThenDecryptObject_String_Roundtrip()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256);
        var original = new PdfIndirectObject(4, 0, PdfString.FromLatin1("secret text"));

        var encrypted = ctx.EncryptObject(original);
        var decrypted = ctx.DecryptObject(encrypted);

        var s = decrypted.Value.ShouldBeOfType<PdfString>();
        Encoding.Latin1.GetString(s.Bytes.Span).ShouldBe("secret text");
    }

    [Fact]
    public void EncryptThenDecryptObject_Dictionary_WithNestedString_Roundtrip()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256);
        var dict = new PdfDictionary(
            new Dictionary<string, PdfObject>
            {
                ["Title"] = PdfString.FromLatin1("My Document"),
                ["Count"] = new PdfInteger(3)
            }
        );
        var original = new PdfIndirectObject(6, 0, dict);

        var decrypted = ctx.DecryptObject(ctx.EncryptObject(original));

        var resultDict = decrypted.Value.ShouldBeOfType<PdfDictionary>();
        var title = resultDict["Title"].ShouldBeOfType<PdfString>();
        Encoding.Latin1.GetString(title.Bytes.Span).ShouldBe("My Document");
        resultDict.Get<PdfInteger>("Count")!.Value.ShouldBe(3);
    }

    [Fact]
    public void EncryptThenDecryptObject_Array_WithStrings_Roundtrip()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256);
        var array = new PdfArray([PdfString.FromLatin1("a"), PdfString.FromLatin1("b")]);
        var original = new PdfIndirectObject(8, 0, array);

        var decrypted = ctx.DecryptObject(ctx.EncryptObject(original));

        var resultArray = decrypted.Value.ShouldBeOfType<PdfArray>();
        Encoding.Latin1.GetString(((PdfString)resultArray[0]).Bytes.Span).ShouldBe("a");
    }

    [Fact]
    public void EncryptObject_NonEncryptableValue_ReturnsSameInstance()
    {
        var ctx = new PdfEncryptionContext(Key(32), PdfEncryptionAlgorithm.Aes256);
        var original = new PdfIndirectObject(2, 0, new PdfInteger(99));
        ctx.EncryptObject(original).ShouldBeSameAs(original);
    }
}
