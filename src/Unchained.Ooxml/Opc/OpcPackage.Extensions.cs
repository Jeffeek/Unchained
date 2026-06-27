using System.Xml.Linq;
using Unchained.Ooxml.Opc;

namespace Unchained.Ooxml.Opc;

/// <summary>
///     Extension methods for <see cref="OpcPackage" /> that are shared across OOXML formats.
/// </summary>
internal static class OpcPackageExtensions
{
    extension(OpcPackage package)
    {
        /// <summary>
        ///     Ensures a package-level relationship of the given type pointing to <paramref name="target" /> exists,
        ///     creating one with a fresh <c>rIdN</c> identifier if absent.
        /// </summary>
        public void EnsurePackageRelationship(string relType, string target)
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
        public static string GetRelativeUri(string sourceUri, string targetUri)
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

        /// <summary>
        ///     Finds the next free relationship identifier in <paramref name="partUri" /> using the
        ///     given <paramref name="prefix" />, starting from <c>prefix1</c>.
        /// </summary>
        public string NextFreeRelId(string partUri, string prefix)
        {
            var part = package.TryGetPart(partUri);
            var used = new HashSet<string>(part?.Relationships.Select(r => r.Id) ?? [], StringComparer.Ordinal);
            var n = 1;
            string id;
            do
                id = $"{prefix}{n++}";
            while (!used.Add(id));
            return id;
        }
    }
}
