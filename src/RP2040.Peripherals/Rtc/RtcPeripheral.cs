using RP2040.Core.Memory;

namespace RP2040.Peripherals.Rtc;

/// <summary>
/// Real-Time Clock peripheral (0x4005C000).
/// Implements register storage and basic time tracking. The RTC does not
/// advance automatically in simulation — the host must inject time via
/// <see cref="SetDateTime"/>.
/// </summary>
public sealed class RtcPeripheral : IMemoryMappedDevice
{
    private const uint RTC_SETUP0   = 0x04;   // YEAR[27:16], MONTH[11:8], DAY[4:0]
    private const uint RTC_SETUP1   = 0x08;   // DOTW[26:24], HOUR[20:16], MIN[13:8], SEC[5:0]
    private const uint RTC_CTRL     = 0x0C;   // ENABLE[0], ACTIVE[1], LOAD[4]
    private const uint IRQ_SETUP_0  = 0x10;
    private const uint IRQ_SETUP_1  = 0x14;
    private const uint RTC_RTC1     = 0x18;   // DOTW/HOUR/MIN read (same layout as SETUP1 bits)
    private const uint RTC_RTC0     = 0x1C;   // YEAR/MONTH/DAY read

    private uint _setup0;
    private uint _setup1;
    private uint _ctrl;
    private uint _irqSetup0;
    private uint _irqSetup1;
    // Latched time (set by LOAD or SetDateTime)
    private uint _rtc0;   // YEAR[27:16] MONTH[11:8] DAY[4:0]
    private uint _rtc1;   // DOTW[26:24] HOUR[20:16] MIN[13:8] SEC[5:0]

    private const uint CTRL_ENABLE = 1u;
    private const uint CTRL_ACTIVE = 1u << 1;
    private const uint CTRL_LOAD   = 1u << 4;

    public uint Size => 0x1000;

    /// <summary>Inject a specific date/time into the RTC.</summary>
    public void SetDateTime(int year, int month, int day, int dayOfWeek, int hour, int min, int sec)
    {
        _rtc0 = ((uint)year << 16) | ((uint)month << 8) | (uint)day;
        _rtc1 = ((uint)dayOfWeek << 24) | ((uint)hour << 16) | ((uint)min << 8) | (uint)sec;
    }

    public uint ReadWord(uint address) => address switch
    {
        RTC_SETUP0  => _setup0,
        RTC_SETUP1  => _setup1,
        RTC_CTRL    => _ctrl | CTRL_ACTIVE,   // always report active when enabled
        IRQ_SETUP_0 => _irqSetup0,
        IRQ_SETUP_1 => _irqSetup1,
        RTC_RTC1    => _rtc1,
        RTC_RTC0    => _rtc0,
        _           => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case RTC_SETUP0:
                _setup0 = value;
                break;
            case RTC_SETUP1:
                _setup1 = value;
                break;
            case RTC_CTRL:
                if ((value & CTRL_LOAD) != 0)
                {
                    // LOAD: latch SETUP values into the running counter
                    _rtc0 = _setup0;
                    _rtc1 = _setup1;
                }
                _ctrl = value & (CTRL_ENABLE | CTRL_LOAD);
                break;
            case IRQ_SETUP_0: _irqSetup0 = value; break;
            case IRQ_SETUP_1: _irqSetup1 = value; break;
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
