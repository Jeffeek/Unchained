using Unchained.Ooxml;
using Unchained.Pptx.Charts;
using Unchained.Pptx.Media;
using Unchained.Pptx.Models;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Pptx.Themes;
using Unchained.Studio.Models;

namespace Unchained.Studio.Studio.Pptx;

/// <summary>
/// Converts a selected <see cref="TreeNode"/> from a PPTX document tree into a
/// <see cref="PropertyBag"/> for display in the properties panel.
/// </summary>
public static class PptxPropertyAdapter
{
    public static PropertyBag Build(TreeNode node) =>
        node.NodeType switch
        {
            TreeNodeType.Metadata when node.Payload is DocumentProperties props => ForProperties(props),
            TreeNodeType.Slide when node.Payload is Slide slide => ForSlide(slide),
            TreeNodeType.Shape when node.Payload is Shape shape => ForShape(shape),
            TreeNodeType.Master when node.Payload is MasterSlide master => ForMaster(master),
            TreeNodeType.Layout when node.Payload is SlideLayout layout => ForLayout(layout),
            TreeNodeType.Theme when node.Payload is PptxTheme theme => ForTheme(theme),
            TreeNodeType.Image when node.Payload is EmbeddedImage image => ForImage(image),
            _ => PropertyBag.Empty(node.Label)
        };

    private static PropertyBag ForProperties(DocumentProperties props) => new()
    {
        Title = "Document Properties",
        Groups =
        [
            new PropertyGroup
            {
                Header = "Core",
                Entries =
                [
                    Entry("Title", props.Title ?? "(absent)"),
                    Entry("Author", props.Author ?? "(absent)"),
                    Entry("Subject", props.Subject ?? "(absent)"),
                    Entry("Keywords", props.Keywords ?? "(absent)"),
                    Entry("Description", props.Description ?? "(absent)"),
                    Entry("Category", props.Category ?? "(absent)"),
                    Entry("Last modified by", props.LastModifiedBy ?? "(absent)")
                ]
            },
            new PropertyGroup
            {
                Header = "Dates",
                Entries =
                [
                    Entry("Created", props.Created?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(absent)", PropertyValueKind.Date),
                    Entry("Modified", props.Modified?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(absent)", PropertyValueKind.Date),
                    Entry("Last printed", props.LastPrinted?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(absent)", PropertyValueKind.Date)
                ]
            },
            new PropertyGroup
            {
                Header = "Application",
                Entries =
                [
                    Entry("Application", props.ApplicationName ?? "(absent)"),
                    Entry("Company", props.Company ?? "(absent)"),
                    Entry("Manager", props.Manager ?? "(absent)"),
                    Entry("Revision", props.RevisionNumber?.ToString() ?? "(absent)", PropertyValueKind.Number)
                ]
            },
            new PropertyGroup
            {
                Header = "Statistics",
                Entries =
                [
                    Entry("Slides", props.SlideCount.ToString(), PropertyValueKind.Number),
                    Entry("Hidden slides", props.HiddenSlideCount.ToString(), PropertyValueKind.Number),
                    Entry("Notes", props.NoteCount.ToString(), PropertyValueKind.Number)
                ]
            }
        ]
    };

    private static PropertyBag ForSlide(Slide slide)
    {
        var allText = slide.GetAllText();
        return new PropertyBag
        {
            Title = $"Slide {slide.SlideNumber}",
            Subtitle = string.IsNullOrEmpty(slide.Name) ? null : slide.Name,
            Groups =
            [
                new PropertyGroup
                {
                    Entries =
                    [
                        Entry("Slide number", slide.SlideNumber.ToString(), PropertyValueKind.Number),
                        Entry("Slide ID", slide.SlideId.ToString(), PropertyValueKind.Number),
                        Entry("Hidden", slide.IsHidden.ToString(), PropertyValueKind.Boolean),
                        Entry("Shapes", slide.Shapes.Count.ToString(), PropertyValueKind.Number),
                        Entry("Layout", slide.Layout.Name, PropertyValueKind.Text)
                    ]
                }
            ],
            RawText = string.IsNullOrWhiteSpace(allText) ? null : allText,
            RawTextLabel = "Slide Text"
        };
    }

    private static PropertyBag ForShape(Shape shape)
    {
        var groups = new List<PropertyGroup>
        {
            new()
            {
                Header = "Identity",
                Entries =
                [
                    Entry("Type", shape.GetType().Name),
                    Entry("Name", shape.Name),
                    Entry("Shape ID", shape.ShapeId.ToString(), PropertyValueKind.Number),
                    Entry("Alt text", shape.AltText ?? "(none)"),
                    Entry("Hidden", shape.IsHidden.ToString(), PropertyValueKind.Boolean),
                    Entry("Decorative", shape.IsDecorative.ToString(), PropertyValueKind.Boolean)
                ]
            },
            new()
            {
                Header = "Geometry",
                Entries =
                [
                    Entry("X", FormatEmu(shape.X)),
                    Entry("Y", FormatEmu(shape.Y)),
                    Entry("Width", FormatEmu(shape.Width)),
                    Entry("Height", FormatEmu(shape.Height)),
                    Entry("Rotation", $"{shape.RotationDegrees:0.##}°", PropertyValueKind.Number),
                    Entry("Flip H", shape.FlipHorizontal.ToString(), PropertyValueKind.Boolean),
                    Entry("Flip V", shape.FlipVertical.ToString(), PropertyValueKind.Boolean)
                ]
            }
        };

        string? rawText = null;
        var rawLabel = "Text";

        switch (shape)
        {
            case AutoShape auto:
            {
                groups.Insert(1, new PropertyGroup
                {
                    Header = "Auto Shape",
                    Entries =
                    [
                        Entry("Preset", auto.ShapeType.ToString()),
                        Entry("Is text box", auto.IsTextBox.ToString(), PropertyValueKind.Boolean)
                    ]
                });
                var text = auto.TextFrame.PlainText;
                rawText = string.IsNullOrWhiteSpace(text) ? null : text;
            }
            break;

            case PictureShape picture:
            {
                groups.Insert(1, new PropertyGroup
                {
                    Header = "Picture",
                    Entries =
                    [
                        Entry("Has image", (picture.Image is not null).ToString(), PropertyValueKind.Boolean),
                        Entry("Content type", picture.Image?.ContentType ?? "(none)"),
                        Entry("Pixel size", picture.Image is { PixelWidth: > 0 } i ? $"{i.PixelWidth} × {i.PixelHeight}" : "(unknown)")
                    ]
                });
            }
            break;

            case TableShape table:
            {
                groups.Insert(1, new PropertyGroup
                {
                    Header = "Table",
                    Entries =
                    [
                        Entry("Header row", table.HasHeaderRow.ToString(), PropertyValueKind.Boolean),
                        Entry("Banded rows", table.HasBandedRows.ToString(), PropertyValueKind.Boolean),
                        Entry("Total row", table.HasTotalRow.ToString(), PropertyValueKind.Boolean)
                    ]
                });
            }
            break;

            case ChartShape chartShape:
            {
                var chart = chartShape.Chart;
                groups.Insert(1, new PropertyGroup
                {
                    Header = "Chart",
                    Entries =
                    [
                        Entry("Type", chart.Type.ToString()),
                        Entry("Title", chart.HasTitle ? chart.Title : "(none)"),
                        Entry("Series", chart.Data.Series.Count.ToString(), PropertyValueKind.Number),
                        Entry("Categories", chart.Data.Categories.Count.ToString(), PropertyValueKind.Number),
                        Entry("Legend", chart.Legend.IsVisible ? chart.Legend.Position.ToString() : "(hidden)")
                    ]
                });
                rawText = DescribeChart(chart);
                rawLabel = "Chart Data";
            }
            break;

            case GroupShape group:
            {
                groups.Insert(1, new PropertyGroup
                {
                    Header = "Group",
                    Entries = [Entry("Children", group.Children.Count.ToString(), PropertyValueKind.Number)]
                });
            }
            break;
        }

        return new PropertyBag
        {
            Title = string.IsNullOrEmpty(shape.Name) ? shape.GetType().Name : shape.Name,
            Subtitle = shape.GetType().Name,
            Groups = groups,
            RawText = rawText,
            RawTextLabel = rawLabel
        };
    }

    private static PropertyBag ForMaster(MasterSlide master) => new()
    {
        Title = string.IsNullOrEmpty(master.Name) ? "Master" : master.Name,
        Subtitle = "Slide Master",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Name", master.Name),
                    Entry("Layouts", master.Layouts.Count.ToString(), PropertyValueKind.Number),
                    Entry("Shapes", master.Shapes.Count.ToString(), PropertyValueKind.Number),
                    Entry("Theme", master.Theme.Name)
                ]
            }
        ]
    };

    private static PropertyBag ForLayout(SlideLayout layout) => new()
    {
        Title = string.IsNullOrEmpty(layout.Name) ? layout.LayoutType.ToString() : layout.Name,
        Subtitle = "Slide Layout",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Name", layout.Name),
                    Entry("Layout type", layout.LayoutType.ToString()),
                    Entry("Shapes", layout.Shapes.Count.ToString(), PropertyValueKind.Number),
                    Entry("Master", layout.Master.Name)
                ]
            }
        ]
    };

    private static PropertyBag ForTheme(PptxTheme theme) => new()
    {
        Title = string.IsNullOrEmpty(theme.Name) ? "Theme" : theme.Name,
        Subtitle = "Theme",
        Groups =
        [
            new PropertyGroup
            {
                Header = "Fonts",
                Entries =
                [
                    Entry("Major (Latin)", theme.Fonts.MajorFont.LatinFont),
                    Entry("Minor (Latin)", theme.Fonts.MinorFont.LatinFont)
                ]
            },
            new PropertyGroup
            {
                Header = "Colors",
                Entries =
                [
                    Entry("Dark 1", FormatColor(theme.Colors.Dark1), PropertyValueKind.Hex),
                    Entry("Light 1", FormatColor(theme.Colors.Light1), PropertyValueKind.Hex),
                    Entry("Dark 2", FormatColor(theme.Colors.Dark2), PropertyValueKind.Hex),
                    Entry("Light 2", FormatColor(theme.Colors.Light2), PropertyValueKind.Hex),
                    Entry("Accent 1", FormatColor(theme.Colors.Accent1), PropertyValueKind.Hex),
                    Entry("Accent 2", FormatColor(theme.Colors.Accent2), PropertyValueKind.Hex),
                    Entry("Accent 3", FormatColor(theme.Colors.Accent3), PropertyValueKind.Hex),
                    Entry("Accent 4", FormatColor(theme.Colors.Accent4), PropertyValueKind.Hex),
                    Entry("Accent 5", FormatColor(theme.Colors.Accent5), PropertyValueKind.Hex),
                    Entry("Accent 6", FormatColor(theme.Colors.Accent6), PropertyValueKind.Hex)
                ]
            }
        ]
    };

    private static PropertyBag ForImage(EmbeddedImage image) => new()
    {
        Title = "Embedded Image",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Content type", image.ContentType),
                    Entry("Size", $"{image.Data.Length / 1024.0:0.#} KB", PropertyValueKind.Number),
                    Entry("Pixel size", image.PixelWidth > 0 ? $"{image.PixelWidth} × {image.PixelHeight}" : "(unknown)")
                ]
            }
        ]
    };

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string DescribeChart(ChartModel chart)
    {
        var sb = new System.Text.StringBuilder();
        if (chart.Data.Categories.Count > 0)
            sb.AppendLine($"Categories: {string.Join(", ", chart.Data.Categories)}");
        foreach (var series in chart.Data.Series)
            sb.AppendLine($"{series.Name}: {string.Join(", ", series.Values)}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatEmu(Emu emu) => $"{emu.ToInches():0.##} in ({emu.Value} EMU)";

    private static string FormatColor(Unchained.Ooxml.Drawing.ColorSpec color)
    {
        var argb = color.Resolve(null);
        return $"#{argb & 0x00FFFFFF:X6}";
    }

    private static PropertyEntry Entry(
        string key,
        string value,
        PropertyValueKind kind = PropertyValueKind.Text,
        string? copyValue = null) =>
        new()
        {
            Key = key,
            DisplayValue = value,
            Kind = kind,
            CopyValue = copyValue ?? value
        };
}
