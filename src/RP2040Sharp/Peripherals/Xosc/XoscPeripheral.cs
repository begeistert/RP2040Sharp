using RP2040.Core.Memory;

namespace RP2040.Peripherals.Xosc;

/// <summary>
/// Crystal Oscillator peripheral (0x40024000).
/// In simulation the crystal is always stable and enabled.
/// </summary>
public sealed class XoscPeripheral : IMemoryMappedDevice
{
    private const uint XOSC_CTRL    = 0x00;   // FREQ_RANGE, ENABLE
    private const uint XOSC_STATUS  = 0x04;   // STABLE(31), ENABLED(12), FREQ_RANGE(1:0)
    private const uint XOSC_DORMANT = 0x08;   // dormancy control
    private const uint XOSC_STARTUP = 0x0C;   // startup delay
    private const uint XOSC_COUNT   = 0x1C;   // countdown

    private const uint CTRL_ENABLE_VALUE  = 0xFAB000;  // enable magic value (bits [23:12])
    private const uint CTRL_DISABLE_VALUE = 0xD1E000;

    private const uint STATUS_STABLE  = 1u << 31;
    private const uint STATUS_ENABLED = 1u << 12;

    private uint _ctrl    = CTRL_ENABLE_VALUE | 0xAA0;   // enabled, 1–15 MHz range
    private uint _dormant = 0;
    private uint _startup = 0xC4;  // default startup delay

    public uint Size => 0x1000;

    public uint ReadWord(uint address) => address switch
    {
        XOSC_CTRL    => _ctrl,
        XOSC_STATUS  => STATUS_STABLE | STATUS_ENABLED | 0xAA0,  // always stable
        XOSC_DORMANT => _dormant,
        XOSC_STARTUP => _startup,
        XOSC_COUNT   => 0,
        _            => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case XOSC_CTRL:    _ctrl    = value; break;
            case XOSC_DORMANT: _dormant = value; break;   // dormancy ignored in sim
            case XOSC_STARTUP: _startup = value; break;
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
