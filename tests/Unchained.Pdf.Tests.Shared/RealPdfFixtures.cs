namespace Unchained.Pdf.Tests.Shared;

/// <summary>
///     Locates real-world PDF files dropped into the <c>TestFiles/</c> folder.
///     Tests call <see cref="TryLoad" /> and skip gracefully when a file is absent.
/// </summary>
internal static class RealPdfFixtures
{
    private static readonly string TestFilesDir =
        Path.Combine(AppContext.BaseDirectory, "TestFiles");

    /// <summary>
    ///     Returns the full path to <paramref name="fileName" /> if the file exists in
    ///     <c>TestFiles/</c>, otherwise <see langword="null" />.
    /// </summary>
    private static string? TryGetPath(string fileName)
    {
        var path = Path.Combine(TestFilesDir, fileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    ///     Reads <paramref name="fileName" /> from <c>TestFiles/</c> and returns the bytes,
    ///     or <see langword="null" /> if the file does not exist.
    /// </summary>
    internal static byte[]? TryLoad(string fileName)
    {
        var path = TryGetPath(fileName);
        return path is null ? null : File.ReadAllBytes(path);
    }

    /// <summary>
    ///     Loads <paramref name="fileName" /> from <c>TestFiles/</c> and returns the bytes.
    ///     Throws when the file is absent — the canonical corpus is committed, so a missing
    ///     file is a real error, not a reason to skip.
    /// </summary>
    internal static byte[] Load(string fileName)
    {
        var path = Path.Combine(TestFilesDir, fileName);
        return !File.Exists(path)
            ? throw new FileNotFoundException($"Committed test corpus file missing: TestFiles/{fileName}", path)
            : File.ReadAllBytes(path);
    }

    /// <summary>
    ///     Enumerates all <c>*.pdf</c> files present in <c>TestFiles/</c>, returning their
    ///     full paths. Used by smoke tests to drive a loop over every file.
    ///     Returns an empty sequence when the folder is absent or empty.
    /// </summary>
    public static IEnumerable<object[]> AllPdfFilePaths() =>
        Directory.Exists(TestFilesDir)
            ? Directory
                .GetFiles(TestFilesDir, "*.pdf")
                .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
                .Select(static p => new object[] { p })
            : [];

    /// <summary>Canonical file names expected in the <c>TestFiles/</c> folder.</summary>
    internal static class Files
    {
        public const string Simple = "simple.pdf";
        public const string Multipage = "multipage.pdf";
        public const string TextOnly = "text-only.pdf";
        public const string WithImages = "with-images.pdf";
        public const string WithTables = "with-tables.pdf";
        public const string WithAnnotations = "with-annotations.pdf";
        public const string WithForms = "with-forms.pdf";
        public const string WithBookmarks = "with-bookmarks.pdf";
        public const string WithEmbeddedFonts = "with-embedded-fonts.pdf";
        public const string Scanned = "scanned.pdf";
        public const string Large = "large.pdf";
        public const string Complex = "complex.pdf";
        public const string Encrypted = "encrypted.pdf";

        /// <summary>ImageMagick-generated PDF with ASCII85Decode image filter.</summary>
        public const string ImagemagickAscii85 = "imagemagick-ascii85.pdf";

        /// <summary>ImageMagick-generated PDF with LZWDecode image filter (tests LZW stub).</summary>
        public const string ImagemagickLzw = "imagemagick-lzw.pdf";

        /// <summary>ImageMagick-generated PDF with CCITTFaxDecode (scanned B&amp;W).</summary>
        public const string ImagemagickCcitt = "imagemagick-ccitt.pdf";

        /// <summary>ReportLab PDF containing inline images (BI…ID…EI operators).</summary>
        public const string InlineImage = "inline-image.pdf";

        /// <summary>LibreOffice-generated fillable form (second AcroForm variant).</summary>
        public const string LibreOfficeForm = "libreoffice-form.pdf";

        /// <summary>Arabic / right-to-left text, requires HarfBuzz shaping.</summary>
        public const string Arabic = "arabic.pdf";

        /// <summary>Arabic text in a rotated page.</summary>
        public const string ArabicRotated = "arabic-rotated.pdf";

        /// <summary>PDF with corrupted / unreadable metadata — tests parser robustness.</summary>
        public const string BadMetadata = "bad-metadata.pdf";

        /// <summary>PDF with a JPEG image encoded as an ASCIIHexDecode stream (DCT).</summary>
        public const string Base64Image = "base64-image.pdf";

        /// <summary>PDF with a real XMP metadata packet (tests <c>GetXmpMetadata()</c>).</summary>
        public const string WithXmp = "with-xmp.pdf";

        /// <summary>PDF/A-1b archival document.</summary>
        public const string PdfA = "pdfa.pdf";

        /// <summary>PDF with a CMYK colour-space image.</summary>
        public const string CmykImage = "cmyk-image.pdf";

        /// <summary>PDF with an embedded file attachment.</summary>
        public const string WithAttachment = "with-attachment.pdf";

        /// <summary>PDF with CropBox, rotation, and scale transforms.</summary>
        public const string CroppedRotated = "cropped-rotated.pdf";

        /// <summary>PDF with internally inconsistent XObject references — tests error recovery.</summary>
        public const string WrongReferences = "wrong-references.pdf";
    }
}
