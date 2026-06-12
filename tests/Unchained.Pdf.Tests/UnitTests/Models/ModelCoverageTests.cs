using System.Text;
using Shouldly;
using Unchained.Pdf.Core;
using Unchained.Pdf.Models;
using Unchained.Pdf.Parsing;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Models;

public sealed class PdfEncryptedExceptionTests
{
    [Fact]
    public async Task MessageConstructor_SetsMessage()
    {
        await Task.CompletedTask;
        var ex = new PdfEncryptedException("document is locked");
        ex.Message.ShouldBe("document is locked");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public async Task InnerExceptionConstructor_SetsMessageAndInner()
    {
        await Task.CompletedTask;
        var inner = new InvalidOperationException("crypto failure");
        var ex = new PdfEncryptedException("decrypt failed", inner);
        ex.Message.ShouldBe("decrypt failed");
        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task IsAssignableToException()
    {
        await Task.CompletedTask;
        var ex = new PdfEncryptedException("x");
        ex.ShouldBeAssignableTo<Exception>();
    }
}

public sealed class PdfAValidationResultTests
{
    [Fact]
    public async Task IsConformant_NoViolations_ReturnsTrue()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations = []
        };
        result.IsConformant.ShouldBeTrue();
    }

    [Fact]
    public async Task IsConformant_OnlyWarnings_ReturnsTrue()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA2B,
            Violations =
            [
                new PdfAViolation("6.1.1", "Optional metadata missing", PdfAViolationSeverity.Warning)
            ]
        };
        result.IsConformant.ShouldBeTrue();
    }

    [Fact]
    public async Task IsConformant_HasError_ReturnsFalse()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations =
            [
                new PdfAViolation("6.3.3", "Font not embedded")
            ]
        };
        result.IsConformant.ShouldBeFalse();
    }

    [Fact]
    public async Task Errors_FiltersOnlyErrors()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations =
            [
                new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error),
                new PdfAViolation("6.1.1", "Optional metadata", PdfAViolationSeverity.Warning),
                new PdfAViolation("6.2.1", "Color space issue", PdfAViolationSeverity.Error)
            ]
        };
        result.Errors.Count.ShouldBe(2);
        result.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ToString_Conformant_IncludesProfile()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations = []
        };
        var str = result.ToString();
        str.ShouldContain("Conformant");
        str.ShouldContain("0 warning(s)");
    }

    [Fact]
    public async Task ToString_NonConformant_IncludesErrorAndWarningCounts()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA2B,
            Violations =
            [
                new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error),
                new PdfAViolation("6.1.1", "Optional metadata", PdfAViolationSeverity.Warning)
            ]
        };
        var str = result.ToString();
        str.ShouldContain("Non-conformant");
        str.ShouldContain("1 error(s)");
        str.ShouldContain("1 warning(s)");
    }

    [Fact]
    public async Task ToString_ConformantWithWarnings_IncludesWarningCount()
    {
        await Task.CompletedTask;
        var result = new PdfAValidationResult
        {
            Profile = PdfAProfile.PdfA1B,
            Violations =
            [
                new PdfAViolation("6.1.1", "Warn1", PdfAViolationSeverity.Warning),
                new PdfAViolation("6.1.2", "Warn2", PdfAViolationSeverity.Warning)
            ]
        };
        var str = result.ToString();
        str.ShouldContain("Conformant");
        str.ShouldContain("2 warning(s)");
    }
}

public sealed class PdfAViolationTests
{
    [Fact]
    public async Task RequiredConstructor_SetsRuleIdAndDescription()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font not embedded");
        v.RuleId.ShouldBe("6.3.3");
        v.Description.ShouldBe("Font not embedded");
        v.Severity.ShouldBe(PdfAViolationSeverity.Error);
        v.ObjectNumber.ShouldBeNull();
        v.PageNumber.ShouldBeNull();
    }

    [Fact]
    public async Task OptionalSeverity_Warning()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.1.1", "Metadata issue", PdfAViolationSeverity.Warning);
        v.Severity.ShouldBe(PdfAViolationSeverity.Warning);
    }

    [Fact]
    public async Task ObjectNumber_Stored()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error, 42);
        v.ObjectNumber.ShouldBe(42);
        v.PageNumber.ShouldBeNull();
    }

    [Fact]
    public async Task PageNumber_Stored()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font issue", PdfAViolationSeverity.Error, PageNumber: 3);
        v.PageNumber.ShouldBe(3);
        v.ObjectNumber.ShouldBeNull();
    }

    [Fact]
    public async Task AllParameters_Stored()
    {
        await Task.CompletedTask;
        var v = new PdfAViolation("6.3.3", "Font issue", PdfAViolationSeverity.Warning, 7, 2);
        v.RuleId.ShouldBe("6.3.3");
        v.Description.ShouldBe("Font issue");
        v.Severity.ShouldBe(PdfAViolationSeverity.Warning);
        v.ObjectNumber.ShouldBe(7);
        v.PageNumber.ShouldBe(2);
    }

    [Fact]
    public async Task RecordEquality_SameValues_AreEqual()
    {
        await Task.CompletedTask;
        var a = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error, 10, 1);
        var b = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error, 10, 1);
        a.ShouldBe(b);
    }

    [Fact]
    public async Task RecordEquality_DifferentSeverity_NotEqual()
    {
        await Task.CompletedTask;
        var a = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Error);
        var b = new PdfAViolation("6.3.3", "Font not embedded", PdfAViolationSeverity.Warning);
        a.ShouldNotBe(b);
    }

    [Fact]
    public async Task SeverityEnum_HasBothValues()
    {
        await Task.CompletedTask;
        Enum.IsDefined(PdfAViolationSeverity.Error).ShouldBeTrue();
        Enum.IsDefined(PdfAViolationSeverity.Warning).ShouldBeTrue();
    }
}

public sealed class MdLoadOptionsTests
{
    [Fact]
    public async Task Default_HasExpectedValues()
    {
        await Task.CompletedTask;
        var opts = MdLoadOptions.Default;
        opts.BodyFontName.ShouldBe("Helvetica");
        opts.BodyFontSize.ShouldBe(11f);
        opts.CodeFontName.ShouldBe("Courier");
        opts.CodeFontSize.ShouldBe(10f);
        opts.LineSpacing.ShouldBe(1.4f);
        opts.ParagraphSpacingPt.ShouldBe(8f);
        opts.MarginPt.ShouldBe(72f);
        opts.PageWidthPt.ShouldBe(595f);
        opts.PageHeightPt.ShouldBe(842f);
    }

    [Fact]
    public async Task CustomConstructor_StoresAllValues()
    {
        await Task.CompletedTask;
        var opts = new MdLoadOptions(
            "Times-Roman",
            12f,
            "Courier-Bold",
            9f,
            1.5f,
            10f,
            36f,
            612f,
            792f
        );
        opts.BodyFontName.ShouldBe("Times-Roman");
        opts.BodyFontSize.ShouldBe(12f);
        opts.CodeFontName.ShouldBe("Courier-Bold");
        opts.CodeFontSize.ShouldBe(9f);
        opts.LineSpacing.ShouldBe(1.5f);
        opts.ParagraphSpacingPt.ShouldBe(10f);
        opts.MarginPt.ShouldBe(36f);
        opts.PageWidthPt.ShouldBe(612f);
        opts.PageHeightPt.ShouldBe(792f);
    }

    [
        Theory,
        InlineData(1, 22f),   // 11 * 2.0
        InlineData(2, 17.6f), // 11 * 1.6
        InlineData(3, 14.3f), // 11 * 1.3
        InlineData(4, 12.1f), // 11 * 1.1
        InlineData(5, 11f),   // 11 * 1.0
        InlineData(6, 9.9f)   // 11 * 0.9
    ]
    public async Task HeadingFontSize_DefaultBodyFont_ReturnsExpectedSize(int level, float expected)
    {
        await Task.CompletedTask;
        var opts = MdLoadOptions.Default;
        opts.HeadingFontSize(level).ShouldBe(expected, 0.01f);
    }

    [Fact]
    public async Task HeadingFontSize_CustomBodyFontSize_ScalesCorrectly()
    {
        await Task.CompletedTask;
        var opts = new MdLoadOptions(BodyFontSize: 10f);
        opts.HeadingFontSize(1).ShouldBe(20f, 0.01f);
        opts.HeadingFontSize(2).ShouldBe(16f, 0.01f);
        opts.HeadingFontSize(3).ShouldBe(13f, 0.01f);
    }

    [Fact]
    public async Task HeadingFontSize_LevelAbove6_UsesBodyTimes09()
    {
        await Task.CompletedTask;
        var opts = MdLoadOptions.Default;
        // level 7 hits the default branch: BodyFontSize * 0.9
        opts.HeadingFontSize(7).ShouldBe(opts.BodyFontSize * 0.9f, 0.01f);
    }

    [Fact]
    public async Task WithExpression_CreatesModifiedCopy()
    {
        await Task.CompletedTask;
        var original = MdLoadOptions.Default;
        var modified = original with { BodyFontSize = 14f };
        modified.BodyFontSize.ShouldBe(14f);
        original.BodyFontSize.ShouldBe(11f);
    }
}

public sealed class RenderOptionsTests
{
    [Fact]
    public async Task Default_Has150DpiAndPng()
    {
        await Task.CompletedTask;
        RenderOptions.Default.Dpi.ShouldBe(150);
        RenderOptions.Default.Format.ShouldBe(OutputFormat.Png);
    }

    [Fact]
    public async Task HighRes_Has300Dpi()
    {
        await Task.CompletedTask;
        RenderOptions.HighRes.Dpi.ShouldBe(300);
        RenderOptions.HighRes.Format.ShouldBe(OutputFormat.Png);
    }

    [Fact]
    public async Task CustomDpi_Stored()
    {
        await Task.CompletedTask;
        var opts = new RenderOptions(72);
        opts.Dpi.ShouldBe(72);
        opts.Format.ShouldBe(OutputFormat.Png);
    }

    [Fact]
    public async Task RecordEquality_SameDpi_Equal()
    {
        await Task.CompletedTask;
        var a = new RenderOptions(200);
        var b = new RenderOptions(200);
        a.ShouldBe(b);
    }

    [Fact]
    public async Task OutputFormat_PngIsDefined()
    {
        await Task.CompletedTask;
        Enum.IsDefined(OutputFormat.Png).ShouldBeTrue();
    }

    [Fact]
    public async Task WithExpression_CreatesModifiedCopy()
    {
        await Task.CompletedTask;
        var original = RenderOptions.Default;
        var modified = original with { Dpi = 600 };
        modified.Dpi.ShouldBe(600);
        original.Dpi.ShouldBe(150);
    }
}

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
        var a = new EncryptionOptions("pw", "own", PdfEncryptionAlgorithm.Aes256, PdfPermissions.All);
        var b = new EncryptionOptions("pw", "own", PdfEncryptionAlgorithm.Aes256, PdfPermissions.All);
        a.ShouldBe(b);
    }
}

public sealed class PdfTokenTests
{
    private static PdfToken MakeToken(PdfTokenKind kind, string raw = "x", long offset = 0)
    {
        var bytes = Encoding.Latin1.GetBytes(raw);
        return new PdfToken(kind, bytes, offset);
    }

    [Fact]
    public async Task Is_MatchingKind_ReturnsTrue()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "42");
        token.Is(PdfTokenKind.Integer).ShouldBeTrue();
    }

    [Fact]
    public async Task Is_DifferentKind_ReturnsFalse()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "42");
        token.Is(PdfTokenKind.Real).ShouldBeFalse();
    }

    [
        Theory,
        InlineData(PdfTokenKind.Integer),
        InlineData(PdfTokenKind.Real),
        InlineData(PdfTokenKind.Name),
        InlineData(PdfTokenKind.LiteralString),
        InlineData(PdfTokenKind.HexString),
        InlineData(PdfTokenKind.BooleanTrue),
        InlineData(PdfTokenKind.BooleanFalse),
        InlineData(PdfTokenKind.Null),
        InlineData(PdfTokenKind.DictionaryBegin),
        InlineData(PdfTokenKind.DictionaryEnd),
        InlineData(PdfTokenKind.ArrayBegin),
        InlineData(PdfTokenKind.ArrayEnd),
        InlineData(PdfTokenKind.Stream),
        InlineData(PdfTokenKind.EndStream),
        InlineData(PdfTokenKind.Obj),
        InlineData(PdfTokenKind.EndObj),
        InlineData(PdfTokenKind.IndirectRef),
        InlineData(PdfTokenKind.Xref),
        InlineData(PdfTokenKind.Trailer),
        InlineData(PdfTokenKind.StartXref),
        InlineData(PdfTokenKind.Comment),
        InlineData(PdfTokenKind.EndOfFile)
    ]
    public async Task Is_EachKind_MatchesItself(PdfTokenKind kind)
    {
        await Task.CompletedTask;
        var token = MakeToken(kind);
        token.Is(kind).ShouldBeTrue();
    }

    [Fact]
    public async Task Kind_StoredCorrectly()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Name, "/Type");
        token.Kind.ShouldBe(PdfTokenKind.Name);
    }

    [Fact]
    public async Task Offset_StoredCorrectly()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "99", 0x200);
        token.Offset.ShouldBe(0x200L);
    }

    [Fact]
    public async Task Raw_ContainsOriginalBytes()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Name, "/MyName");
        Encoding.Latin1.GetString(token.Raw.Span).ShouldBe("/MyName");
    }

    [Fact]
    public async Task ToString_ContainsKindOffsetAndRaw()
    {
        await Task.CompletedTask;
        var token = MakeToken(PdfTokenKind.Integer, "42", 0x10);
        var str = token.ToString();
        str.ShouldContain("Integer");
        str.ShouldContain("0x10");
        str.ShouldContain("42");
    }
}
