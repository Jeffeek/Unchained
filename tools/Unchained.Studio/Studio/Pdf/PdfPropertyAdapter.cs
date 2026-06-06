using MudBlazor;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Unchained.Studio.Models;

namespace Unchained.Studio.Studio.Pdf;

public static class PdfPropertyAdapter
{
    public static PropertyBag Build(TreeNode node) =>
        node.NodeType switch
        {
            TreeNodeType.Document when node.Payload is IPdfDocument doc => ForDocument(doc),
            TreeNodeType.Metadata when node.Payload is IPdfDocument doc => ForMetadata(doc),
            TreeNodeType.Page when node.Payload is IPdfPage page => ForPage(page),
            TreeNodeType.Font when node.Payload is (string key, string name) => ForFont(key, name),
            TreeNodeType.Image when node.Payload is (string key, ImageXObject img) => ForImage(key, img),
            TreeNodeType.Annotation when node.Payload is Annotation ann => ForAnnotation(ann),
            TreeNodeType.Bookmark when node.Payload is Bookmark bm => ForBookmark(bm),
            TreeNodeType.FormField when node.Payload is FormField field => ForFormField(field),
            TreeNodeType.NamedDestination when node.Payload is NamedDestination dest => ForNamedDest(dest),
            TreeNodeType.ContentStream when node.Payload is IPdfPage page2 => ForContentStream(page2),
            TreeNodeType.Operator when node.Payload is ContentOperator op => ForContentOperator(op),
            TreeNodeType.XmpMetadata when node.Payload is string xmp => ForXmp(xmp),
            TreeNodeType.Encryption when node.Payload is IPdfDocument encDoc => ForEncryption(encDoc),
            _ => PropertyBag.Empty(node.Label)
        };

    private static PropertyBag ForDocument(IPdfDocument doc) => new()
    {
        Title = "Document",
        Groups =
        [
            new PropertyGroup
            {
                Header = "Structure",
                Entries =
                [
                    Entry("Pages", doc.PageCount.ToString(), PropertyValueKind.Number),
                    Entry("Version", "PDF 1.x", PropertyValueKind.Text),
                    Entry("Linearized", doc.IsLinearized.ToString(), doc.IsLinearized ? PropertyValueKind.Boolean : PropertyValueKind.Text),
                    Entry("Tagged", doc.IsTagged.ToString(), PropertyValueKind.Boolean),
                    Entry("Encrypted", doc.IsEncrypted.ToString(), PropertyValueKind.Boolean),
                ]
            },
            new PropertyGroup
            {
                Header = "Compliance",
                Entries =
                [
                    Entry("PDF/A", doc.IsPdfaCompliant.ToString(), PropertyValueKind.Boolean),
                    Entry("PDF/UA", doc.IsPdfUaCompliant.ToString(), PropertyValueKind.Boolean),
                ]
            },
            new PropertyGroup
            {
                Header = "Document ID",
                Entries = doc.Id is { } id
                    ?
                    [
                        Entry("First", id.First, PropertyValueKind.Hex),
                        Entry("Second", id.Second, PropertyValueKind.Hex),
                    ]
                    : [Entry("ID", "(absent)", PropertyValueKind.Text)]
            }
        ]
    };

    private static PropertyBag ForMetadata(IPdfDocument doc)
    {
        var m = doc.Metadata;
        return new PropertyBag
        {
            Title = "Document Info (/Info)",
            Groups =
            [
                new PropertyGroup
                {
                    Entries =
                    [
                        Entry("Title", m.Title ?? "(absent)", PropertyValueKind.Text, m.Title),
                        Entry("Author", m.Author ?? "(absent)", PropertyValueKind.Text, m.Author),
                        Entry("Subject", m.Subject ?? "(absent)", PropertyValueKind.Text, m.Subject),
                        Entry("Keywords", m.Keywords ?? "(absent)", PropertyValueKind.Text, m.Keywords),
                        Entry("Creator", m.Creator ?? "(absent)", PropertyValueKind.Text),
                        Entry("Producer", m.Producer ?? "(absent)", PropertyValueKind.Text),
                        Entry("Created", m.CreationDate?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(absent)", PropertyValueKind.Date),
                        Entry("Modified", m.ModificationDate?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "(absent)", PropertyValueKind.Date),
                    ]
                }
            ]
        };
    }

    private static PropertyBag ForPage(IPdfPage page) => new()
    {
        Title = $"Page {page.PageNumber}",
        Subtitle = $"{page.Width:F2} × {page.Height:F2} pt  ({page.Width / 72 * 25.4:F1} × {page.Height / 72 * 25.4:F1} mm)",
        Groups =
        [
            new PropertyGroup
            {
                Header = "Dimensions",
                Entries =
                [
                    Entry("Width", $"{page.Width:F2} pt  ({page.Width / 72 * 25.4:F1} mm)", PropertyValueKind.Number),
                    Entry("Height", $"{page.Height:F2} pt  ({page.Height / 72 * 25.4:F1} mm)", PropertyValueKind.Number),
                    Entry("Orientation", page.IsLandscape ? "Landscape" : "Portrait", PropertyValueKind.Text),
                ]
            },
            new PropertyGroup
            {
                Header = "Content",
                Entries =
                [
                    Entry("Fonts", page.GetFontNameMap().Count.ToString(), PropertyValueKind.Number),
                    Entry("Images", page.GetImageXObjects().Count.ToString(), PropertyValueKind.Number),
                    Entry("Annotations", page.GetAnnotations().Count.ToString(), PropertyValueKind.Number),
                ]
            }
        ]
    };

    private static PropertyBag ForFont(string key, string name) => new()
    {
        Title = $"Font  {key}",
        Subtitle = name,
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Resource key", key, PropertyValueKind.Text),
                    Entry("Base font", name, PropertyValueKind.Text, name),
                ]
            }
        ]
    };

    private static PropertyBag ForImage(string key, ImageXObject img) => new()
    {
        Title = $"Image  {key}",
        Subtitle = $"{img.Width} × {img.Height} px",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Resource key", key, PropertyValueKind.Text),
                    Entry("Width", $"{img.Width} px", PropertyValueKind.Number),
                    Entry("Height", $"{img.Height} px", PropertyValueKind.Number),
                    Entry("RGB data size", $"{img.RgbData.Length:N0} bytes", PropertyValueKind.Number),
                ]
            }
        ]
    };

    private static PropertyBag ForAnnotation(Annotation ann) => new()
    {
        Title = $"Annotation: {ann.Subtype}",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Subtype", ann.Subtype.ToString(), PropertyValueKind.Text),
                    Entry("Contents", ann.Contents ?? "(none)", PropertyValueKind.Text, ann.Contents),
                    Entry("Rect", $"x={ann.X:F1} y={ann.Y:F1} w={ann.Width:F1} h={ann.Height:F1}", PropertyValueKind.Text),
                ]
            }
        ]
    };

    private static PropertyBag ForBookmark(Bookmark bm) => new()
    {
        Title = "Bookmark",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Title", bm.Title, PropertyValueKind.Text, bm.Title),
                    Entry("Target page", bm.PageNumber.ToString(), PropertyValueKind.Number),
                    Entry("Children", (bm.Children?.Count ?? 0).ToString(), PropertyValueKind.Number),
                ]
            }
        ]
    };

    private static PropertyBag ForFormField(FormField field) => new()
    {
        Title = $"Form field: {field.Name}",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Name", field.Name, PropertyValueKind.Text),
                    Entry("Type", field.FieldType, PropertyValueKind.Text),
                    Entry("Value", field.Value ?? "(empty)", PropertyValueKind.Text, field.Value),
                ]
            }
        ]
    };

    private static PropertyBag ForNamedDest(NamedDestination dest) => new()
    {
        Title = $"Named destination: {dest.Name}",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Name", dest.Name, PropertyValueKind.Text),
                    Entry("Target page", dest.PageNumber.ToString(), PropertyValueKind.Number),
                ]
            }
        ]
    };

    private static PropertyBag ForContentStream(IPdfPage page)
    {
        var operators = page.GetContentOperators();
        return new PropertyBag
        {
            Title = $"Content stream  —  Page {page.PageNumber}",
            Subtitle = $"{operators.Count} operators",
            Groups =
            [
                new PropertyGroup
                {
                    Entries =
                    [
                        Entry("Operator count", operators.Count.ToString(), PropertyValueKind.Number),
                        Entry("Page", page.PageNumber.ToString(), PropertyValueKind.Number),
                    ]
                }
            ]
        };
    }

    private static PropertyBag ForContentOperator(ContentOperator op) => new()
    {
        Title = $"Operator:  {op.Name}",
        Subtitle = DescribeOperator(op.Name),
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Keyword", op.Name, PropertyValueKind.Text),
                    .. op.Operands.Select(static (o, i) =>
                        Entry($"Operand {i + 1}", o?.ToString() ?? "null", PropertyValueKind.Text))
                ]
            }
        ]
    };

    private static PropertyBag ForXmp(string xmp) => new()
    {
        Title = "XMP Metadata",
        Subtitle = $"{xmp.Length:N0} bytes",
        Groups = [],
        RawText = xmp,
        RawTextLabel = "Raw XMP XML"
    };

    private static PropertyBag ForEncryption(IPdfDocument doc) => new()
    {
        Title = "Encryption",
        Groups =
        [
            new PropertyGroup
            {
                Entries =
                [
                    Entry("Algorithm", doc.CryptoAlgorithm?.ToString() ?? "(unknown)", PropertyValueKind.Text),
                    Entry("Permissions", doc.Permissions.ToString(), PropertyValueKind.Text),
                ]
            }
        ]
    };

    private static PropertyEntry Entry(
        string key,
        string value,
        PropertyValueKind kind,
        string? copyValue = null) =>
        new()
        {
            Key = key,
            DisplayValue = value,
            Kind = kind,
            CopyValue = copyValue ?? value
        };

    private static string DescribeOperator(string op) => op switch
    {
        "BT" => "Begin text object",
        "ET" => "End text object",
        "Tf" => "Set font and size",
        "Td" => "Move text position",
        "TD" => "Move text position and set leading",
        "Tm" => "Set text matrix",
        "Tj" => "Show text string",
        "TJ" => "Show text array (with kerning)",
        "\"" => "Move and show text string",
        "'" => "Move to next line and show text",
        "cm" => "Concatenate matrix to CTM",
        "q" => "Save graphics state",
        "Q" => "Restore graphics state",
        "re" => "Rectangle path",
        "m" => "Move to",
        "l" => "Line to",
        "c" => "Cubic Bézier curve",
        "v" => "Bézier curve (first control = current)",
        "y" => "Bézier curve (last control = final)",
        "h" => "Close path",
        "S" => "Stroke path",
        "s" => "Close and stroke path",
        "f" => "Fill path (non-zero winding)",
        "F" => "Fill path (non-zero winding, alias for f)",
        "f*" => "Fill path (even-odd rule)",
        "B" => "Fill and stroke path",
        "b" => "Close, fill and stroke path",
        "n" => "End path (no fill/stroke)",
        "W" => "Set clipping path (non-zero winding)",
        "W*" => "Set clipping path (even-odd rule)",
        "Do" => "Invoke named XObject",
        "RG" => "Set stroke color (DeviceRGB)",
        "rg" => "Set fill color (DeviceRGB)",
        "G" => "Set stroke color (DeviceGray)",
        "g" => "Set fill color (DeviceGray)",
        "K" => "Set stroke color (DeviceCMYK)",
        "k" => "Set fill color (DeviceCMYK)",
        "cs" => "Set color space (fill)",
        "CS" => "Set color space (stroke)",
        "sc" => "Set color (fill)",
        "SC" => "Set color (stroke)",
        "scn" => "Set color with pattern/ICC (fill)",
        "SCN" => "Set color with pattern/ICC (stroke)",
        "gs" => "Set graphics state parameters",
        "w" => "Set line width",
        "j" => "Set line join style",
        "J" => "Set line cap style",
        "M" => "Set miter limit",
        "d" => "Set dash pattern",
        "ri" => "Set color rendering intent",
        "i" => "Set flatness tolerance",
        "BDC" => "Begin marked-content sequence with properties",
        "BMC" => "Begin marked-content sequence",
        "EMC" => "End marked-content sequence",
        "MP" => "Define marked-content point",
        "DP" => "Define marked-content point with properties",
        "Tc" => "Set character spacing",
        "Tw" => "Set word spacing",
        "Tz" => "Set horizontal text scaling",
        "TL" => "Set text leading",
        "Tr" => "Set text rendering mode",
        "Ts" => "Set text rise",
        "BI" => "Begin inline image",
        "ID" => "Begin inline image data",
        "EI" => "End inline image",
        "sh" => "Paint shading pattern",
        _ => string.Empty
    };
}
