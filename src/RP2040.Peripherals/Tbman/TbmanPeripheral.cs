using RP2040.Core.Memory;

namespace RP2040.Peripherals.Tbman;

/// <summary>
/// Testbench Manager peripheral (0x4006C000).
/// Allows firmware to detect whether it is running on ASIC, FPGA, or simulation.
/// </summary>
public sealed class TbmanPeripheral : IMemoryMappedDevice
{
    private const uint PLATFORM = 0x00;

    // ASIC bit (bit 1) — match SysInfo.PLATFORM for consistency
    private const uint PLATFORM_ASIC = 0x2;

    public uint Size => 0x1000;

    public uint ReadWord(uint address) => address == PLATFORM ? PLATFORM_ASIC : 0;

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value) { }
    public void WriteHalfWord(uint address, ushort value) { }
    public void WriteByte(uint address, byte value) { }
}
