using Shouldly;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class EncryptionOptionsTests
{
    [Fact]
    public async Task DefaultConstructor_HasExpectedDefaults()
    {
        await Task.CompletedTask;
        var opts = new EncryptionOptions();
        opts.UserPassword.ShouldBe("");
        opts.OwnerPassword.ShouldBe("");
        opts.Algorithm.ShouldBe(PdfEncryptionAlgorithm.Aes256);
        opts.Permissions.ShouldBe(PdfPermissions.All);
    }

    [Fact]
    public async Task CustomConstructor_StoresAllFields()
    {
        await Task.CompletedTask;
        var opts = new EncryptionOptions(
            "user123",
            "owner456",
            PdfEncryptionAlgorithm.Aes128,
            PdfPermissions.Print
        );
        opts.UserPassword.ShouldBe("user123");
        opts.OwnerPassword.ShouldBe("owner456");
        opts.Algorithm.ShouldBe(PdfEncryptionAlgorithm.Aes128);
        opts.Permissions.ShouldBe(PdfPermissions.Print);
    }

    [Fact]
    public async Task Rc4Algorithm_Stored()
    {
        await Task.CompletedTask;
        var opts = new EncryptionOptions(Algorithm: PdfEncryptionAlgorithm.Rc4_128);
        opts.Algorithm.ShouldBe(PdfEncryptionAlgorithm.Rc4_128);
    }

    [Fact]
    public async Task AlgorithmEnum_AllValuesDefined()
    {
        await Task.CompletedTask;
        Enum.IsDefined(PdfEncryptionAlgorithm.Rc4_128).ShouldBeTrue();
        Enum.IsDefined(PdfEncryptionAlgorithm.Aes128).ShouldBeTrue();
        Enum.IsDefined(PdfEncryptionAlgorithm.Aes256).ShouldBeTrue();
    }

    [Fact]
    public async Task RecordEquality_SameValues_Equal()
    {
        await Task.CompletedTask;
        var a = new EncryptionOptions("pw", "own");
        var b = new EncryptionOptions("pw", "own");
        a.ShouldBe(b);
    }
}
