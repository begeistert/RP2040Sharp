using RP2040.Core.Memory;

namespace RP2040.Peripherals.Pads;

/// <summary>
/// PADS_BANK0 peripheral (0x4001C000) and PADS_QSPI (0x40020000).
/// Controls pad electrical characteristics: I/O enable, drive strength, pull,
/// schmitt trigger, slew rate. In simulation these are stored but have no
/// electrical effect — GPIO function is handled by IO_BANK0.
/// </summary>
public sealed class PadsPeripheral : IMemoryMappedDevice
{
    private const uint VOLTAGE_SELECT = 0x000;    // 0=3.3V, 1=1.8V
    private const uint GPIO_FIRST     = 0x004;    // GPIO0
    // GPIO_LAST = GPIO_FIRST + 29*4 = 0x078

    // Store voltage select + up to 32 GPIO pads
    private uint _voltageSelect;
    private readonly uint[] _gpioRegs = new uint[32];

    public uint Size => 0x1000;

    // Default pad value: IE=1 (input enabled), DRIVE=4mA (bits[5:4]=01), PUE=0, PDE=0
    private const uint DEFAULT_PAD = (1u << 6);  // IE bit

    public PadsPeripheral()
    {
        for (int i = 0; i < _gpioRegs.Length; i++)
            _gpioRegs[i] = DEFAULT_PAD;
    }

    public uint ReadWord(uint address)
    {
        if (address == VOLTAGE_SELECT) return _voltageSelect;
        if (address >= GPIO_FIRST && address < GPIO_FIRST + 32 * 4)
        {
            var idx = (address - GPIO_FIRST) >> 2;
            return _gpioRegs[idx];
        }
        return 0;
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        if (address == VOLTAGE_SELECT) { _voltageSelect = value & 1; return; }
        if (address >= GPIO_FIRST && address < GPIO_FIRST + 32 * 4)
        {
            var idx = (address - GPIO_FIRST) >> 2;
            _gpioRegs[idx] = value & 0xFF;
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
