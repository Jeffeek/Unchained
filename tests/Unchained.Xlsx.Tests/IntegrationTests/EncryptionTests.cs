using Shouldly;
using Unchained.Xlsx.Core;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Models;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

public class EncryptionTests
{
    [Fact]
    public async Task EncryptDecrypt_RoundTrips()
    {
        using var document = XlsxFixtures.WithSheets("Secret");
        document.Sheets[0].SetValue(1, 1, "classified");

        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream();
        await processor.SaveAsync(
            document,
            ms,
            new XlsxSaveOptions { Password = "hunter2" },
            TestContext.Current.CancellationToken
        );

        using var reloaded = await processor.LoadAsync(
            ms.ToArray(),
            new OpenOptions { Password = "hunter2" },
            TestContext.Current.CancellationToken
        );

        reloaded.WasLoadedEncrypted.ShouldBeTrue();
        reloaded.Sheets[0].GetCell(1, 1)!.GetString().ShouldBe("classified");
    }

    [Fact]
    public async Task Load_EncryptedWithoutPassword_Throws()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream();
        await processor.SaveAsync(
            document,
            ms,
            new XlsxSaveOptions { Password = "pw" },
            TestContext.Current.CancellationToken
        );
        var bytes = ms.ToArray();

        await Should.ThrowAsync<SpreadsheetEncryptedException>(async () => await processor.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Load_WrongPassword_Throws()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        using var processor = new SpreadsheetProcessor();
        using var ms = new MemoryStream();
        await processor.SaveAsync(
            document,
            ms,
            new XlsxSaveOptions { Password = "correct" },
            TestContext.Current.CancellationToken
        );
        var bytes = ms.ToArray();

        await Should.ThrowAsync<SpreadsheetEncryptedException>(async () => await processor.LoadAsync(
                bytes,
                new OpenOptions { Password = "wrong" },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task RemoveEncryption_ThenSave_ProducesUnencryptedFile()
    {
        using var document = XlsxFixtures.WithSheets("Data");
        using var processor = new SpreadsheetProcessor();
        using var encrypted = new MemoryStream();
        await processor.SaveAsync(
            document,
            encrypted,
            new XlsxSaveOptions { Password = "pw" },
            TestContext.Current.CancellationToken
        );

        using var reloaded = await processor.LoadAsync(
            encrypted.ToArray(),
            new OpenOptions { Password = "pw" },
            TestContext.Current.CancellationToken
        );
        reloaded.RemoveEncryption();

        using var plain = new MemoryStream();
        await processor.SaveAsync(reloaded, plain, cancellationToken: TestContext.Current.CancellationToken);

        // Re-loads with no password.
        using var final = await processor.LoadAsync(
            plain.ToArray(),
            cancellationToken: TestContext.Current.CancellationToken
        );
        final.WasLoadedEncrypted.ShouldBeFalse();
    }
}
