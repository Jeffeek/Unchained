using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Abstractions;

/// <summary>
///     Exercises the default interface implementations on <see cref="IPdfPage" />. A minimal stub
///     supplies only the abstract members and inherits every default body, so the defaults (which
///     the real <see cref="Unchained.Pdf.Engine.PdfPageAdapter" /> overrides) are covered here.
/// </summary>
public sealed class IPdfPageDefaultsTests
{
    private static IPdfPage Page(double width, double height) => new StubPage(width, height);

    [Fact]
    public void IsLandscape_WiderThanTall_True() =>
        Page(800, 600).IsLandscape.ShouldBeTrue();

    [Fact]
    public void IsLandscape_TallerThanWide_False() =>
        Page(600, 800).IsLandscape.ShouldBeFalse();

    [Fact]
    public void IsLandscape_Square_False() =>
        Page(500, 500).IsLandscape.ShouldBeFalse();

    [Fact]
    public void GetCompositeFonts_DefaultsToEmpty() =>
        Page(1, 1).GetCompositeFonts().ShouldBeEmpty();

    [Fact]
    public void GetExtGStateAlphas_DefaultsToEmpty() =>
        Page(1, 1).GetExtGStateAlphas().ShouldBeEmpty();

    [Fact]
    public void GetSoftMasks_DefaultsToEmpty() =>
        Page(1, 1).GetSoftMasks(100, 100).ShouldBeEmpty();

    [Fact]
    public void GetShadings_DefaultsToEmpty() =>
        Page(1, 1).GetShadings().ShouldBeEmpty();

    [Fact]
    public void GetTilingPatterns_DefaultsToEmpty() =>
        Page(1, 1).GetTilingPatterns().ShouldBeEmpty();

    private sealed class StubPage(double width, double height) : IPdfPage
    {
        public int PageNumber => 1;
        public double Width => width;
        public double Height => height;
        public double CropOriginX => 0;
        public double CropOriginY => 0;
        public int Rotate => 0;

        public IReadOnlyList<ContentOperator> GetContentOperators() => throw new NotSupportedException();
        public IReadOnlyList<TextSpan> GetTextSpans() => throw new NotSupportedException();
        public string ExtractText() => throw new NotSupportedException();
        public IReadOnlyList<Annotation> GetAnnotations() => throw new NotSupportedException();
        public IReadOnlyDictionary<string, string> GetFontNameMap() => throw new NotSupportedException();
        public IReadOnlyDictionary<string, byte[]?> GetEmbeddedFontBytes() => throw new NotSupportedException();
        public IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> GetToUnicodeMaps() => throw new NotSupportedException();
        public IReadOnlyDictionary<string, ImageXObject> GetImageXObjects() => throw new NotSupportedException();
        public IPdfDocument Document => throw new NotSupportedException();
    }
}
