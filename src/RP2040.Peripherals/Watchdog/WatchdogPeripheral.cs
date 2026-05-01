using RP2040.Core.Memory;

namespace RP2040.Peripherals.Watchdog;

/// <summary>
/// Watchdog peripheral (0x40058000).
/// Implements SCRATCH registers (used by pico-sdk to pass boot info),
/// TICK generator, and watchdog control. In simulation the watchdog timer
/// never fires unless TRIGGER bit is explicitly set.
/// </summary>
public sealed class WatchdogPeripheral : IMemoryMappedDevice
{
    private const uint CTRL      = 0x00;
    private const uint LOAD      = 0x04;
    private const uint REASON    = 0x08;
    private const uint SCRATCH0  = 0x0C;
    private const uint SCRATCH7  = 0x28;   // SCRATCH0 + 7*4
    private const uint TICK      = 0x2C;

    // CTRL bits
    private const uint CTRL_TRIGGER = 1u << 31;
    private const uint CTRL_ENABLE  = 1u << 30;

    // TICK bits: [8:0] = CYCLES (divider), [9] = ENABLE, [10] = RUNNING, [19:11] = COUNT
    private const uint TICK_RUNNING = 1u << 10;
    private const uint TICK_ENABLE  = 1u << 9;

    private uint _ctrl;
    private uint _load;
    private uint _reason;
    private uint _tick = TICK_ENABLE | TICK_RUNNING | 12;  // enabled, running, 12 cycles (default)
    private readonly uint[] _scratch = new uint[8];

    public uint Size => 0x1000;

    public uint ReadWord(uint address)
    {
        if (address >= SCRATCH0 && address <= SCRATCH7)
            return _scratch[(address - SCRATCH0) >> 2];

        return address switch
        {
            CTRL   => _ctrl,
            LOAD   => _load,
            REASON => _reason,
            TICK   => _tick,
            _      => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        if (address >= SCRATCH0 && address <= SCRATCH7)
        {
            _scratch[(address - SCRATCH0) >> 2] = value;
            return;
        }

        switch (address)
        {
            case CTRL:
                _ctrl = value & ~CTRL_TRIGGER;  // TRIGGER is write-only strobe
                break;
            case LOAD:
                _load = value & 0x00FFFFFF;
                break;
            case TICK:
                // ENABLE bit and CYCLES field; RUNNING and COUNT are read-only status
                _tick = (_tick & ~0x3FFu) | (value & 0x3FF);
                // If enable bit is set, mark as running
                if ((value & TICK_ENABLE) != 0)
                    _tick |= TICK_RUNNING;
                else
                    _tick &= ~TICK_RUNNING;
                break;
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
