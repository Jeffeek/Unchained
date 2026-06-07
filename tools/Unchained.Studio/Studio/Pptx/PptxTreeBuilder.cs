using Unchained.Pptx.Charts;
using Unchained.Pptx.Engine;
using Unchained.Pptx.Shapes;
using Unchained.Pptx.Slides;
using Unchained.Studio.Models;

namespace Unchained.Studio.Studio.Pptx;

/// <summary>
/// Builds the navigable <see cref="TreeNode"/> hierarchy for a loaded
/// <see cref="PresentationDocument"/>: document → properties, slides (with shapes),
/// masters/layouts, themes, and media.
/// </summary>
public static class PptxTreeBuilder
{
    public static TreeNode Build(PresentationDocument document, string fileName) => new()
    {
        Label = fileName,
        Icon = Icons.Slideshow,
        NodeType = TreeNodeType.Document,
        Payload = document,
        IsExpanded = true,
        Children =
        [
            BuildPropertiesNode(document),
            BuildSlidesNode(document),
            BuildMastersNode(document),
            BuildMediaNode(document)
        ]
    };

    private static TreeNode BuildPropertiesNode(PresentationDocument document) => new()
    {
        Label = "Properties",
        Icon = Icons.Info,
        NodeType = TreeNodeType.Metadata,
        Payload = document.Properties
    };

    private static TreeNode BuildSlidesNode(PresentationDocument document)
    {
        var slides = document.Slides;
        var node = new TreeNode
        {
            Label = $"Slides ({slides.Count})",
            Icon = Icons.Collections,
            NodeType = TreeNodeType.Pages,
            Payload = document,
            IsExpanded = true
        };

        for (var i = 0; i < slides.Count; i++)
        {
            var slide = slides[i];
            node.Children.Add(BuildSlideNode(slide, i + 1));
        }

        return node;
    }

    private static TreeNode BuildSlideNode(Slide slide, int number)
    {
        var hiddenSuffix = slide.IsHidden ? " (hidden)" : string.Empty;
        var node = new TreeNode
        {
            Label = $"Slide {number}{hiddenSuffix}",
            Icon = Icons.Slide,
            NodeType = TreeNodeType.Slide,
            Payload = slide
        };

        foreach (var shape in slide.Shapes)
            node.Children.Add(BuildShapeNode(shape));

        return node;
    }

    private static TreeNode BuildShapeNode(Shape shape)
    {
        var node = new TreeNode
        {
            Label = ShapeLabel(shape),
            Icon = ShapeIcon(shape),
            NodeType = TreeNodeType.Shape,
            Payload = shape
        };

        // Groups expose nested children.
        if (shape is GroupShape group)
        {
            foreach (var child in group.Children)
                node.Children.Add(BuildShapeNode(child));
        }

        return node;
    }

    private static TreeNode BuildMastersNode(PresentationDocument document)
    {
        var masters = document.Masters;
        var node = new TreeNode
        {
            Label = $"Masters ({masters.Count})",
            Icon = Icons.Dashboard,
            NodeType = TreeNodeType.Generic,
            Payload = document
        };

        foreach (var master in masters)
        {
            var masterNode = new TreeNode
            {
                Label = string.IsNullOrEmpty(master.Name) ? "Master" : master.Name,
                Icon = Icons.Dashboard,
                NodeType = TreeNodeType.Master,
                Payload = master
            };

            // Theme under each master.
            masterNode.Children.Add(new TreeNode
            {
                Label = string.IsNullOrEmpty(master.Theme.Name) ? "Theme" : master.Theme.Name,
                Icon = Icons.Palette,
                NodeType = TreeNodeType.Theme,
                Payload = master.Theme
            });

            // Layouts under each master.
            foreach (var layout in master.Layouts)
            {
                masterNode.Children.Add(new TreeNode
                {
                    Label = string.IsNullOrEmpty(layout.Name) ? layout.LayoutType.ToString() : layout.Name,
                    Icon = Icons.Layout,
                    NodeType = TreeNodeType.Layout,
                    Payload = layout
                });
            }

            node.Children.Add(masterNode);
        }

        return node;
    }

    private static TreeNode BuildMediaNode(PresentationDocument document)
    {
        var media = document.Media;
        var imageCount = media.Images.Count;
        var node = new TreeNode
        {
            Label = $"Media ({imageCount} image{(imageCount == 1 ? string.Empty : "s")})",
            Icon = Icons.Image,
            NodeType = TreeNodeType.Generic,
            Payload = document
        };

        for (var i = 0; i < media.Images.Count; i++)
        {
            var image = media.Images[i];
            node.Children.Add(new TreeNode
            {
                Label = $"Image {i + 1} ({image.ContentType})",
                Icon = Icons.Image,
                NodeType = TreeNodeType.Image,
                Payload = image
            });
        }

        return node;
    }

    // ── Labels & icons ──────────────────────────────────────────────────────

    private static string ShapeLabel(Shape shape)
    {
        var name = string.IsNullOrEmpty(shape.Name) ? shape.GetType().Name : shape.Name;
        return shape switch
        {
            AutoShape { IsTextBox: true } => $"{name} (text box)",
            AutoShape auto => $"{name} ({auto.ShapeType})",
            PictureShape => $"{name} (picture)",
            TableShape => $"{name} (table)",
            ChartShape chart => $"{name} ({ChartTypeLabel(chart.Chart)})",
            ConnectorShape => $"{name} (connector)",
            GroupShape group => $"{name} (group, {group.Children.Count})",
            VideoShape => $"{name} (video)",
            AudioShape => $"{name} (audio)",
            _ => name
        };
    }

    private static string ChartTypeLabel(ChartModel chart) => $"{chart.Type} chart";

    private static string ShapeIcon(Shape shape) => shape switch
    {
        AutoShape { IsTextBox: true } => Icons.TextBox,
        AutoShape => Icons.Shape,
        PictureShape => Icons.Image,
        TableShape => Icons.Table,
        ChartShape => Icons.Chart,
        ConnectorShape => Icons.Connector,
        GroupShape => Icons.Group,
        VideoShape => Icons.Video,
        AudioShape => Icons.Audio,
        _ => Icons.Shape
    };

    // Local icon constants keep the builder free of MudBlazor markup dependencies.
    private static class Icons
    {
        internal const string Slideshow = MudBlazor.Icons.Material.Outlined.Slideshow;
        internal const string Slide = MudBlazor.Icons.Material.Outlined.Crop169;
        internal const string Collections = MudBlazor.Icons.Material.Outlined.Collections;
        internal const string Info = MudBlazor.Icons.Material.Outlined.Info;
        internal const string Dashboard = MudBlazor.Icons.Material.Outlined.Dashboard;
        internal const string Layout = MudBlazor.Icons.Material.Outlined.ViewQuilt;
        internal const string Palette = MudBlazor.Icons.Material.Outlined.Palette;
        internal const string Image = MudBlazor.Icons.Material.Outlined.Image;
        internal const string Table = MudBlazor.Icons.Material.Outlined.TableChart;
        internal const string Chart = MudBlazor.Icons.Material.Outlined.BarChart;
        internal const string Shape = MudBlazor.Icons.Material.Outlined.Category;
        internal const string TextBox = MudBlazor.Icons.Material.Outlined.TextFields;
        internal const string Connector = MudBlazor.Icons.Material.Outlined.Timeline;
        internal const string Group = MudBlazor.Icons.Material.Outlined.Workspaces;
        internal const string Video = MudBlazor.Icons.Material.Outlined.Movie;
        internal const string Audio = MudBlazor.Icons.Material.Outlined.AudioFile;
    }
}
