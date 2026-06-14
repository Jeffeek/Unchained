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
    [Fact]
    public void EncryptThenDecryptStream_AES256_Roundtrip()
    {
        var key = new byte[32];
        for (var i = 0; i < 32; i++)
            key[i] = (byte)i;

        var ctx = new PdfEncryptionContext(key, PdfEncryptionAlgorithm.Aes256);
        var plain = "Hello, encrypted world!"u8.ToArray();
        var encrypted = ctx.EncryptStream(plain, 5, 0);
        var decrypted = ctx.DecryptStream(encrypted, 5, 0);

        decrypted.ShouldBe(plain);
    }
}
