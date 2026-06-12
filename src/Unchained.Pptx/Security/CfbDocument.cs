using System.Buffers.Binary;
using System.Text;
using Unchained.Pptx.Core;

namespace Unchained.Pptx.Security;

/// <summary>
/// Minimal OLE Compound File Binary (CFB) reader/writer, tailored for the
/// OOXML encryption use-case where the file always contains exactly two streams:
/// <c>EncryptionInfo</c> and <c>EncryptedPackage</c>.
/// Implements the 512-byte-sector (version 3) CFB layout per [MS-CFB].
/// </summary>
internal static class CfbDocument
{
    // CFB magic bytes (8)
    private static readonly byte[] Magic =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private const int SectorSize = 512;
    private const int MiniSectorSize = 64;
    private const int MiniStreamCutoff = 4096;
    private const int DirEntrySize = 128;
    private const int DirsPerSector = SectorSize / DirEntrySize;   // 4
    private const int FatEntriesPerSector = SectorSize / 4;        // 128

    // Special sector/stream values (stored as signed int; these are unsigned sentinel values)
    private const int FreeSect = unchecked((int)0xFFFFFFFF);   // -1
    private const int EndOfChain = unchecked((int)0xFFFFFFFE); // -2
    private const int FatSect = unchecked((int)0xFFFFFFFD);    // -3
    private const int NoStream = unchecked((int)0xFFFFFFFF);   // -1 (same as FreeSect)

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a CFB byte array and returns a dictionary of stream names → bytes.
    /// Only streams in the root storage are returned.
    /// </summary>
    public static Dictionary<string, byte[]> Read(byte[] data)
    {
        if (data.Length < SectorSize) ThrowInvalid("File too small");
        for (var i = 0; i < Magic.Length; i++)
            if (data[i] != Magic[i]) ThrowInvalid("Missing CFB magic bytes");

        var sectorSize = 1 << BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(30));
        var miniSectorSize = 1 << BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(32));
        var numFatSectors = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(44));
        var firstDirSector = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(48));
        var miniStreamCutoff = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(56));
        var firstMiniFatSector = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(60));

        // Build FAT from DIFAT
        var fat = BuildFat(data, numFatSectors, sectorSize);

        // Build mini-FAT
        var miniFat = BuildMiniFat(data, firstMiniFatSector, fat, sectorSize);

        // Read directory
        var dirEntries = ReadDirectory(data, firstDirSector, fat, sectorSize);

        if (dirEntries.Count == 0) ThrowInvalid("Empty directory");
        var rootEntry = dirEntries[0];

        // Read mini-stream container (from root entry)
        byte[]? miniStream = null;
        if (rootEntry.Size > 0 && rootEntry.StartSector != (int)EndOfChain)
        {
            var chain = FollowChain(fat, rootEntry.StartSector);
            miniStream = ReadSectorChain(data, chain, sectorSize, (int)rootEntry.Size);
        }

        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        // Extract each non-root stream
        for (var i = 1; i < dirEntries.Count; i++)
        {
            var entry = dirEntries[i];
            if (entry.ObjectType != 2 || string.IsNullOrEmpty(entry.Name)) continue;

            byte[] streamBytes;
            if ((long)entry.Size < miniStreamCutoff && miniStream != null)
            {
                // Small stream in mini-stream
                var chain = FollowChain(miniFat, entry.StartSector);
                streamBytes = ReadMiniStreamChain(miniStream, chain, miniSectorSize, (int)entry.Size);
            }
            else
            {
                // Large stream in full sectors
                var chain = FollowChain(fat, entry.StartSector);
                streamBytes = ReadSectorChain(data, chain, sectorSize, (int)entry.Size);
            }

            result[entry.Name] = streamBytes;
        }

        return result;
    }

    private static int[] BuildFat(byte[] data, int numFatSectors, int sectorSize)
    {
        var fatSectors = new List<int>();

        // First 109 FAT sectors from DIFAT in header
        for (var i = 0; i < 109 && i < numFatSectors; i++)
        {
            var s = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(76 + i * 4));
            if (s == FreeSect || s == EndOfChain || s < 0) break;
            fatSectors.Add(s);
        }

        var allFatEntries = new List<int>();
        foreach (var sector in fatSectors)
        {
            var offset = SectorOffset(sector, sectorSize);
            for (var i = 0; i < sectorSize / 4; i++)
                allFatEntries.Add(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + i * 4)));
        }
        return [.. allFatEntries];
    }

    private static int[] BuildMiniFat(byte[] data, int firstMiniFatSector, int[] fat, int sectorSize)
    {
        if (firstMiniFatSector == FreeSect || firstMiniFatSector == EndOfChain || firstMiniFatSector < 0) return [];

        var entries = new List<int>();
        var chain = FollowChain(fat, firstMiniFatSector);
        foreach (var sector in chain)
        {
            var offset = SectorOffset(sector, sectorSize);
            for (var i = 0; i < sectorSize / 4; i++)
                entries.Add(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + i * 4)));
        }
        return [.. entries];
    }

    private static List<DirEntry> ReadDirectory(byte[] data, int firstDirSector, int[] fat, int sectorSize)
    {
        var entries = new List<DirEntry>();
        var chain = FollowChain(fat, firstDirSector);
        foreach (var sector in chain)
        {
            var offset = SectorOffset(sector, sectorSize);
            for (var i = 0; i < DirsPerSector; i++)
            {
                var e = ParseDirEntry(data, offset + i * DirEntrySize);
                entries.Add(e);
            }
        }
        return entries;
    }

    private static DirEntry ParseDirEntry(byte[] data, int offset)
    {
        var nameLen = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 64));
        var name = nameLen > 2
            ? Encoding.Unicode.GetString(data, offset, nameLen - 2).TrimEnd('\0')
            : string.Empty;
        return new DirEntry
        {
            Name = name,
            ObjectType = data[offset + 66],
            StartSector = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 116)),
            Size = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset + 120)),
        };
    }

    private static List<int> FollowChain(int[] fat, int start)
    {
        var chain = new List<int>();
        var current = start;
        while (current >= 0 && current < fat.Length)
        {
            chain.Add(current);
            current = fat[current];
            if (chain.Count > fat.Length) break; // cycle guard
        }
        return chain;
    }

    private static byte[] ReadSectorChain(byte[] data, List<int> chain, int sectorSize, int size)
    {
        var result = new byte[size];
        var written = 0;
        foreach (var sector in chain)
        {
            var offset = SectorOffset(sector, sectorSize);
            var toCopy = Math.Min(sectorSize, size - written);
            Array.Copy(data, offset, result, written, toCopy);
            written += toCopy;
            if (written >= size) break;
        }
        return result;
    }

    private static byte[] ReadMiniStreamChain(byte[] miniStream, List<int> chain, int miniSectorSize, int size)
    {
        var result = new byte[size];
        var written = 0;
        foreach (var miniSector in chain)
        {
            var offset = miniSector * miniSectorSize;
            var toCopy = Math.Min(miniSectorSize, size - written);
            Array.Copy(miniStream, offset, result, written, toCopy);
            written += toCopy;
            if (written >= size) break;
        }
        return result;
    }

    private static int SectorOffset(int sector, int sectorSize) => SectorSize + sector * sectorSize;

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a CFB byte array containing the specified named streams.
    /// Streams below <see cref="MiniStreamCutoff"/> bytes go into the mini-stream;
    /// all others use full sectors.
    /// </summary>
    public static byte[] Write(IReadOnlyList<(string name, byte[] data)> streams)
    {
        // Separate mini-stream and large streams
        var miniEntries = new List<(string name, byte[] data)>();
        var largeEntries = new List<(string name, byte[] data)>();
        foreach (var (name, data) in streams)
        {
            if (data.Length < MiniStreamCutoff) miniEntries.Add((name, data));
            else largeEntries.Add((name, data));
        }

        // Calculate mini-stream layout
        var miniSectors = new List<(int startMiniSector, int size)>();
        var totalMiniBytes = 0;
        foreach (var (_, data) in miniEntries)
        {
            var start = totalMiniBytes / MiniSectorSize;
            miniSectors.Add((start, data.Length));
            totalMiniBytes += Pad(data.Length, MiniSectorSize);
        }

        // Mini-stream container: how many full sectors it needs
        var miniContainerBytes = Pad(totalMiniBytes, SectorSize);
        var miniContainerSectors = miniContainerBytes / SectorSize;

        // Large stream sector allocations
        var largeSectorStarts = new List<int>();
        var largeSectorCounts = new List<int>();

        // Sector layout:
        // 0: FAT
        // 1: MiniFAT (if any mini streams)
        // 2: Directory
        // 3..3+miniContainerSectors-1: mini-stream container
        // 3+miniContainerSectors...: large streams

        var hasMini = miniEntries.Count > 0;
        var firstDataSector = 3 + (hasMini ? miniContainerSectors : 0);

        var nextSector = firstDataSector;
        foreach (var (_, data) in largeEntries)
        {
            var n = Sectors(data.Length);
            largeSectorStarts.Add(nextSector);
            largeSectorCounts.Add(n);
            nextSector += n;
        }

        var totalSectors = nextSector;
        // FAT must cover all sectors (including itself)
        // Actual FAT sector count needed: ceil(totalSectors / 128)
        // For typical OOXML files totalSectors < 128 so 1 FAT sector suffices
        var fatSectorCount = Math.Max(1, (totalSectors + FatEntriesPerSector - 1) / FatEntriesPerSector);

        // Build the byte array
        var totalFileSize = SectorSize + totalSectors * SectorSize;
        var result = new byte[totalFileSize];

        WriteHeader(result, fatSectorCount, hasMini);
        WriteFat(result, fatSectorCount, miniContainerSectors, largeEntries, largeSectorStarts, largeSectorCounts, hasMini);
        if (hasMini) WriteMiniFat(result, miniEntries);
        WriteDirectory(result, miniEntries, largeEntries, miniSectors, largeSectorStarts,
                       miniContainerSectors, totalMiniBytes, hasMini);
        if (hasMini) WriteMiniStreamContainer(result, miniEntries, miniContainerSectors);
        WriteLargeStreams(result, largeEntries, largeSectorStarts, firstDataSector);

        return result;
    }

    private static void WriteHeader(byte[] buf, int fatSectorCount, bool hasMini)
    {
        Magic.CopyTo(buf, 0);
        // CLSID (16 zeros already)
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(24), 0x003E); // minor ver
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(26), 0x0003); // major ver (v3)
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(28), 0xFFFE); // byte order LE
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(30), 0x0009); // sector size = 2^9 = 512
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(32), 0x0006); // mini-sector = 2^6 = 64
        // reserved: 6 bytes zeros
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(40), 0); // num dir sectors (0 for v3)
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(44), fatSectorCount);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(48), 2); // first dir sector = 2
        // transaction sig: 0
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(56), MiniStreamCutoff);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(60), hasMini ? 1 : (int)EndOfChain); // first miniFAT sector
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(64), hasMini ? 1 : 0); // num miniFAT sectors
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(68), (int)EndOfChain); // no DIFAT chain
        // num DIFAT sectors = 0

        // DIFAT[0]: sector 0 is the FAT sector
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(76), 0);
        // remaining DIFAT entries = FREESECT
        for (var i = 1; i < 109; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(76 + i * 4), FreeSect);
    }

    private static void WriteFat(byte[] buf, int fatSectorCount,
        int miniContainerSectors,
        IReadOnlyList<(string name, byte[] data)> largeEntries,
        IReadOnlyList<int> largeSectorStarts,
        IReadOnlyList<int> largeSectorCounts,
        bool hasMini)
    {
        var fatBase = SectorSize; // sector 0 starts here

        // Fill all entries with FREESECT
        for (var i = 0; i < FatEntriesPerSector; i++)
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(fatBase + i * 4), FreeSect);

        void WriteFatEntry(int sector, int value) =>
            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(fatBase + sector * 4), value);

        // Sector 0: FAT (self)
        WriteFatEntry(0, FatSect);

        if (hasMini)
        {
            // Sector 1: MiniFAT → ENDOFCHAIN
            WriteFatEntry(1, EndOfChain);
        }

        // Sector 2: Directory → ENDOFCHAIN
        WriteFatEntry(2, EndOfChain);

        // Mini-stream container sectors (3..3+miniContainerSectors-1)
        if (hasMini)
        {
            for (var i = 0; i < miniContainerSectors; i++)
            {
                var s = 3 + i;
                WriteFatEntry(s, i == miniContainerSectors - 1 ? EndOfChain : s + 1);
            }
        }

        // Large stream sectors
        for (var li = 0; li < largeEntries.Count; li++)
        {
            var start = largeSectorStarts[li];
            var count = largeSectorCounts[li];
            for (var i = 0; i < count; i++)
            {
                var s = start + i;
                WriteFatEntry(s, i == count - 1 ? EndOfChain : s + 1);
            }
        }
    }

    private static void WriteMiniFat(byte[] buf, IReadOnlyList<(string name, byte[] data)> miniEntries)
    {
        var miniFatOffset = SectorSize * 2; // sector 1
        var miniFatBuf = buf.AsSpan(miniFatOffset, SectorSize);

        // Fill with FREESECT
        for (var i = 0; i < FatEntriesPerSector; i++)
            BinaryPrimitives.WriteInt32LittleEndian(miniFatBuf.Slice(i * 4), FreeSect);

        var miniSectorIdx = 0;
        foreach (var (_, data) in miniEntries)
        {
            var count = Pad(data.Length, MiniSectorSize) / MiniSectorSize;
            for (var i = 0; i < count; i++)
            {
                var isLast = i == count - 1;
                BinaryPrimitives.WriteInt32LittleEndian(
                    miniFatBuf.Slice(miniSectorIdx * 4),
                    isLast ? EndOfChain : miniSectorIdx + 1);
                miniSectorIdx++;
            }
        }
    }

    private static void WriteDirectory(byte[] buf,
        IReadOnlyList<(string name, byte[] data)> miniEntries,
        IReadOnlyList<(string name, byte[] data)> largeEntries,
        IReadOnlyList<(int startMiniSector, int size)> miniSectors,
        IReadOnlyList<int> largeSectorStarts,
        int miniContainerSectors,
        int totalMiniBytes,
        bool hasMini)
    {
        var dirOffset = SectorSize * 3; // sector 2

        // Fill entire directory sector with zeros first
        Array.Clear(buf, dirOffset, SectorSize);

        var all = new List<(string name, byte[] data, bool isMini, int start, int size)>();
        for (var i = 0; i < miniEntries.Count; i++)
        {
            var (name, data) = miniEntries[i];
            all.Add((name, data, isMini: true, miniSectors[i].startMiniSector, data.Length));
        }
        for (var i = 0; i < largeEntries.Count; i++)
        {
            var (name, data) = largeEntries[i];
            all.Add((name, data, isMini: false, largeSectorStarts[i], data.Length));
        }

        // Determine child/sibling structure for root's subtree
        // Build a simple left-skewed binary tree sorted by name (CFB uses Unicode case-insensitive)
        var sorted = all
            .Select((entry, idx) => (entry, idx: idx + 1)) // entry 0 = root
            .OrderBy(x => x.entry.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var leftSibling = new int[all.Count + 1];
        var rightSibling = new int[all.Count + 1];
        for (var i = 0; i <= all.Count; i++) { leftSibling[i] = NoStream; rightSibling[i] = NoStream; }

        // Build simple chain tree: sorted[0] is root of subtree
        // Each node's right sibling = next sorted node
        for (var i = 0; i < sorted.Count - 1; i++)
            rightSibling[sorted[i].idx] = sorted[i + 1].idx;

        // Root entry (entry 0)
        var rootChild = sorted.Count > 0 ? sorted[0].idx : NoStream;
        WriteDirEntry(buf, dirOffset, 0,
            name: "Root Entry",
            objectType: 5, // root storage
            color: 1, // black
            left: NoStream, right: NoStream, child: rootChild,
            startSector: hasMini ? 3 : EndOfChain,
            size: hasMini ? (ulong)Pad(totalMiniBytes, SectorSize) : 0UL);

        // Stream entries
        for (var i = 0; i < all.Count; i++)
        {
            var (name, _, _, start, size) = all[i];
            var entryIdx = i + 1;
            WriteDirEntry(buf, dirOffset, entryIdx,
                name: name,
                objectType: 2, // stream
                color: 1, // black
                left: leftSibling[entryIdx], right: rightSibling[entryIdx],
                child: NoStream,
                startSector: start,
                size: (ulong)size);
        }

        // Mark remaining entries as free (type=0 already from Array.Clear)
    }

    private static void WriteDirEntry(byte[] buf, int dirSectorOffset, int index,
        string name, byte objectType, byte color,
        int left, int right, int child,
        int startSector, ulong size)
    {
        var offset = dirSectorOffset + index * DirEntrySize;

        if (name.Length > 0)
        {
            var nameBytes = Encoding.Unicode.GetBytes(name + '\0');
            var nameLen = Math.Min(nameBytes.Length, 64);
            nameBytes.AsSpan(0, nameLen).CopyTo(buf.AsSpan(offset));
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset + 64), (ushort)nameLen);
        }

        buf[offset + 66] = objectType;
        buf[offset + 67] = color;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(offset + 68), left);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(offset + 72), right);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(offset + 76), child);
        // CLSID (16 bytes, zeros)
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(offset + 116), startSector);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 120), size);
    }

    private static void WriteMiniStreamContainer(byte[] buf,
        IReadOnlyList<(string name, byte[] data)> miniEntries, int miniContainerSectors)
    {
        // Container starts at sector 3
        var containerOffset = SectorSize * 4; // sector 3 (header + 0,1,2 + sector3 = offset 4*512)
        var writePos = 0;

        foreach (var (_, data) in miniEntries)
        {
            var paddedLen = Pad(data.Length, MiniSectorSize);
            data.CopyTo(buf, containerOffset + writePos);
            writePos += paddedLen;
        }
    }

    private static void WriteLargeStreams(byte[] buf,
        IReadOnlyList<(string name, byte[] data)> largeEntries,
        IReadOnlyList<int> largeSectorStarts,
        int firstDataSector)
    {
        for (var i = 0; i < largeEntries.Count; i++)
        {
            var (_, data) = largeEntries[i];
            var startSector = largeSectorStarts[i];
            var destOffset = SectorSize + startSector * SectorSize;
            data.CopyTo(buf, destOffset);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int Pad(int value, int boundary) =>
        value == 0 ? 0 : (value + boundary - 1) / boundary * boundary;

    private static int Sectors(int byteCount) =>
        (byteCount + SectorSize - 1) / SectorSize;

    private static void ThrowInvalid(string reason) =>
        throw new PptxException($"Invalid OLE Compound File: {reason}.");

    private sealed class DirEntry
    {
        public string Name { get; set; } = string.Empty;
        public byte ObjectType { get; set; }
        public int StartSector { get; set; }
        public ulong Size { get; set; }
    }
}
