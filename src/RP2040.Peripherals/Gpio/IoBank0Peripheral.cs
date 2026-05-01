using RP2040.Core.Memory;
using RP2040.Peripherals.Sio;

namespace RP2040.Peripherals.Gpio;

/// <summary>
/// IO_BANK0 peripheral (base 0x40014000).
/// Each GPIO pin has a STATUS (RO) and CTRL (RW) register pair at offsets n*8 and n*8+4.
/// FUNCSEL bits [4:0] of CTRL select the function; FUNCSEL=5 routes the pin through SIO.
/// </summary>
public sealed class IoBank0Peripheral : IMemoryMappedDevice
{
    private const int GPIO_COUNT = 30;
    private const uint STATUS_OFFSET = 0;
    private const uint CTRL_OFFSET   = 4;

    // CTRL fields of interest
    private const uint FUNCSEL_MASK = 0x1F;
    private const uint FUNCSEL_SIO  = 5;

    private readonly uint[] _ctrl = new uint[GPIO_COUNT];
    private readonly SioPeripheral _sio;

    public uint Size => (uint)(GPIO_COUNT * 8);

    public IoBank0Peripheral(SioPeripheral sio)
    {
        _sio = sio;
        // Default FUNCSEL=31 (NULL / hi-Z) for all pins
        Array.Fill(_ctrl, 0x1Fu);
    }

    public uint ReadWord(uint address)
    {
        var pinPair = address >> 3;   // each pin has 8 bytes
        if (pinPair >= GPIO_COUNT) return 0;

        var isCtrl = (address & 4) != 0;
        if (isCtrl)
            return _ctrl[pinPair];

        // STATUS: reflect SIO output value when FUNCSEL=SIO
        var pin = (int)pinPair;
        var status = 0u;
        if ((_ctrl[pin] & FUNCSEL_MASK) == FUNCSEL_SIO)
        {
            if ((_sio.GpioOe & (1u << pin)) != 0)
                status |= (1u << 9);   // OUTTOPAD - driving high
            if ((_sio.GpioOut & (1u << pin)) != 0)
                status |= (1u << 8);   // OUTFROMPERI
        }
        return status;
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        var pinPair = address >> 3;
        if (pinPair >= GPIO_COUNT) return;

        var isCtrl = (address & 4) != 0;
        if (!isCtrl) return;   // STATUS is read-only

        _ctrl[pinPair] = value;
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 2) << 3);
        var current = ReadWord(aligned);
        WriteWord(aligned, (current & ~(0xFFFFu << shift)) | ((uint)value << shift));
    }

    public void WriteByte(uint address, byte value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 3) << 3);
        var current = ReadWord(aligned);
        WriteWord(aligned, (current & ~(0xFFu << shift)) | ((uint)value << shift));
    }
}
