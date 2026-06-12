using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests;

/// <summary>
///     Direct unit tests for AES-256 PDF encryption key derivation,
///     bypassing PDF serialization to isolate crypto correctness.
/// </summary>
public sealed class EncryptionCryptoTests
{
    [
        Theory,
        InlineData(""),
        InlineData("secret"),
        InlineData("test123"),
        InlineData("a very long passphrase with spaces and symbols!@#$%")
    ]
    public void CreateWriteContext_ThenReadContext_ValidatesPassword(string password)
    {
        var options = new EncryptionOptions(password);
        var fileId = new byte[16]; // zeros — deterministic

        var (_, encryptDict) = PdfEncryption.CreateWriteContext(options, fileId);

        // Without serialization round-trip: should succeed immediately
        var ctx = PdfEncryption.CreateReadContext(encryptDict, fileId, password);
        ctx.ShouldNotBeNull($"Password '{password}' should validate against freshly-created /Encrypt dict.");
    }

    [Fact]
    public void CreateWriteContext_WrongPassword_ReturnsNull()
    {
        var options = new EncryptionOptions("correct");
        var fileId = new byte[16];

        var (_, encryptDict) = PdfEncryption.CreateWriteContext(options, fileId);

        Should.Throw<PdfEncryptedException>(() => PdfEncryption.CreateReadContext(encryptDict, fileId, "wrong"));
    }

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
