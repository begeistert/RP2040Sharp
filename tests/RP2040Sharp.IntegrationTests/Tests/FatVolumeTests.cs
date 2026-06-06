using FluentAssertions;
using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Unit tests for <see cref="FatVolume"/> using an in-memory FAT12 disk image.
/// These tests run entirely offline (no firmware download required).
/// </summary>
public sealed class FatVolumeTests
{
    // A minimal 256 KB FAT12 disk (512 sectors × 512 bytes).
    private const int SECTOR_BYTES  = 512;
    private const int TOTAL_SECTORS = 512;
    private const int RESERVED      = 1;      // 1 reserved sector (VBR)
    private const int NUM_FATS      = 2;
    private const int ROOT_ENTRIES  = 32;     // 1 sector of root dir
    private const int SECTORS_PER_CLUSTER = 1;
    private const int SECTORS_PER_FAT    = 1; // FAT12: 512 × 8 / 12 ≈ 341 entries → fits in 1 sector

    private static byte[][] BuildDisk()
    {
        var disk = new byte[TOTAL_SECTORS][];
        for (var i = 0; i < TOTAL_SECTORS; i++) disk[i] = new byte[SECTOR_BYTES];

        // Write VBR / BPB (BIOS Parameter Block).
        var vbr = disk[0];
        // Jump boot
        vbr[0] = 0xEB; vbr[1] = 0x58; vbr[2] = 0x90;
        // OEM name "MSDOS5.0"
        System.Text.Encoding.ASCII.GetBytes("MSDOS5.0", 0, 8, vbr, 3);
        // Bytes per sector = 512
        vbr[11] = 0x00; vbr[12] = 0x02;
        // Sectors per cluster = 1
        vbr[13] = SECTORS_PER_CLUSTER;
        // Reserved sectors = 1
        vbr[14] = RESERVED; vbr[15] = 0x00;
        // Number of FATs = 2
        vbr[16] = NUM_FATS;
        // Root entry count = 32
        vbr[17] = ROOT_ENTRIES; vbr[18] = 0x00;
        // Total sectors16 = 512
        vbr[19] = (byte)(TOTAL_SECTORS & 0xFF); vbr[20] = (byte)(TOTAL_SECTORS >> 8);
        // Media type
        vbr[21] = 0xF8;
        // Sectors per FAT = 1
        vbr[22] = SECTORS_PER_FAT; vbr[23] = 0x00;
        // Boot sector signature
        vbr[510] = 0x55; vbr[511] = 0xAA;

        // FAT1: sector 1.  Mark cluster 0 (media) and cluster 1 (reserved) as used.
        var fat1 = disk[1];
        fat1[0] = 0xF8; fat1[1] = 0xFF; fat1[2] = 0xFF; // clusters 0+1 reserved

        // FAT2: sector 2 (copy).
        disk[2][0] = 0xF8; disk[2][1] = 0xFF; disk[2][2] = 0xFF;

        // Root directory: sector 3.  (Empty for now — tests will write to it.)

        return disk;
    }

    private static (FatVolume fat, byte[][] disk) OpenDisk()
    {
        var disk = BuildDisk();
        var fat = FatVolume.Open(
            lba  => disk[lba],
            (lba, data) => { var sec = new byte[SECTOR_BYTES]; data.CopyTo(sec, 0); disk[lba] = sec; });
        return (fat!, disk);
    }

    [Fact]
    public void PadName83_ShortName_PadsCorrectly()
    {
        FatVolume.PadName83("code.py").Should().Be("CODE    PY ");
        FatVolume.PadName83("main.py").Should().Be("MAIN    PY ");
        FatVolume.PadName83("readme.txt").Should().Be("README  TXT");
    }

    [Fact]
    public void PadName83_NoExtension_PadsWithSpaces()
    {
        FatVolume.PadName83("boot").Should().Be("BOOT       ");
    }

    [Fact]
    public void Open_ValidVbr_ReturnsNonNullAndIsValid()
    {
        var (fat, _) = OpenDisk();
        fat.Should().NotBeNull();
        fat.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Open_EmptyDisk_ReturnsNull()
    {
        var empty = new byte[SECTOR_BYTES];
        var fat = FatVolume.Open(_ => empty, (_, __) => { });
        fat.Should().BeNull("an all-zero VBR is not a valid FAT filesystem");
    }

    [Fact]
    public void WriteFile_NewFile_AppearInRootDirectory()
    {
        var (fat, disk) = OpenDisk();

        var content = "print('hello')\n"u8.ToArray();
        fat.WriteFile("code.py", content).Should().BeTrue();

        // Root dir is sector 3 (reserved=1, FAT×2=2 → sector 3).
        var rootDir = disk[3];
        var name = System.Text.Encoding.ASCII.GetString(rootDir, 0, 11);
        name.Should().Be("CODE    PY ", "file name must be stored in 8.3 format");
        rootDir[11].Should().Be(0x20, "archive attribute must be set");

        // First cluster stored at offset 26.
        var firstCluster = rootDir[26] | (rootDir[27] << 8);
        firstCluster.Should().BeGreaterThanOrEqualTo(2, "first cluster must be in the data area");

        // File size at offset 28.
        var fileSize = rootDir[28] | (rootDir[29] << 8) | (rootDir[30] << 16) | (rootDir[31] << 24);
        fileSize.Should().Be(content.Length);
    }

    [Fact]
    public void WriteFile_SmallContent_DataWrittenToCluster()
    {
        var (fat, disk) = OpenDisk();

        var content = "hello\n"u8.ToArray();
        fat.WriteFile("test.txt", content).Should().BeTrue();

        // Root dir sector is 3; first cluster starts at data sector = 3 (root) + 1 = 4.
        // Cluster 2 → data sector index 4.
        var rootDir = disk[3];
        var firstCluster = rootDir[26] | (rootDir[27] << 8);
        var dataStart = RESERVED + NUM_FATS * SECTORS_PER_FAT +
                        (ROOT_ENTRIES * 32 + SECTOR_BYTES - 1) / SECTOR_BYTES;
        var dataSector = dataStart + (firstCluster - 2) * SECTORS_PER_CLUSTER;

        var actual = disk[dataSector][..content.Length];
        actual.Should().Equal(content, "file content must be written to the cluster");
    }

    [Fact]
    public void WriteFile_OverwriteExistingFile_UpdatesContent()
    {
        var (fat, disk) = OpenDisk();

        fat.WriteFile("code.py", "v1\n"u8.ToArray()).Should().BeTrue();
        fat.WriteFile("code.py", "v2 longer\n"u8.ToArray()).Should().BeTrue();

        var rootDir = disk[3];
        var fileSize = rootDir[28] | (rootDir[29] << 8) | (rootDir[30] << 16) | (rootDir[31] << 24);
        fileSize.Should().Be("v2 longer\n"u8.Length);

        var firstCluster = rootDir[26] | (rootDir[27] << 8);
        var dataStart = RESERVED + NUM_FATS * SECTORS_PER_FAT +
                        (ROOT_ENTRIES * 32 + SECTOR_BYTES - 1) / SECTOR_BYTES;
        var dataSector = dataStart + (firstCluster - 2) * SECTORS_PER_CLUSTER;
        var actual = disk[dataSector][.."v2 longer\n".Length];
        System.Text.Encoding.UTF8.GetString(actual).Should().Be("v2 longer\n");
    }

    [Fact]
    public void WriteFile_MultipleFiles_BothPresent()
    {
        var (fat, disk) = OpenDisk();

        fat.WriteFile("code.py", "a"u8.ToArray()).Should().BeTrue();
        fat.WriteFile("main.py", "b"u8.ToArray()).Should().BeTrue();

        var rootDir = disk[3];
        var name0 = System.Text.Encoding.ASCII.GetString(rootDir, 0, 11);
        var name1 = System.Text.Encoding.ASCII.GetString(rootDir, 32, 11);

        name0.Should().Be("CODE    PY ");
        name1.Should().Be("MAIN    PY ");
    }
}
