using Unchained.Pdf.Engine;
using Unchained.Pdf.Models;
using Unchained.Pdf.Rendering.Engine;

namespace Unchained.Pdf.Samples;

/// <summary>
///     Console walkthrough of the <c>Unchained.Pdf</c> and <c>Unchained.Pdf.Rendering</c> public
///     APIs. Each demo is self-contained and writes its output into an <c>output/</c> directory
///     next to the executable. Run without arguments for an interactive menu, or pass a demo name
///     (e.g. <c>dotnet run -- tables</c>) to run a single demo, or <c>all</c> to run everything.
/// </summary>
internal static class Program
{
    private static readonly string OutputDir =
        Path.Combine(AppContext.BaseDirectory, "output");

    private static readonly (string Key, string Title, Func<Task> Run)[] Demos =
    [
        ("create", "Create a PDF from Markdown", CreateFromMarkdownAsync),
        ("extract", "Extract text from a PDF", ExtractTextAsync),
        ("tables", "Generate a table PDF", GenerateTableAsync),
        ("merge", "Merge multiple PDFs into one", MergeAsync),
        ("stamp", "Add a watermark stamp", StampAsync),
        ("metadata", "Set document metadata", MetadataAsync),
        ("encrypt", "Encrypt and re-open a PDF", EncryptAsync),
        ("render", "Render the first page to PNG", RenderAsync)
    ];

    private static async Task<int> Main(string[] args)
    {
        Directory.CreateDirectory(OutputDir);
        Console.WriteLine("Unchained.Pdf samples");
        Console.WriteLine($"Output directory: {OutputDir}");
        Console.WriteLine();

        var selection = args.Length > 0 ? args[0].ToLowerInvariant() : Prompt();

        try
        {
            if (selection is "all")
            {
                foreach (var demo in Demos)
                    await RunOneAsync(demo);
            }
            else if (Demos.FirstOrDefault(d => d.Key == selection) is { Run: not null } match)
                await RunOneAsync(match);
            else
            {
                Console.WriteLine($"Unknown demo '{selection}'. Valid: {string.Join(", ", Demos.Select(static d => d.Key))}, all.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Demo failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
        return 0;
    }

    private static string Prompt()
    {
        Console.WriteLine("Available demos:");
        for (var i = 0; i < Demos.Length; i++)
            Console.WriteLine($"  {i + 1}. {Demos[i].Key,-9} — {Demos[i].Title}");
        Console.WriteLine("  a. all");
        Console.Write("Select (number, name, or 'a'): ");

        var input = Console.ReadLine()?.Trim() ?? string.Empty;
        return input is "a" or "all"
            ? "all"
            : int.TryParse(input, out var n) && n >= 1 && n <= Demos.Length
                ? Demos[n - 1].Key
                : input.ToLowerInvariant();
    }

    private static async Task RunOneAsync((string Key, string Title, Func<Task> Run) demo)
    {
        Console.WriteLine($"▶ {demo.Title}");
        await demo.Run();
        Console.WriteLine();
    }

    // ── Demos ─────────────────────────────────────────────────────────────────

    /// <summary>Converts a Markdown string into a PDF (headings, lists, bold/italic, code).</summary>
    private static async Task CreateFromMarkdownAsync()
    {
        const string markdown = """
            # Unchained.Pdf

            A **pure-managed** PDF engine for .NET.

            ## Features

            - Text extraction
            - Table generation
            - Merging, stamping, encryption

            > No native dependencies for core operations.
            """;

        var processor = new DocumentProcessor();
        await using var doc = await processor.LoadFromMarkdownAsync(markdown);
        var path = Path.Combine(OutputDir, "from-markdown.pdf");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote {doc.PageCount}-page PDF → {Rel(path)}");
    }

    /// <summary>Loads a PDF and prints the text of its first page plus a few positioned spans.</summary>
    private static async Task ExtractTextAsync()
    {
        // Reuse the Markdown demo's output as input; generate it if absent.
        var source = Path.Combine(OutputDir, "from-markdown.pdf");
        if (!File.Exists(source)) await CreateFromMarkdownAsync();

        var processor = new DocumentProcessor();
        await using var doc = await processor.LoadAsync(source);

        var text = doc.Pages[1].ExtractText();
        Console.WriteLine($"  Page 1 text ({text.Length} chars):");
        foreach (var line in text.Split('\n').Take(5))
            Console.WriteLine($"    {line}");

        var spans = doc.Pages[1].GetTextSpans();
        Console.WriteLine($"  {spans.Count} positioned text spans on page 1.");
    }

    /// <summary>Builds a data table and renders it to a PDF using the table generator.</summary>
    private static async Task GenerateTableAsync()
    {
        var data = new TableData
        {
            Title = "Quarterly Revenue",
            Headers = ["Quarter", "Region", "Revenue"],
            Rows =
            [
                ["Q1", "North", "$12,400"],
                ["Q2", "North", "$15,100"],
                ["Q1", "South", "$9,800"],
                ["Q2", "South", "$11,300"]
            ]
        };

        var generator = new TableGenerator();
        await using var doc = await generator.GenerateAsync(data, TableStyle.Default);

        var processor = new DocumentProcessor();
        var path = Path.Combine(OutputDir, "table.pdf");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote {data.Rows.Count}-row table → {Rel(path)}");
    }

    /// <summary>Merges two generated PDFs into a single document.</summary>
    private static async Task MergeAsync()
    {
        var processor = new DocumentProcessor();

        await using var first = await processor.LoadFromMarkdownAsync("# Document A\n\nFirst document.");
        await using var second = await processor.LoadFromMarkdownAsync("# Document B\n\nSecond document.");

        var merger = new DocumentMerger();
        await using var merged = await merger.MergeAsync([first, second], MergeOptions.Default);

        var path = Path.Combine(OutputDir, "merged.pdf");
        await processor.SaveAsync(merged, path);
        Console.WriteLine($"  Merged into {merged.PageCount}-page PDF → {Rel(path)}");
    }

    /// <summary>Applies a diagonal "DRAFT" watermark to every page.</summary>
    private static async Task StampAsync()
    {
        var processor = new DocumentProcessor();
        await using var doc = await processor.LoadFromMarkdownAsync("# Confidential\n\nWatermarked content.");

        var applier = new StampApplier();
        await applier.StampAsync(
            doc,
            new TextStamp(
                "DRAFT",
                X: 150,
                Y: 400,
                FontSize: 60f,
                GrayLevel: 0.8f,
                RotationDegrees: 45f,
                IsBackground: true
            )
        );

        var path = Path.Combine(OutputDir, "stamped.pdf");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote watermarked PDF → {Rel(path)}");
    }

    /// <summary>Sets the document's /Info metadata (title, author, subject, keywords).</summary>
    private static async Task MetadataAsync()
    {
        var processor = new DocumentProcessor();
        await using var doc = await processor.LoadFromMarkdownAsync("# Report\n\nWith metadata.");

        await processor.SetMetadataAsync(
            doc,
            new DocumentMetadata(
                Title: "Annual Report 2026",
                Author: "Unchained Samples",
                Subject: "Finance",
                Keywords: "report finance annual",
                Creator: null,
                Producer: null,
                CreationDate: null,
                ModificationDate: null
            )
        );

        var path = Path.Combine(OutputDir, "with-metadata.pdf");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote PDF with /Info metadata → {Rel(path)}");
    }

    /// <summary>Encrypts a PDF with AES-256, then re-opens it with the password.</summary>
    private static async Task EncryptAsync()
    {
        var processor = new DocumentProcessor();
        await using var doc = await processor.LoadFromMarkdownAsync("# Secret\n\nEncrypted content.");

        var path = Path.Combine(OutputDir, "encrypted.pdf");
        await processor.SaveAsync(
            doc,
            path,
            new SaveOptions(
                Encryption: new EncryptionOptions(
                    UserPassword: "open-sesame",
                    OwnerPassword: "owner-key",
                    Algorithm: PdfEncryptionAlgorithm.Aes256
                )
            )
        );
        Console.WriteLine($"  Wrote AES-256 encrypted PDF → {Rel(path)}");

        await using var reopened = await processor.LoadAsync(path, "open-sesame");
        Console.WriteLine($"  Re-opened with password — {reopened.PageCount} page(s) readable.");
    }

    /// <summary>Rasterizes the first page of a PDF to a PNG image at 150 DPI.</summary>
    private static async Task RenderAsync()
    {
        var source = Path.Combine(OutputDir, "table.pdf");
        var processor = new DocumentProcessor();
        if (!File.Exists(source)) await GenerateTableAsync();

        await using var doc = await processor.LoadAsync(source);

        PdfRenderer renderer;
        try
        {
            renderer = new PdfRenderer();
        }
        catch (InvalidOperationException ex)
        {
            // The FreeType2 native library could not be loaded on this machine.
            Console.WriteLine($"  Rendering unavailable: {ex.Message}");
            return;
        }

        using (renderer)
        {
            var png = await renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 150));
            var path = Path.Combine(OutputDir, "page1.png");
            await File.WriteAllBytesAsync(path, png);
            Console.WriteLine($"  Rendered page 1 → {Rel(path)} ({png.Length:N0} bytes)");
        }
    }

    private static string Rel(string path) => Path.GetRelativePath(AppContext.BaseDirectory, path);
}
