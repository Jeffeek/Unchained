using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Tests.Helpers;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

public sealed class EncryptionTests : PptxTestBase
{
    // ── Encrypt / Decrypt round-trip ──────────────────────────────────────────

    [Fact]
    public async Task Encrypt_SaveBytes_AreNotRawZip()
    {
        var doc = PptxFixtures.WithSlides(2);
        var processor = new PresentationProcessor();
        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "secret" });

        var bytes = ms.ToArray();
        // ZIP starts with PK\x03\x04; CFB (encrypted) starts with D0 CF 11 E0
        bytes[0].ShouldBe((byte)0xD0);
        bytes[1].ShouldBe((byte)0xCF);
    }

    [Fact]
    public async Task RoundTrip_EncryptDecrypt_SlideCountPreserved()
    {
        var doc = PptxFixtures.WithSlides(3);
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "mypassword" });
        ms.Position = 0;

        var reloaded = await processor.LoadAsync(ms, new OpenOptions { Password = "mypassword" });
        reloaded.Slides.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RoundTrip_EncryptDecrypt_SlideContentPreserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Slides[0]
            .Shapes.AddTextBox(
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(4),
                Emu.FromInches(2),
                "Encrypted content"
            );

        var processor = new PresentationProcessor();
        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "pass123" });
        ms.Position = 0;

        var reloaded = await processor.LoadAsync(ms, new OpenOptions { Password = "pass123" });
        reloaded.Slides[0].GetAllText().ShouldContain("Encrypted content");
    }

    [Fact]
    public async Task RoundTrip_EncryptDecrypt_SlideSize_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "abc" });
        ms.Position = 0;

        var reloaded = await processor.LoadAsync(ms, new OpenOptions { Password = "abc" });
        reloaded.SlideSize.Width.ShouldBe(doc.SlideSize.Width);
        reloaded.SlideSize.Height.ShouldBe(doc.SlideSize.Height);
    }

    [Fact]
    public async Task RoundTrip_PasswordWithSpecialChars_Works()
    {
        var doc = PptxFixtures.WithSlides(1);
        const string password = "P@ssw0rd!#$%^&*()";
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = password });
        ms.Position = 0;

        var reloaded = await processor.LoadAsync(ms, new OpenOptions { Password = password });
        reloaded.Slides.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RoundTrip_MultipleSlides_AllPreserved()
    {
        var doc = PptxFixtures.WithSlides(5);
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "test" });
        ms.Position = 0;

        var reloaded = await processor.LoadAsync(ms, new OpenOptions { Password = "test" });
        reloaded.Slides.Count.ShouldBe(5);
    }

    // ── Wrong password rejection ──────────────────────────────────────────────

    [Fact]
    public async Task WrongPassword_ThrowsPptxEncryptedException()
    {
        var doc = PptxFixtures.WithSlides(1);
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "correctpassword" });
        ms.Position = 0;

        await Should.ThrowAsync<PptxEncryptedException>(() => processor.LoadAsync(ms, new OpenOptions { Password = "wrongpassword" }));
    }

    [Fact]
    public async Task NoPassword_ThrowsPptxEncryptedException()
    {
        var doc = PptxFixtures.WithSlides(1);
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "secret" });
        ms.Position = 0;

        await Should.ThrowAsync<PptxEncryptedException>(() => processor.LoadAsync(ms)); // no password supplied
    }

    [Fact]
    public async Task EmptyPassword_ThrowsPptxEncryptedException()
    {
        var doc = PptxFixtures.WithSlides(1);
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "secret" });
        ms.Position = 0;

        await Should.ThrowAsync<PptxEncryptedException>(() => processor.LoadAsync(ms, new OpenOptions { Password = string.Empty }));
    }

    // ── IsEncrypted flag ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoadEncrypted_ProtectionIsEncrypted_IsTrue()
    {
        var doc = PptxFixtures.WithSlides(1);
        var processor = new PresentationProcessor();

        var ms = new MemoryStream();
        await processor.SaveAsync(doc, ms, new SaveOptions { Password = "pwd" });
        ms.Position = 0;

        var reloaded = await processor.LoadAsync(ms, new OpenOptions { Password = "pwd" });
        // The loaded file was decrypted; IsEncrypted reflects the source was encrypted
        // (set during parse when CFB is detected)
        reloaded.Protection.IsEncrypted.ShouldBeTrue();
    }

    // ── Write-protection ──────────────────────────────────────────────────────

    [Fact]
    public void SetWriteProtection_IsWriteProtectedTrue()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("editpwd");
        doc.Protection.IsWriteProtected.ShouldBeTrue();
    }

    [Fact]
    public void RemoveWriteProtection_IsWriteProtectedFalse()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("editpwd");
        doc.Protection.RemoveWriteProtection();
        doc.Protection.IsWriteProtected.ShouldBeFalse();
    }

    [Fact]
    public void CheckWriteProtection_CorrectPassword_ReturnsTrue()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("myeditpwd");
        doc.Protection.CheckWriteProtection("myeditpwd").ShouldBeTrue();
    }

    [Fact]
    public void CheckWriteProtection_WrongPassword_ReturnsFalse()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("myeditpwd");
        doc.Protection.CheckWriteProtection("wrongpwd").ShouldBeFalse();
    }

    [Fact]
    public async Task RoundTrip_WriteProtection_Preserved()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("editpwd");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Protection.IsWriteProtected.ShouldBeTrue();
    }

    [Fact]
    public async Task RoundTrip_WriteProtection_PasswordVerifiesAfterReload()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("editpwd");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Protection.CheckWriteProtection("editpwd").ShouldBeTrue();
    }

    [Fact]
    public async Task RoundTrip_WriteProtection_WrongPasswordReturnsFalseAfterReload()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("editpwd");

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Protection.CheckWriteProtection("wrongpwd").ShouldBeFalse();
    }

    [Fact]
    public async Task RoundTrip_NoWriteProtection_IsWriteProtectedFalse()
    {
        var doc = PptxFixtures.WithSlides(1);

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Protection.IsWriteProtected.ShouldBeFalse();
    }

    [Fact]
    public async Task RoundTrip_RemoveWriteProtection_PersistedAsRemoved()
    {
        var doc = PptxFixtures.WithSlides(1);
        doc.Protection.SetWriteProtection("pwd");
        doc.Protection.RemoveWriteProtection();

        var reloaded = await PptxFixtures.RoundTripAsync(doc);
        reloaded.Protection.IsWriteProtected.ShouldBeFalse();
    }
}
