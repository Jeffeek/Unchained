using MudBlazor;
using Unchained.Pdf.Abstractions;
using Unchained.Pdf.Models;
using Unchained.Studio.Models;

namespace Unchained.Studio.Studio.Pdf;

public static class PdfTreeBuilder
{
    public static TreeNode Build(IPdfDocument document, string fileName)
    {
        var root = new TreeNode
        {
            Label = fileName,
            Icon = Icons.Material.Outlined.PictureAsPdf,
            NodeType = TreeNodeType.Document,
            Payload = document,
            IsExpanded = true,
            Children =
            [
                BuildMetadataNode(document),
                BuildPagesNode(document),
                BuildBookmarksNode(document),
                BuildFormFieldsNode(document),
                BuildNamedDestinationsNode(document),
                BuildViewerPreferencesNode(document),
                BuildXmpNode(document),
                BuildEncryptionNode(document)
            ]
        };

        // Remove nodes that have no content
        root.Children.RemoveAll(static n => n.Children.Count == 0 && n.Label.EndsWith(" (none)", StringComparison.Ordinal));

        return root;
    }

    private static TreeNode BuildMetadataNode(IPdfDocument document)
    {
        var meta = document.Metadata;
        var entries = new List<TreeNode>();

        if (meta.Title is not null)
            entries.Add(Leaf($"Title: {meta.Title}", Icons.Material.Outlined.Title, TreeNodeType.Generic));
        if (meta.Author is not null)
            entries.Add(Leaf($"Author: {meta.Author}", Icons.Material.Outlined.Person, TreeNodeType.Generic));
        if (meta.Subject is not null)
            entries.Add(Leaf($"Subject: {meta.Subject}", Icons.Material.Outlined.Subject, TreeNodeType.Generic));
        if (meta.Creator is not null)
            entries.Add(Leaf($"Creator: {meta.Creator}", Icons.Material.Outlined.Build, TreeNodeType.Generic));
        if (meta.Producer is not null)
            entries.Add(Leaf($"Producer: {meta.Producer}", Icons.Material.Outlined.Print, TreeNodeType.Generic));
        if (meta.CreationDate is not null)
            entries.Add(Leaf($"Created: {meta.CreationDate:yyyy-MM-dd HH:mm}", Icons.Material.Outlined.CalendarToday, TreeNodeType.Generic));
        if (meta.ModificationDate is not null)
            entries.Add(Leaf($"Modified: {meta.ModificationDate:yyyy-MM-dd HH:mm}", Icons.Material.Outlined.Edit, TreeNodeType.Generic));

        // Document-level flags
        entries.Add(Leaf($"Pages: {document.PageCount}", Icons.Material.Outlined.Numbers, TreeNodeType.Generic));
        entries.Add(Leaf($"Linearized: {document.IsLinearized}", Icons.Material.Outlined.Speed, TreeNodeType.Generic));
        entries.Add(Leaf($"Tagged (PDF/UA): {document.IsTagged}", Icons.Material.Outlined.Accessibility, TreeNodeType.Generic));
        entries.Add(Leaf($"PDF/A: {document.IsPdfaCompliant}", Icons.Material.Outlined.VerifiedUser, TreeNodeType.Generic));
        entries.Add(Leaf($"PDF/UA: {document.IsPdfUaCompliant}", Icons.Material.Outlined.AccessibilityNew, TreeNodeType.Generic));
        if (document.Id is { } id)
            entries.Add(Leaf($"ID: {id.First[..Math.Min(8, id.First.Length)]}…", Icons.Material.Outlined.Fingerprint, TreeNodeType.Generic));

        return new TreeNode
        {
            Label = "Document Info",
            Icon = Icons.Material.Outlined.Info,
            NodeType = TreeNodeType.Metadata,
            Payload = document,
            IsExpanded = true,
            Children = entries
        };
    }

    private static TreeNode BuildPagesNode(IPdfDocument document)
    {
        var pagesNode = new TreeNode
        {
            Label = $"Pages ({document.PageCount})",
            Icon = Icons.Material.Outlined.MenuBook,
            NodeType = TreeNodeType.Pages,
            Payload = document,
            IsExpanded = true
        };

        for (var i = 1; i <= document.PageCount; i++)
        {
            var page = document.Pages[i];
            var pageNode = new TreeNode
            {
                Label = $"Page {i}  ({page.Width:F0} × {page.Height:F0} pt)",
                Icon = page.IsLandscape
                    ? Icons.Material.Outlined.CropLandscape
                    : Icons.Material.Outlined.CropPortrait,
                NodeType = TreeNodeType.Page,
                Payload = page,
                HasLazyChildren = true,
                LoadChildrenAsync = () => BuildPageChildrenAsync(page)
            };
            pagesNode.Children.Add(pageNode);
        }

        return pagesNode;
    }

    private static Task<List<TreeNode>> BuildPageChildrenAsync(IPdfPage page)
    {
        var children = new List<TreeNode>();

        // Fonts
        var fonts = page.GetFontNameMap();
        if (fonts.Count > 0)
        {
            var fontsNode = new TreeNode
            {
                Label = $"Fonts ({fonts.Count})",
                Icon = Icons.Material.Outlined.TextFields,
                NodeType = TreeNodeType.Generic,
                Payload = fonts
            };
            foreach (var (key, name) in fonts)
                fontsNode.Children.Add(Leaf($"{key}: {name}", Icons.Material.Outlined.FontDownload, TreeNodeType.Font, (key, name)));
            children.Add(fontsNode);
        }

        // Images
        var images = page.GetImageXObjects();
        if (images.Count > 0)
        {
            var imagesNode = new TreeNode
            {
                Label = $"Images ({images.Count})",
                Icon = Icons.Material.Outlined.Image,
                NodeType = TreeNodeType.Generic,
                Payload = images
            };
            foreach (var (key, img) in images)
            {
                imagesNode.Children.Add(
                    Leaf(
                        $"{key}: {img.Width}×{img.Height} px",
                        Icons.Material.Outlined.PhotoLibrary,
                        TreeNodeType.Image,
                        (key, img)
                    )
                );
            }

            children.Add(imagesNode);
        }

        // Annotations
        var annotations = page.GetAnnotations();
        if (annotations.Count > 0)
        {
            var annNode = new TreeNode
            {
                Label = $"Annotations ({annotations.Count})",
                Icon = Icons.Material.Outlined.Comment,
                NodeType = TreeNodeType.Generic,
                Payload = annotations
            };
            foreach (var ann in annotations)
            {
                annNode.Children.Add(
                    Leaf(
                        $"{ann.Subtype}: {TruncateLabel(ann.Contents ?? ann.Subtype.ToString(), 40)}",
                        Icons.Material.Outlined.StickyNote2,
                        TreeNodeType.Annotation,
                        ann
                    )
                );
            }

            children.Add(annNode);
        }

        // Content stream operators (lazy; expensive on large pages)
        children.Add(
            new TreeNode
            {
                Label = "Content Stream",
                Icon = Icons.Material.Outlined.Code,
                NodeType = TreeNodeType.ContentStream,
                Payload = page,
                HasLazyChildren = true,
                LoadChildrenAsync = () => BuildOperatorsAsync(page)
            }
        );

        return Task.FromResult(children);
    }

    private static Task<List<TreeNode>> BuildOperatorsAsync(IPdfPage page)
    {
        var operators = page.GetContentOperators();
        var nodes = operators
            .Take(500) // cap at 500 operators to keep the UI responsive
            .Select(static (op, i) => Leaf(
                    $"{i + 1:D4}  {op.Name}  {string.Join(" ", op.Operands.Take(3).Select(static o => o.ToString() ?? "?"))}",
                    Icons.Material.Outlined.Terminal,
                    TreeNodeType.Operator,
                    op
                )
            )
            .ToList();

        if (operators.Count > 500)
            nodes.Add(Leaf($"… {operators.Count - 500} more operators (truncated)", Icons.Material.Outlined.MoreHoriz, TreeNodeType.Generic));

        return Task.FromResult(nodes);
    }

    private static TreeNode BuildBookmarksNode(IPdfDocument document)
    {
        var bookmarks = document.GetBookmarks();
        if (bookmarks.Count == 0)
            return EmptyNode("Bookmarks (none)");

        var node = new TreeNode
        {
            Label = $"Bookmarks ({bookmarks.Count})",
            Icon = Icons.Material.Outlined.Bookmarks,
            NodeType = TreeNodeType.BookmarkGroup,
            Payload = bookmarks
        };
        foreach (var bm in bookmarks)
            node.Children.Add(BuildBookmarkNode(bm));
        return node;
    }

    private static TreeNode BuildBookmarkNode(Bookmark bm)
    {
        var node = new TreeNode
        {
            Label = $"{TruncateLabel(bm.Title, 50)} → p.{bm.PageNumber}",
            Icon = Icons.Material.Outlined.Bookmark,
            NodeType = TreeNodeType.Bookmark,
            Payload = bm
        };
        if (bm.Children is not { Count: > 0 } children) return node;

        foreach (var child in children)
            node.Children.Add(BuildBookmarkNode(child));

        return node;
    }

    private static TreeNode BuildFormFieldsNode(IPdfDocument document)
    {
        var fields = document.GetFormFields();
        if (fields.Count == 0)
            return EmptyNode("Form Fields (none)");

        var node = new TreeNode
        {
            Label = $"Form Fields ({fields.Count})",
            Icon = Icons.Material.Outlined.Assignment,
            NodeType = TreeNodeType.FormFieldGroup,
            Payload = fields
        };
        foreach (var f in fields)
        {
            node.Children.Add(
                Leaf(
                    $"{f.Name} [{f.FieldType}] = {TruncateLabel(f.Value ?? "(empty)", 30)}",
                    Icons.Material.Outlined.TextFields,
                    TreeNodeType.FormField,
                    f
                )
            );
        }

        return node;
    }

    private static TreeNode BuildNamedDestinationsNode(IPdfDocument document)
    {
        var dests = document.GetNamedDestinations();
        if (dests.Count == 0)
            return EmptyNode("Named Destinations (none)");

        var node = new TreeNode
        {
            Label = $"Named Destinations ({dests.Count})",
            Icon = Icons.Material.Outlined.FmdGood,
            NodeType = TreeNodeType.NamedDestinationGroup,
            Payload = dests
        };
        foreach (var d in dests)
        {
            node.Children.Add(
                Leaf(
                    $"{d.Name} → p.{d.PageNumber}",
                    Icons.Material.Outlined.LocationOn,
                    TreeNodeType.NamedDestination,
                    d
                )
            );
        }

        return node;
    }

    private static TreeNode BuildViewerPreferencesNode(IPdfDocument document)
    {
        var vp = document.GetViewerPreferences();
        return new TreeNode
        {
            Label = "Viewer Preferences",
            Icon = Icons.Material.Outlined.Visibility,
            NodeType = TreeNodeType.ViewerPreferences,
            Payload = vp,
            Children =
            [
                Leaf($"PageLayout: {document.PageLayout}", Icons.Material.Outlined.ViewModule, TreeNodeType.Generic),
                Leaf($"PageMode: {document.PageMode}", Icons.Material.Outlined.ViewSidebar, TreeNodeType.Generic),
                Leaf($"HideMenubar: {vp.HideMenubar}", Icons.Material.Outlined.MenuOpen, TreeNodeType.Generic),
                Leaf($"HideToolbar: {vp.HideToolbar}", Icons.Material.Outlined.ViewSidebar, TreeNodeType.Generic),
                Leaf($"FitWindow: {vp.FitWindow}", Icons.Material.Outlined.FitScreen, TreeNodeType.Generic),
                Leaf($"DisplayDocTitle: {vp.DisplayDocTitle}", Icons.Material.Outlined.Title, TreeNodeType.Generic)
            ]
        };
    }

    private static TreeNode BuildXmpNode(IPdfDocument document)
    {
        var xmp = document.GetXmpMetadata();
        return xmp is null
            ? EmptyNode("XMP Metadata (none)")
            : new TreeNode
            {
                Label = "XMP Metadata",
                Icon = Icons.Material.Outlined.Description,
                NodeType = TreeNodeType.XmpMetadata,
                Payload = xmp,
                Children =
                [
                    Leaf($"{xmp.Length:N0} bytes", Icons.Material.Outlined.DataObject, TreeNodeType.Generic)
                ]
            };
    }

    private static TreeNode BuildEncryptionNode(IPdfDocument document) =>
        !document.IsEncrypted
            ? EmptyNode("Encryption (none)")
            : new TreeNode
            {
                Label = "Encryption",
                Icon = Icons.Material.Outlined.Lock,
                NodeType = TreeNodeType.Encryption,
                Payload = document,
                Children =
                [
                    Leaf($"Algorithm: {document.CryptoAlgorithm}", Icons.Material.Outlined.Security, TreeNodeType.Generic),
                    Leaf($"Permissions: {document.Permissions}", Icons.Material.Outlined.AdminPanelSettings, TreeNodeType.Generic)
                ]
            };

    private static TreeNode Leaf(
        string label,
        string icon,
        TreeNodeType type,
        object? payload = null
    ) =>
        new()
        {
            Label = label,
            Icon = icon,
            NodeType = type,
            Payload = payload
        };

    private static TreeNode EmptyNode(string label) =>
        new()
        {
            Label = label,
            Icon = Icons.Material.Outlined.HorizontalRule,
            NodeType = TreeNodeType.Generic
        };

    private static string TruncateLabel(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
