using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Tests.Helpers;
using Xunit;

namespace Unchained.Pdf.Tests.IntegrationTests;

/// <summary>
/// Tests for M8 encryption: AES-256 write, password-protected read, round-trips.
/// </summary>
public sealed class EncryptionTests : PdfTestBase
{
    // ── Encryption round-trips ────────────────────────────────────────────────

    [Fact]
    public async Task Encrypt_WithUserPassword_RoundTripRestoresPageCount()
    {
        await using var original = await LoadAsync(PdfFixtures.MultiPage(count: 3));
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "secret"));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        var bytes = ms.ToArray();
        await using var reopened = await Processor.LoadAsync(new MemoryStream(bytes), "secret");
        reopened.PageCount.ShouldBe(3);
    }

    [Fact]
    public async Task Encrypt_EmptyPassword_CanBeOpenedWithoutPassword()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: string.Empty));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        // An empty user password means anyone can open it (but it is still encrypted).
        var bytes = ms.ToArray();
        await using var reopened = await Processor.LoadAsync(new MemoryStream(bytes), string.Empty);
        reopened.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Encrypt_IsEncrypted_TrueAfterRoundTrip()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "test123"));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        var bytes = ms.ToArray();
        await using var reopened = await Processor.LoadAsync(new MemoryStream(bytes), "test123");
        reopened.IsEncrypted.ShouldBeTrue();
    }

    [Fact]
    public async Task Encrypt_UnencryptedDocument_IsEncryptedFalse()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        doc.IsEncrypted.ShouldBeFalse();
    }

    [Fact]
    public async Task Encrypt_TextSurvivesRoundTrip()
    {
        const string content = "Hello encrypted world";
        await using var original = await LoadAsync(PdfFixtures.WithTextContent(content));
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "abc123"));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        var bytes = ms.ToArray();
        await using var reopened = await Processor.LoadAsync(new MemoryStream(bytes), "abc123");
        var text = reopened.Pages[1].ExtractText();
        text.ShouldContain("Hello");
    }

    [Fact]
    public async Task Encrypt_WithOwnerAndUserPassword_BothWork()
    {
        await using var original = await LoadAsync(PdfFixtures.MultiPage(count: 2));
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(
            UserPassword: "user",
            OwnerPassword: "owner"));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);
        var bytes = ms.ToArray();

        // User password works
        await using var byUser = await Processor.LoadAsync(new MemoryStream(bytes), "user");
        byUser.PageCount.ShouldBe(2);

        // Owner password works
        await using var byOwner = await Processor.LoadAsync(new MemoryStream(bytes), "owner");
        byOwner.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task Encrypt_SavedPdfStartsWithPdfHeader()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "pw"));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        ms.Position = 0;
        var header = new byte[5];
        _ = ms.Read(header, 0, 5);
        header.ShouldBe("%PDF-"u8.ToArray());
    }

    [Fact]
    public async Task Encrypt_TableDocument_RoundTrip()
    {
        var data = PdfFixtures.SimpleTableData(rows: 5);
        var gen = new Engine.TableGenerator();
        await using var original = await gen.GenerateAsync(data, TableStyle.Default);

        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "table"));
        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        var bytes = ms.ToArray();
        await using var reopened = await Processor.LoadAsync(new MemoryStream(bytes), "table");
        reopened.PageCount.ShouldBe(original.PageCount);
    }

    // ── Permissions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Permissions_UnencryptedDocument_ReturnsAll()
    {
        await using var doc = await LoadAsync(PdfFixtures.SinglePage());
        doc.Permissions.ShouldBe(PdfPermissions.All);
    }

    [Fact]
    public async Task Permissions_EncryptedWithAllPermissions_ReturnsAll()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "pw", Permissions: PdfPermissions.All));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        await using var reopened = await Processor.LoadAsync(new MemoryStream(ms.ToArray()), "pw");
        reopened.Permissions.HasFlag(PdfPermissions.Print).ShouldBeTrue();
        reopened.Permissions.HasFlag(PdfPermissions.Copy).ShouldBeTrue();
    }

    [Fact]
    public async Task Permissions_RestrictedPermissions_RoundTrip()
    {
        const PdfPermissions restricted = PdfPermissions.Print;
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "pw", Permissions: restricted));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);

        await using var reopened = await Processor.LoadAsync(new MemoryStream(ms.ToArray()), "pw");
        reopened.Permissions.HasFlag(PdfPermissions.Print).ShouldBeTrue();
        reopened.Permissions.HasFlag(PdfPermissions.Copy).ShouldBeFalse();
        reopened.Permissions.HasFlag(PdfPermissions.Modify).ShouldBeFalse();
    }

    // ── ChangePasswords ───────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswords_NewPasswordWorks()
    {
        // Encrypt, then change password from "old" to "new"
        await using var original = await LoadAsync(PdfFixtures.MultiPage(count: 2));
        var firstMs = new MemoryStream();
        await Processor.SaveAsync(original, firstMs, new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "old")));

        await using var decrypted = await Processor.LoadAsync(new MemoryStream(firstMs.ToArray()), "old");
        var changedMs = new MemoryStream();
        await Processor.ChangePasswordsAsync(decrypted, "new", "new", changedMs);

        // Old password no longer works
        await Should.ThrowAsync<PdfEncryptedException>(() => Processor.LoadAsync(new MemoryStream(changedMs.ToArray()), "old"));

        // New password works
        await using var reopened = await Processor.LoadAsync(new MemoryStream(changedMs.ToArray()), "new");
        reopened.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task ChangePasswords_EmptyPasswords_RemovesEncryption()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encMs = new MemoryStream();
        await Processor.SaveAsync(original, encMs, new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "secret")));

        await using var decrypted = await Processor.LoadAsync(new MemoryStream(encMs.ToArray()), "secret");
        var decryptedMs = new MemoryStream();
        await Processor.ChangePasswordsAsync(decrypted, string.Empty, string.Empty, decryptedMs);

        // No password needed anymore
        await using var plain = await Processor.LoadAsync(new MemoryStream(decryptedMs.ToArray()));
        plain.IsEncrypted.ShouldBeFalse();
        plain.PageCount.ShouldBe(1);
    }

    [Fact]
    public async Task ChangePasswords_DifferentUserAndOwnerPasswords_BothWork()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encMs = new MemoryStream();
        await Processor.SaveAsync(original, encMs, new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "initial")));

        await using var decrypted = await Processor.LoadAsync(new MemoryStream(encMs.ToArray()), "initial");
        var changedMs = new MemoryStream();
        await Processor.ChangePasswordsAsync(decrypted, "user2", "owner2", changedMs);

        // Both new passwords work
        await using var byUser = await Processor.LoadAsync(new MemoryStream(changedMs.ToArray()), "user2");
        byUser.PageCount.ShouldBe(1);

        await using var byOwner = await Processor.LoadAsync(new MemoryStream(changedMs.ToArray()), "owner2");
        byOwner.PageCount.ShouldBe(1);
    }

    // ── Wrong-password rejection ───────────────────────────────────────────────

    [Fact]
    public async Task Encrypt_WrongPassword_ThrowsPdfEncryptedException()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "correct"));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);
        var bytes = ms.ToArray();

        await Should.ThrowAsync<PdfEncryptedException>(() => Processor.LoadAsync(new MemoryStream(bytes), "wrong"));
    }

    [Fact]
    public async Task Encrypt_EmptyPasswordOnProtectedDoc_ThrowsPdfEncryptedException()
    {
        await using var original = await LoadAsync(PdfFixtures.SinglePage());
        var encOpts = new SaveOptions(Encryption: new EncryptionOptions(UserPassword: "secret"));

        using var ms = new MemoryStream();
        await Processor.SaveAsync(original, ms, encOpts);
        var bytes = ms.ToArray();

        // Loading without password should fail
        await Should.ThrowAsync<PdfEncryptedException>(() => Processor.LoadAsync(new MemoryStream(bytes)));
    }
}
