using RP2040.Core.Memory;

namespace RP2040.Peripherals.SysInfo;

/// <summary>
/// SysInfo peripheral (0x40000000).
/// Read-only chip identification registers.
/// </summary>
public sealed class SysInfoPeripheral : IMemoryMappedDevice
{
    private const uint CHIP_ID       = 0x00;   // RP2040 chip ID
    private const uint PLATFORM      = 0x04;   // 0=FPGA, 1=ASIC, 2=SIMULATION
    private const uint GITREF_RP2040 = 0x40;   // ROM git ref

    // RP2040-B2 chip ID: MANUFACTURER=0x927, PART=0x2, REVISION=2 → 0x10029927
    private const uint RP2040_CHIP_ID = 0x10029927;

    public uint Size => 0x1000;

    public uint ReadWord(uint address) => address switch
    {
        CHIP_ID       => RP2040_CHIP_ID,
        PLATFORM      => 1,    // ASIC
        GITREF_RP2040 => 0,
        _             => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value) { }
    public void WriteHalfWord(uint address, ushort value) { }
    public void WriteByte(uint address, byte value) { }
}
