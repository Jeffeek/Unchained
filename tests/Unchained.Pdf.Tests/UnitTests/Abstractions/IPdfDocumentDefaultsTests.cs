using Shouldly;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Xunit;

namespace Unchained.Pdf.Tests.UnitTests.Abstractions;

/// <summary>
///     Exercises the default interface implementation on <see cref="IPdfDocument" /> — the
///     <see cref="IPdfDocument.GetLayers" /> default that returns an empty list for documents not
///     produced by Unchained. A minimal stub inherits the default body.
/// </summary>
public sealed class IPdfDocumentDefaultsTests
{
    [Fact]
    public void GetLayers_DefaultsToEmpty()
    {
        IPdfDocument document = new StubDocument();
        document.GetLayers().ShouldBeEmpty();
    }

    private sealed class StubDocument : IPdfDocument
    {
        public int PageCount => 0;
        public IPageCollection Pages => throw new NotSupportedException();
        public DocumentMetadata Metadata => DocumentMetadata.Empty;
        public bool IsEncrypted => false;
        public PdfPermissions Permissions => PdfPermissions.All;
        public PdfEncryptionAlgorithm? CryptoAlgorithm => null;
        public bool IsDisposed => false;
        public bool IsLinearized => false;
        public bool IsTagged => false;
        public bool IsPdfaCompliant => false;
        public bool IsPdfUaCompliant => false;
        public (string First, string Second)? Id => null;
        public PageLayout PageLayout => PageLayout.SinglePage;
        public PageMode PageMode => PageMode.UseNone;

        public IReadOnlyList<Bookmark> GetBookmarks() => throw new NotSupportedException();
        public IReadOnlyList<FormField> GetFormFields() => throw new NotSupportedException();
        public ViewerPreferences GetViewerPreferences() => throw new NotSupportedException();
        public string? GetXmpMetadata() => null;
        public IReadOnlyList<NamedDestination> GetNamedDestinations() => throw new NotSupportedException();
        public ReadOnlyMemory<byte> Bytes => ReadOnlyMemory<byte>.Empty;

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
