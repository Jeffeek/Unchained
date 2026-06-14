using System.IO.Compression;

namespace Unchained.Studio.Studio;

/// <summary>
///     Builds an in-memory ZIP archive from a set of named byte payloads. Shared by the
///     PDF split download, the export-comparison dialog, and the batch-export dialog.
/// </summary>
public static class ZipBuilder
{
    /// <summary>
    ///     Packs <paramref name="files" /> into a ZIP and returns its bytes. Entry names may
    ///     contain forward slashes to create subfolders (e.g. <c>"unchained/page_001.png"</c>).
    /// </summary>
    public static byte[] Build(IReadOnlyList<(string Name, byte[] Bytes)> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in files)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(bytes);
            }
        }

        return ms.ToArray();
    }
}
