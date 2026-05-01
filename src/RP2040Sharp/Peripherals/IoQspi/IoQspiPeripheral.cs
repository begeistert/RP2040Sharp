using RP2040.Core.Memory;

namespace RP2040.Peripherals.IoQspi;

/// <summary>
/// IO_QSPI peripheral stub (0x40018000).
/// Controls the QSPI GPIO pins (SCLK, SS, SD0-SD3). Stores FUNCSEL/CTRL
/// registers but has no electrical simulation.
/// </summary>
public sealed class IoQspiPeripheral : IMemoryMappedDevice
{
    // 6 QSPI GPIO pins: SCLK, SS, SD0, SD1, SD2, SD3
    // Each pin: STATUS (0, RO) + CTRL (4, R/W) → stride 8 bytes
    private const int PIN_COUNT = 6;
    private readonly uint[] _ctrl = new uint[PIN_COUNT];  // FUNCSEL + overrides

    public uint Size => 0x1000;

    public uint ReadWord(uint address)
    {
        var pin = (int)(address >> 3) & 0x1F;
        if (pin >= PIN_COUNT) return 0;
        var field = address & 7;
        return field switch
        {
            0 => 0,          // STATUS (read-only, always 0 in stub)
            4 => _ctrl[pin],
            _ => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        var pin = (int)(address >> 3) & 0x1F;
        if (pin >= PIN_COUNT) return;
        if ((address & 7) == 4)
            _ctrl[pin] = value;
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 2) << 3);
        WriteWord(aligned, (ReadWord(aligned) & ~(0xFFFFu << shift)) | ((uint)value << shift));
    }

    public void WriteByte(uint address, byte value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 3) << 3);
        WriteWord(aligned, (ReadWord(aligned) & ~(0xFFu << shift)) | ((uint)value << shift));
    }
}
