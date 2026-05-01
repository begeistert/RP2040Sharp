using RP2040.Core.Memory;

namespace RP2040.Peripherals.Resets;

/// <summary>
/// RESETS peripheral (0x4000C000).
/// Controls reset state of each RP2040 subsystem.
/// Firmware writes RESET bits to hold subsystems in reset, then clears bits
/// to bring them out. RESET_DONE returns the complement — polled by SDK init.
/// </summary>
public sealed class ResetsPeripheral : IMemoryMappedDevice
{
    private const uint RESET      = 0x00;
    private const uint WDSEL      = 0x04;
    private const uint RESET_DONE = 0x08;

    // 25 subsystem bits
    private const uint ALL_BITS = 0x01FFFFFF;

    // Start with nothing in reset so RESET_DONE = ALL_BITS from power-on.
    // Firmware reset/unreset sequences will still work correctly because after
    // firmware writes RESET then clears it, RESET_DONE returns that bit set.
    private uint _reset = 0;
    private uint _wdsel;

    public uint Size => 0x1000;

    public uint ReadWord(uint address) => address switch
    {
        RESET      => _reset,
        WDSEL      => _wdsel,
        RESET_DONE => (~_reset) & ALL_BITS,
        _          => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case RESET: _reset = value & ALL_BITS; break;
            case WDSEL: _wdsel = value & ALL_BITS; break;
        }
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
