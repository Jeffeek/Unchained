using System.Xml.Linq;
using Unchained.Ooxml.Opc;

namespace Unchained.Ooxml.Opc;

/// <summary>
///     Extension methods for <see cref="OpcPackage" /> that are shared across OOXML formats.
/// </summary>
internal static class OpcPackageExtensions
{
    /// <summary>
    ///     Ensures a package-level relationship of the given type pointing to <paramref name="target" /> exists,
    ///     creating one with a fresh <c>rIdN</c> identifier if absent.
    /// </summary>
    public static void EnsurePackageRelationship(this OpcPackage package, string relType, string target)
    {
        var exists = package.PackageRelationships
            .Any(r => r.RelationshipType.Equals(relType, StringComparison.Ordinal));
        if (exists)
            return;

        var used = new HashSet<string>(package.PackageRelationships.Select(r => r.Id), StringComparer.Ordinal);
        var n = 1;
        string relId;
        do
            relId = $"rId{n++}";
        while (!used.Add(relId));

        package.AddPackageRelationship(relId, relType, target);
    }

    /// <summary>
    ///     Returns a relative URI from <paramref name="sourceUri" /> to <paramref name="targetUri" />.
    ///     Both must be absolute OPC part URIs starting with '/'.
    /// </summary>
    public static string GetRelativeUri(this OpcPackage package, string sourceUri, string targetUri)
    {
        var fromDir = (Path.GetDirectoryName(sourceUri) ?? "/").Replace('\\', '/').TrimEnd('/');
        var fromSegments = fromDir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var targetSegments = targetUri.TrimStart('/').Split('/');

        // Find common prefix length.
        var common = 0;
        while (common < fromSegments.Length && common < targetSegments.Length - 1 &&
               string.Equals(fromSegments[common], targetSegments[common], StringComparison.OrdinalIgnoreCase))
            common++;

        var ups = fromSegments.Length - common;
        return string.Concat(Enumerable.Repeat("../", ups)) + string.Join('/', targetSegments.Skip(common));
    }
}
