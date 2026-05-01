using RP2040.Core.Cpu;
using RP2040.Core.Memory;

namespace RP2040.Peripherals.Adc;

/// <summary>
/// RP2040 ADC peripheral (base 0x4004C000).
/// 4 external channels (GPIO26-29) + 1 internal temperature sensor.
/// Conversion results are provided via injectable callbacks for simulation.
/// </summary>
public sealed class AdcPeripheral : IMemoryMappedDevice
{
    private const uint ADC_CS     = 0x000;  // Control / Status
    private const uint ADC_RESULT = 0x004;  // Conversion result (12-bit, read-only)
    private const uint ADC_FCS    = 0x008;  // FIFO control / status
    private const uint ADC_FIFO   = 0x00C;  // FIFO result
    private const uint ADC_DIV    = 0x010;  // Clock divisor
    private const uint ADC_INTR   = 0x014;  // Raw interrupt status
    private const uint ADC_INTE   = 0x018;  // Interrupt enable
    private const uint ADC_INTF   = 0x01C;  // Force interrupt
    private const uint ADC_INTS   = 0x020;  // Masked interrupt status

    private const int CHANNEL_COUNT = 5;

    private readonly CortexM0Plus _cpu;

    private uint _cs;     // Includes selected channel (bits 14:12), EN (bit 0), START_ONCE (bit 2)
    private uint _result; // Latest 12-bit conversion result
    private uint _div;
    private uint _inte;
    private uint _intf;

    /// <summary>
    /// Optional per-channel value provider. Return a 12-bit value (0-4095).
    /// If null for a channel, returns 0.
    /// </summary>
    public Func<int, ushort>? ReadChannel;

    public uint Size => 0x100;

    public AdcPeripheral(CortexM0Plus cpu)
    {
        _cpu = cpu;
    }

    public uint ReadWord(uint address)
    {
        return address switch
        {
            ADC_CS     => _cs,
            ADC_RESULT => _result & 0xFFF,
            ADC_FCS    => 0,  // FIFO not simulated
            ADC_FIFO   => _result & 0xFFF,
            ADC_DIV    => _div,
            ADC_INTR   => 0,
            ADC_INTE   => _inte,
            ADC_INTF   => _intf,
            ADC_INTS   => _intf & _inte,
            _ => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case ADC_CS:
                _cs = value;
                if ((value & (1u << 2)) != 0)  // START_ONCE
                    PerformConversion();
                break;
            case ADC_DIV:
                _div = value;
                break;
            case ADC_INTE:
                _inte = value & 1;
                break;
            case ADC_INTF:
                _intf = value & 1;
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

    private void PerformConversion()
    {
        var channel = (int)((_cs >> 12) & 0x7);
        if (channel >= CHANNEL_COUNT) channel = 0;

        _result = ReadChannel?.Invoke(channel) ?? 0;
        _result &= 0xFFF;

        // Clear START_ONCE, set READY
        _cs = (_cs & ~(1u << 2)) | (1u << 8);
    }
}
