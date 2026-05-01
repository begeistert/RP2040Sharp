using RP2040.Core.Memory;

namespace RP2040.Peripherals.Busctrl;

/// <summary>
/// Bus Fabric control peripheral (0x40030000).
/// Controls bus priority and performance counters.
/// In simulation buses have no contention, so all counters stay at 0.
/// </summary>
public sealed class BusctrlPeripheral : IMemoryMappedDevice
{
    private const uint BUS_PRIORITY     = 0x000;
    private const uint BUS_PRIORITY_ACK = 0x004;
    private const uint PERFCTR0         = 0x008;
    private const uint PERFSEL0         = 0x00C;
    private const uint PERFCTR1         = 0x010;
    private const uint PERFSEL1         = 0x014;
    private const uint PERFCTR2         = 0x018;
    private const uint PERFSEL2         = 0x01C;
    private const uint PERFCTR3         = 0x020;
    private const uint PERFSEL3         = 0x024;

    private uint _priority;
    private readonly uint[] _perfsel = new uint[4];

    public uint Size => 0x1000;

    public uint ReadWord(uint address) => address switch
    {
        BUS_PRIORITY     => _priority,
        BUS_PRIORITY_ACK => _priority,    // ack mirrors priority in simulation
        PERFCTR0         => 0,
        PERFSEL0         => _perfsel[0],
        PERFCTR1         => 0,
        PERFSEL1         => _perfsel[1],
        PERFCTR2         => 0,
        PERFSEL2         => _perfsel[2],
        PERFCTR3         => 0,
        PERFSEL3         => _perfsel[3],
        _                => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case BUS_PRIORITY: _priority  = value & 0xF; break;
            case PERFSEL0:     _perfsel[0] = value; break;
            case PERFSEL1:     _perfsel[1] = value; break;
            case PERFSEL2:     _perfsel[2] = value; break;
            case PERFSEL3:     _perfsel[3] = value; break;
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
