namespace RP2040Sharp.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal UF2 parser: extracts the Flash image from a UF2 file and returns it as a flat
/// byte array ready to load with <c>machine.LoadFlash()</c>.
/// </summary>
public static class Uf2Reader
{
    private const uint UF2_MAGIC_START0 = 0x0A324655; // "UF2\n"
    private const uint UF2_MAGIC_START1 = 0x9E5D5157;
    private const uint UF2_MAGIC_END    = 0x0AB16F30;
    private const int  UF2_BLOCK_SIZE   = 512;
    private const int  UF2_DATA_OFFSET  = 32;
    private const int  UF2_DATA_SIZE    = 256;

    /// <summary>
    /// Parse a UF2 byte array and return the Flash image.
    /// All blocks must target a contiguous Flash range; gaps are filled with 0xFF.
    /// </summary>
    public static byte[] ToFlashImage(byte[] uf2)
    {
        var blocks = uf2.Length / UF2_BLOCK_SIZE;
        uint minAddr = uint.MaxValue;
        uint maxAddr = 0;

        // First pass: determine address range
        for (var i = 0; i < blocks; i++)
        {
            var off = i * UF2_BLOCK_SIZE;
            var magic0 = ReadU32(uf2, off);
            var magic1 = ReadU32(uf2, off + 4);
            if (magic0 != UF2_MAGIC_START0 || magic1 != UF2_MAGIC_START1)
                continue;

            var targetAddr = ReadU32(uf2, off + 12);
            var payloadSize = ReadU32(uf2, off + 16);
            if (payloadSize == 0 || payloadSize > 256) continue;

            if (targetAddr < minAddr) minAddr = targetAddr;
            if (targetAddr + payloadSize > maxAddr) maxAddr = targetAddr + payloadSize;
        }

        if (minAddr == uint.MaxValue)
            throw new InvalidDataException("No valid UF2 blocks found.");

        // RP2040 Flash starts at 0x10000000 — strip the base address
        const uint flashBase = 0x10000000;
        if (minAddr < flashBase)
            throw new InvalidDataException($"UF2 target address 0x{minAddr:X8} is below Flash base 0x{flashBase:X8}.");

        var imageSize = (int)(maxAddr - flashBase);
        var image = new byte[imageSize];
        Array.Fill(image, (byte)0xFF);

        // Second pass: copy payload data
        for (var i = 0; i < blocks; i++)
        {
            var off = i * UF2_BLOCK_SIZE;
            var magic0 = ReadU32(uf2, off);
            var magic1 = ReadU32(uf2, off + 4);
            if (magic0 != UF2_MAGIC_START0 || magic1 != UF2_MAGIC_START1)
                continue;

            var targetAddr  = ReadU32(uf2, off + 12);
            var payloadSize = ReadU32(uf2, off + 16);
            if (payloadSize == 0 || payloadSize > 256) continue;

            var destOffset = (int)(targetAddr - flashBase);
            Buffer.BlockCopy(uf2, off + UF2_DATA_OFFSET, image, destOffset, (int)payloadSize);
        }

        return image;
    }

    private static uint ReadU32(byte[] buf, int offset) =>
        (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
}
