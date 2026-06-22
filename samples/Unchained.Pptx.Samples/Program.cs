using Unchained.Ooxml;
using Unchained.Pptx.Core;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Rendering.Engine;
using Unchained.Pptx.Rendering.Models;

namespace Unchained.Pptx.Samples;

/// <summary>
///     Console walkthrough of the <c>Unchained.Pptx</c> and <c>Unchained.Pptx.Rendering</c> public
///     APIs. Each demo is self-contained and writes its output into an <c>output/</c> directory next
///     to the executable. Run without arguments for an interactive menu, pass a demo name (e.g.
///     <c>dotnet run -- export</c>) to run one, or <c>all</c> to run everything.
/// </summary>
internal static class Program
{
    private static readonly string OutputDir =
        Path.Combine(AppContext.BaseDirectory, "output");

    private static readonly (string Key, string Title, Func<Task> Run)[] Demos =
    [
        ("create", "Create a presentation with text, shapes, and a table", CreateDeckAsync),
        ("read", "Read all text from a presentation", ReadTextAsync),
        ("export", "Export to PDF, SVG, and HTML", ExportAsync),
        ("render", "Render slides to PNG images", RenderAsync),
        ("encrypt", "Encrypt and re-open a presentation", EncryptAsync)
    ];

    private static async Task<int> Main(string[] args)
    {
        Directory.CreateDirectory(OutputDir);
        Console.WriteLine("Unchained.Pptx samples");
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
            Console.WriteLine($"  {i + 1}. {Demos[i].Key,-8} — {Demos[i].Title}");
        Console.WriteLine("  a. all");
        Console.Write("Select (number, name, or 'a'): ");

        var input = Console.ReadLine()?.Trim() ?? string.Empty;
        if (input is "a" or "all") return "all";
        if (int.TryParse(input, out var n) && n >= 1 && n <= Demos.Length)
            return Demos[n - 1].Key;

        return input.ToLowerInvariant();
    }

    private static async Task RunOneAsync((string Key, string Title, Func<Task> Run) demo)
    {
        Console.WriteLine($"▶ {demo.Title}");
        await demo.Run();
        Console.WriteLine();
    }

    // ── Demos ─────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Builds a widescreen presentation: a title slide, a content slide with an auto shape,
    ///     and a slide containing a 2×3 table — then saves it as a .pptx file.
    /// </summary>
    private static async Task CreateDeckAsync()
    {
        var processor = new PresentationProcessor();
        var doc = processor.CreateBlank(SlideSize.Widescreen);
        var layout = doc.Masters[0].Layouts[0];

        // Title slide.
        var title = doc.Slides.AddBlank(layout);
        title.Shapes.AddTextBox(
            Emu.FromInches(1),
            Emu.FromInches(2.5),
            Emu.FromInches(11),
            Emu.FromInches(1.5),
            "Unchained.Pptx"
        );
        title.Shapes.AddTextBox(
            Emu.FromInches(1),
            Emu.FromInches(4),
            Emu.FromInches(11),
            Emu.FromInches(1),
            "MIT-licensed PowerPoint processing for .NET"
        );

        // Content slide with an auto shape.
        var content = doc.Slides.AddBlank(layout);
        content.Shapes.AddTextBox(
            Emu.FromInches(0.5),
            Emu.FromInches(0.4),
            Emu.FromInches(12),
            Emu.FromInches(1),
            "What you can do"
        );
        var box = content.Shapes.AddShape(
            AutoShapeType.RoundedRectangle,
            Emu.FromInches(1),
            Emu.FromInches(2),
            Emu.FromInches(5),
            Emu.FromInches(2)
        );
        box.TextFrame.PlainText = "Read · Write · Export · Render";

        // Table slide.
        var tableSlide = doc.Slides.AddBlank(layout);
        var cols = new[] { Emu.FromInches(4), Emu.FromInches(4) };
        var rows = new[] { Emu.FromInches(0.6), Emu.FromInches(0.6), Emu.FromInches(0.6) };
        var table = tableSlide.Shapes.AddTable(Emu.FromInches(1), Emu.FromInches(1.5), cols, rows);
        table.HasHeaderRow = true;
        string[,] cells =
        {
            { "Feature", "Status" },
            { "PDF export", "Supported" },
            { "Slide render", "Supported" }
        };
        for (var r = 0; r < 3; r++)
        for (var c = 0; c < 2; c++)
            table[c, r].TextFrame.PlainText = cells[r, c];

        var path = Path.Combine(OutputDir, "deck.pptx");
        await processor.SaveAsync(doc, path);
        Console.WriteLine($"  Wrote {doc.Slides.Count}-slide presentation → {Rel(path)}");
    }

    /// <summary>Loads a presentation and prints the text content of each slide.</summary>
    private static async Task ReadTextAsync()
    {
        var source = Path.Combine(OutputDir, "deck.pptx");
        if (!File.Exists(source)) await CreateDeckAsync();

        var processor = new PresentationProcessor();
        await using var doc = await processor.LoadAsync(source);

        var index = 1;
        foreach (var text in doc.Slides.Select(static slide => slide.GetAllText().Replace("\n", " | ")))
            Console.WriteLine($"  Slide {index++}: {text}");
    }

    /// <summary>Exports a presentation to PDF, per-slide SVG, and per-slide HTML.</summary>
    private static async Task ExportAsync()
    {
        var source = Path.Combine(OutputDir, "deck.pptx");
        if (!File.Exists(source)) await CreateDeckAsync();

        var processor = new PresentationProcessor();
        await using var doc = await processor.LoadAsync(source);

        // PDF.
        var pdfPath = Path.Combine(OutputDir, "deck.pdf");
        await processor.SaveAsPdfAsync(doc, pdfPath);
        Console.WriteLine($"  PDF  → {Rel(pdfPath)}");

        // SVG (one byte[] per slide).
        var svgs = await processor.ExportAsSvgAsync(doc);
        for (var i = 0; i < svgs.Length; i++)
        {
            var svgPath = Path.Combine(OutputDir, $"slide{i + 1}.svg");
            await File.WriteAllBytesAsync(svgPath, svgs[i]);
        }

        Console.WriteLine($"  SVG  → {svgs.Length} file(s) (slide1.svg …)");

        // HTML (one file per slide written into a directory).
        var htmlDir = Path.Combine(OutputDir, "html");
        Directory.CreateDirectory(htmlDir);
        var htmlFiles = await processor.SaveAsHtmlAsync(doc, htmlDir);
        Console.WriteLine($"  HTML → {htmlFiles.Count} file(s) in {Rel(htmlDir)}");
    }

    /// <summary>Rasterizes every slide to a PNG at 1280×720.</summary>
    private static async Task RenderAsync()
    {
        var source = Path.Combine(OutputDir, "deck.pptx");
        if (!File.Exists(source)) await CreateDeckAsync();

        var processor = new PresentationProcessor();
        await using var doc = await processor.LoadAsync(source);

        PptxImage[] images;
        try
        {
            images = await SlideRenderer.RenderAllAsync(doc, new RenderOptions(WidthPx: 1280, HeightPx: 720));
        }
        catch (Exception ex)
        {
            // Rendering needs the FreeType2 native library; report rather than crash.
            Console.WriteLine($"  Rendering unavailable: {ex.Message}");
            return;
        }

        for (var i = 0; i < images.Length; i++)
        {
            var path = Path.Combine(OutputDir, $"slide{i + 1}.png");
            await images[i].SaveAsync(path);
        }

        Console.WriteLine($"  Rendered {images.Length} slide(s) → slide1.png … ({images[0].WidthPx}×{images[0].HeightPx})");
    }

    /// <summary>Saves a presentation with AES-256 encryption, then re-opens it with the password.</summary>
    private static async Task EncryptAsync()
    {
        var source = Path.Combine(OutputDir, "deck.pptx");
        if (!File.Exists(source)) await CreateDeckAsync();

        var processor = new PresentationProcessor();
        await using var doc = await processor.LoadAsync(source);

        var path = Path.Combine(OutputDir, "deck-encrypted.pptx");
        await processor.SaveAsync(doc, path, new SaveOptions { Password = "open-sesame" });
        Console.WriteLine($"  Wrote AES-256 encrypted presentation → {Rel(path)}");

        await using var reopened = await processor.LoadAsync(path, new OpenOptions { Password = "open-sesame" });
        Console.WriteLine($"  Re-opened with password — {reopened.Slides.Count} slide(s) readable.");
    }

    private static string Rel(string path) => Path.GetRelativePath(AppContext.BaseDirectory, path);
}
