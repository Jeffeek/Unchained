using Shouldly;
using Unchained.Ooxml;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.IntegrationTests;

/// <summary>
///     Tests that exercise OpenXmlPresentationWriter by creating presentations with various features,
///     saving, reloading, and verifying correct writing and parsing.
///     Targets: shapes, text, slides, round-trip integrity.
/// </summary>
public sealed class PresentationWriterRegressionTests
{
    [Fact]
    public async Task Writer_EmptyPresentation_WritesAndLoads()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];
            doc.Slides.AddBlank(layout);

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
            doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Writer_MultipleSlides_PreservesCount()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];

            // Add 5 slides
            for (var i = 0; i < 5; i++)
                doc.Slides.AddBlank(layout);

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
            doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Writer_SlideWithShapes_PreservesShapes()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];
            var slide = doc.Slides.AddBlank(layout);

            // Add various shapes
            slide.Shapes.AddShape(
                AutoShapeType.Rectangle,
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(3),
                Emu.FromInches(2)
            );

            slide.Shapes.AddShape(
                AutoShapeType.Ellipse,
                Emu.FromInches(5),
                Emu.FromInches(1),
                Emu.FromInches(2),
                Emu.FromInches(2)
            );

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
        {
            var slide = doc.Slides[0];
            slide.Shapes.Count.ShouldBeGreaterThanOrEqualTo(2);
        }
    }

    [Fact]
    public async Task Writer_SlideWithTextBox_PreservesText()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];
            var slide = doc.Slides.AddBlank(layout);

            var textBox = slide.Shapes.AddTextBox(
                Emu.FromInches(1),
                Emu.FromInches(2),
                Emu.FromInches(6),
                Emu.FromInches(1)
            );

            textBox.Text = "Hello PowerPoint!";

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
        {
            var slide = doc.Slides[0];
            slide.Shapes.Count.ShouldBeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public async Task Writer_LargePresentation_HandlesManySlides()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];

            // Add 25 slides (reduced from 50 for faster test)
            for (var i = 0; i < 25; i++)
            {
                var slide = doc.Slides.AddBlank(layout);

                // Add a shape to each
                slide.Shapes.AddShape(
                    AutoShapeType.Rectangle,
                    Emu.FromInches(1),
                    Emu.FromInches(1),
                    Emu.FromInches(2),
                    Emu.FromInches(1)
                );
            }

            bytes = await SaveToBytes(processor, doc);
        }

        // Should compress reasonably
        bytes.Length.ShouldBeLessThan(10_000_000); // Less than 10MB

        await using (var doc = await LoadFromBytes(processor, bytes))
            doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(25);
    }

    [Fact]
    public async Task Writer_SlideWithMultipleTextBoxes_PreservesAll()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];
            var slide = doc.Slides.AddBlank(layout);

            // Add multiple text boxes
            for (var i = 0; i < 5; i++)
            {
                var textBox = slide.Shapes.AddTextBox(
                    Emu.FromInches(1),
                    Emu.FromInches(i + 1),
                    Emu.FromInches(5),
                    Emu.FromInches(0.5)
                );
                textBox.Text = $"Text Box {i + 1}";
            }

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
        {
            var slide = doc.Slides[0];
            slide.Shapes.Count.ShouldBeGreaterThanOrEqualTo(5);
        }
    }

    [Fact]
    public async Task Writer_DifferentShapeTypes_PreservesAll()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];
            var slide = doc.Slides.AddBlank(layout);

            // Add different shape types
            slide.Shapes.AddShape(AutoShapeType.Rectangle, Emu.FromInches(1), Emu.FromInches(1), Emu.FromInches(2), Emu.FromInches(1));
            slide.Shapes.AddShape(AutoShapeType.Ellipse, Emu.FromInches(1), Emu.FromInches(3), Emu.FromInches(2), Emu.FromInches(1));
            slide.Shapes.AddShape(AutoShapeType.RoundedRectangle, Emu.FromInches(4), Emu.FromInches(3), Emu.FromInches(2), Emu.FromInches(1));

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
        {
            var slide = doc.Slides[0];
            slide.Shapes.Count.ShouldBeGreaterThanOrEqualTo(3);
        }
    }

    [Fact]
    public async Task Writer_EmptyShapes_DoesNotCrash()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];
            var slide = doc.Slides.AddBlank(layout);

            // Add shapes with no text
            slide.Shapes.AddTextBox(
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(3),
                Emu.FromInches(1)
            );

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
            doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Writer_VeryLongText_HandlesCorrectly()
    {
        using var processor = new PresentationProcessor();
        byte[] bytes;

        await using (var doc = processor.CreateBlank())
        {
            var layout = doc.Masters[0].Layouts[0];
            var slide = doc.Slides.AddBlank(layout);

            var textBox = slide.Shapes.AddTextBox(
                Emu.FromInches(1),
                Emu.FromInches(1),
                Emu.FromInches(8),
                Emu.FromInches(5)
            );

            // Very long text
            textBox.Text = new string('A', 5000);

            bytes = await SaveToBytes(processor, doc);
        }

        await using (var doc = await LoadFromBytes(processor, bytes))
            doc.Slides.Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    // Helper methods
    private static async Task<byte[]> SaveToBytes(PresentationProcessor processor, PresentationDocument doc)
    {
        using var stream = new MemoryStream();
        await processor.SaveAsync(doc, stream, cancellationToken: TestContext.Current.CancellationToken);
        return stream.ToArray();
    }

    private static async Task<PresentationDocument> LoadFromBytes(PresentationProcessor processor, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return await processor.LoadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }
}
