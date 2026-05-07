namespace RP2040Sharp.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal FAT12/FAT16 volume reader/writer for use with the USB MSC host probe.
///
/// Provides just enough FAT support to create or overwrite a single file by name
/// in the root directory — sufficient to write <c>code.py</c> to the
/// CircuitPython CIRCUITPY drive via the emulated MSC transport.
///
/// Design notes:
/// <list type="bullet">
///   <item>All sectors are 512 bytes.</item>
///   <item>Supports FAT12 and FAT16 (CircuitPython uses FAT12 on a 2 MB Pico).</item>
///   <item>Does <em>not</em> support subdirectories; only the root directory.</item>
///   <item>Sector I/O is deferred to caller-supplied delegates so the implementation
///         is independent of the USB transport.</item>
/// </list>
/// </summary>
internal sealed class FatVolume
{
    private const int SECTOR_SIZE    = 512;
    private const int DIR_ENTRY_SIZE = 32;
    private const byte ATTR_ARCHIVE  = 0x20;
    private const byte ATTR_DELETED  = 0xE5;
    private const int   FAT12_EOC     = 0xFF8;
    private const ushort FAT16_EOC   = 0xFFF8;

    private readonly Func<uint, byte[]>     _readSector;
    private readonly Action<uint, byte[]>   _writeSector;

    // Populated from the VBR (Volume Boot Record / BPB).
    private int    _bytesPerSector;
    private int    _sectorsPerCluster;
    private int    _reservedSectors;
    private int    _numFats;
    private int    _rootEntryCount;
    private int    _totalSectors16;
    private int    _sectorsPerFat;
    private bool   _isFat12;

    // Derived sector offsets.
    private int _fatStart;
    private int _rootDirStart;
    private int _dataStart;
    private int _dataClusterCount;

    public bool IsValid { get; private set; }

    private FatVolume(Func<uint, byte[]> read, Action<uint, byte[]> write)
    {
        _readSector  = read;
        _writeSector = write;
    }

    /// <summary>
    /// Create a <see cref="FatVolume"/> by reading the VBR from sector 0 via
    /// <paramref name="readSector"/>.  Returns <c>null</c> if the VBR is not valid.
    /// </summary>
    public static FatVolume? Open(Func<uint, byte[]> readSector, Action<uint, byte[]> writeSector)
    {
        var vbr = readSector(0);
        if (vbr.Length < SECTOR_SIZE) return null;

        var v = new FatVolume(readSector, writeSector);
        if (!v.ParseVbr(vbr)) return null;
        return v;
    }

    /// <summary>
    /// Write <paramref name="content"/> bytes to <paramref name="name"/> (8.3 format,
    /// e.g. "CODE    PY ") in the root directory.  Creates the file if it does not exist,
    /// or overwrites the data area if it does (directory entry is updated in-place).
    /// Returns <c>true</c> on success.
    /// </summary>
    public bool WriteFile(string name83, byte[] content)
    {
        // Find or create the directory entry.
        if (!FindOrCreateDirEntry(name83, out var dirSector, out var dirOffset, out var existingFirstCluster))
            return false;

        // Free any existing cluster chain.
        if (existingFirstCluster >= 2) FreeChain((uint)existingFirstCluster);

        // Allocate clusters for the new content.
        uint firstCluster = 0;
        if (content.Length > 0)
        {
            var clusters = AllocateClusters(content, out firstCluster);
            if (clusters == 0) return false;
        }

        // Update the directory entry.
        var dirSectorData = _readSector((uint)dirSector);
        WriteDirectoryEntry(dirSectorData, dirOffset, name83, (uint)content.Length, (ushort)firstCluster);
        _writeSector((uint)dirSector, dirSectorData);
        return true;
    }

    // ── VBR parsing ──────────────────────────────────────────────────────────

    private bool ParseVbr(byte[] vbr)
    {
        _bytesPerSector   = Le16(vbr, 11);
        _sectorsPerCluster = vbr[13];
        _reservedSectors  = Le16(vbr, 14);
        _numFats          = vbr[16];
        _rootEntryCount   = Le16(vbr, 17);
        _totalSectors16   = Le16(vbr, 19);
        _sectorsPerFat    = Le16(vbr, 22);

        if (_bytesPerSector != SECTOR_SIZE || _sectorsPerCluster == 0 ||
            _reservedSectors == 0 || _numFats == 0 || _sectorsPerFat == 0)
            return false;

        _fatStart    = _reservedSectors;
        _rootDirStart = _fatStart + _numFats * _sectorsPerFat;
        var rootDirSectors = (_rootEntryCount * DIR_ENTRY_SIZE + SECTOR_SIZE - 1) / SECTOR_SIZE;
        _dataStart   = _rootDirStart + rootDirSectors;
        var totalSectors = _totalSectors16 != 0 ? _totalSectors16 : Le32(vbr, 32);
        _dataClusterCount = (totalSectors - _dataStart) / _sectorsPerCluster;
        _isFat12 = _dataClusterCount < 4085;
        IsValid = true;
        return true;
    }

    // ── Directory helpers ─────────────────────────────────────────────────────

    private bool FindOrCreateDirEntry(string name83, out int sector, out int offset, out int firstCluster)
    {
        sector = 0; offset = 0; firstCluster = 0;
        var rootSectors = (_rootEntryCount * DIR_ENTRY_SIZE + SECTOR_SIZE - 1) / SECTOR_SIZE;
        int? freeSlotSector = null;
        int freeSlotOffset = 0;

        for (var s = 0; s < rootSectors; s++)
        {
            var sectorData = _readSector((uint)(_rootDirStart + s));
            for (var o = 0; o < SECTOR_SIZE; o += DIR_ENTRY_SIZE)
            {
                var firstByte = sectorData[o];
                if (firstByte == 0x00) goto NotFound; // end of directory
                if (firstByte == ATTR_DELETED)
                {
                    freeSlotSector ??= _rootDirStart + s;
                    freeSlotOffset = o;
                    continue;
                }
                var attr = sectorData[o + 11];
                if ((attr & 0x08) != 0) continue; // volume label
                if ((attr & 0x10) != 0) continue; // subdirectory
                var entryName = System.Text.Encoding.ASCII.GetString(sectorData, o, 11);
                if (string.Equals(entryName, PadName83(name83), StringComparison.OrdinalIgnoreCase))
                {
                    sector       = _rootDirStart + s;
                    offset       = o;
                    firstCluster = Le16(sectorData, o + 26);
                    return true;
                }
            }
        }

        NotFound:
        // Use a free slot or return the free-slot position.
        if (freeSlotSector.HasValue)
        {
            sector = freeSlotSector.Value;
            offset = freeSlotOffset;
            return true;
        }
        // Find the first free entry by scanning for first-byte == 0x00.
        for (var s = 0; s < rootSectors; s++)
        {
            var sectorData = _readSector((uint)(_rootDirStart + s));
            for (var o = 0; o < SECTOR_SIZE; o += DIR_ENTRY_SIZE)
            {
                if (sectorData[o] == 0x00)
                {
                    sector = _rootDirStart + s;
                    offset = o;
                    return true;
                }
            }
        }
        return false;
    }

    private static void WriteDirectoryEntry(byte[] sectorData, int offset, string name83,
        uint fileSize, ushort firstCluster)
    {
        var padded = PadName83(name83);
        System.Text.Encoding.ASCII.GetBytes(padded, 0, 11, sectorData, offset);
        sectorData[offset + 11] = ATTR_ARCHIVE;
        // Time/date: use a fixed stamp (2024-01-01 00:00:00) so tests are deterministic.
        sectorData[offset + 22] = 0x00; // write time
        sectorData[offset + 23] = 0x00;
        sectorData[offset + 24] = 0x21; // write date: 2024-01-01
        sectorData[offset + 25] = 0x58;
        sectorData[offset + 26] = (byte)(firstCluster & 0xFF);
        sectorData[offset + 27] = (byte)(firstCluster >> 8);
        sectorData[offset + 28] = (byte)(fileSize       & 0xFF);
        sectorData[offset + 29] = (byte)((fileSize >>  8) & 0xFF);
        sectorData[offset + 30] = (byte)((fileSize >> 16) & 0xFF);
        sectorData[offset + 31] = (byte)((fileSize >> 24) & 0xFF);
    }

    // ── FAT chain allocation / freeing ────────────────────────────────────────

    private int AllocateClusters(byte[] content, out uint firstCluster)
    {
        firstCluster = 0;
        var bytesPerCluster = _sectorsPerCluster * SECTOR_SIZE;
        var clusterCount = (content.Length + bytesPerCluster - 1) / bytesPerCluster;

        // Find free clusters.
        var freeList = new List<uint>();
        for (uint c = 2; c < _dataClusterCount + 2 && freeList.Count < clusterCount; c++)
        {
            if (GetFatEntry(c) == 0) freeList.Add(c);
        }
        if (freeList.Count < clusterCount) return 0;

        // Link clusters and write data.
        for (var i = 0; i < freeList.Count; i++)
        {
            var cluster = freeList[i];
            var nextVal = i + 1 < freeList.Count ? freeList[i + 1] : (uint)(_isFat12 ? FAT12_EOC : FAT16_EOC);
            SetFatEntry(cluster, (uint)nextVal);

            // Write sector data for this cluster.
            var clusterSectorBase = _dataStart + (int)(cluster - 2) * _sectorsPerCluster;
            for (var s = 0; s < _sectorsPerCluster; s++)
            {
                var byteOffset = (i * _sectorsPerCluster + s) * SECTOR_SIZE;
                var sectorBuf  = new byte[SECTOR_SIZE];
                if (byteOffset < content.Length)
                {
                    var toCopy = Math.Min(SECTOR_SIZE, content.Length - byteOffset);
                    Array.Copy(content, byteOffset, sectorBuf, 0, toCopy);
                }
                _writeSector((uint)(clusterSectorBase + s), sectorBuf);
            }
        }

        firstCluster = freeList[0];
        return freeList.Count;
    }

    private void FreeChain(uint firstCluster)
    {
        var c = firstCluster;
        while (c >= 2 && c < (uint)(_dataClusterCount + 2))
        {
            var next = GetFatEntry(c);
            SetFatEntry(c, 0);
            if (next >= (_isFat12 ? (uint)FAT12_EOC : (uint)FAT16_EOC)) break;
            c = next;
        }
    }

    // ── FAT I/O ───────────────────────────────────────────────────────────────

    private uint GetFatEntry(uint cluster)
    {
        if (_isFat12)
        {
            var byteOffset = cluster + cluster / 2; // 12-bit: 1.5 bytes per entry
            var secIdx     = (int)(byteOffset / SECTOR_SIZE);
            var secOff     = (int)(byteOffset % SECTOR_SIZE);
            var s  = _readSector((uint)(_fatStart + secIdx));
            uint lo = s[secOff];
            uint hi = secOff + 1 < SECTOR_SIZE ? s[secOff + 1]
                      : _readSector((uint)(_fatStart + secIdx + 1))[0];
            var raw = lo | (hi << 8);
            return (cluster & 1) != 0 ? (raw >> 4) & 0xFFF : raw & 0xFFF;
        }
        else
        {
            var byteOffset = cluster * 2;
            var sec        = _readSector((uint)(_fatStart + byteOffset / SECTOR_SIZE));
            return (uint)Le16(sec, (int)(byteOffset % SECTOR_SIZE));
        }
    }

    private void SetFatEntry(uint cluster, uint value)
    {
        // Write to all FAT copies.
        for (var fatIdx = 0; fatIdx < _numFats; fatIdx++)
        {
            var fatBase = _fatStart + fatIdx * _sectorsPerFat;
            if (_isFat12)
            {
                var byteOffset = cluster + cluster / 2;
                var secIdx     = (int)(byteOffset / SECTOR_SIZE);
                var secOff     = (int)(byteOffset % SECTOR_SIZE);
                var s  = _readSector((uint)(fatBase + secIdx));
                if ((cluster & 1) != 0)
                {
                    s[secOff] = (byte)((s[secOff] & 0x0F) | (byte)((value & 0x0F) << 4));
                    if (secOff + 1 < SECTOR_SIZE)
                        s[secOff + 1] = (byte)((value >> 4) & 0xFF);
                    else
                    {
                        _writeSector((uint)(fatBase + secIdx), s);
                        var s2 = _readSector((uint)(fatBase + secIdx + 1));
                        s2[0] = (byte)((value >> 4) & 0xFF);
                        _writeSector((uint)(fatBase + secIdx + 1), s2);
                        continue;
                    }
                }
                else
                {
                    s[secOff] = (byte)(value & 0xFF);
                    if (secOff + 1 < SECTOR_SIZE)
                        s[secOff + 1] = (byte)((s[secOff + 1] & 0xF0) | (byte)((value >> 8) & 0x0F));
                    else
                    {
                        _writeSector((uint)(fatBase + secIdx), s);
                        var s2 = _readSector((uint)(fatBase + secIdx + 1));
                        s2[0] = (byte)((s2[0] & 0xF0) | (byte)((value >> 8) & 0x0F));
                        _writeSector((uint)(fatBase + secIdx + 1), s2);
                        continue;
                    }
                }
                _writeSector((uint)(fatBase + secIdx), s);
            }
            else // FAT16
            {
                var byteOffset = cluster * 2;
                var sec        = _readSector((uint)(fatBase + byteOffset / SECTOR_SIZE));
                var off        = (int)(byteOffset % SECTOR_SIZE);
                sec[off]     = (byte)(value & 0xFF);
                sec[off + 1] = (byte)((value >> 8) & 0xFF);
                _writeSector((uint)(fatBase + byteOffset / SECTOR_SIZE), sec);
            }
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    /// <summary>Convert a short filename ("code.py") to a padded 11-char 8.3 name ("CODE    PY ").</summary>
    internal static string PadName83(string name)
    {
        // If already 11 chars, return as-is.
        if (name.Length == 11) return name.ToUpperInvariant();

        var dot = name.LastIndexOf('.');
        string baseName, ext;
        if (dot < 0)
        {
            baseName = name;
            ext      = "";
        }
        else
        {
            baseName = name[..dot];
            ext      = name[(dot + 1)..];
        }
        var b = baseName.ToUpperInvariant().PadRight(8).Substring(0, 8);
        var e = ext.ToUpperInvariant().PadRight(3).Substring(0, 3);
        return b + e;
    }

    private static int Le16(byte[] b, int o) => b[o] | (b[o + 1] << 8);
    private static int Le32(byte[] b, int o) => b[o] | (b[o+1] << 8) | (b[o+2] << 16) | (b[o+3] << 24);
}
