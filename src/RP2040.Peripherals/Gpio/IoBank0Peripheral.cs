using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals.Sio;

namespace RP2040.Peripherals.Gpio;

/// <summary>
/// IO_BANK0 peripheral (base 0x40014000).
/// Each GPIO pin has a STATUS (RO) and CTRL (RW) register pair at offsets n*8 and n*8+4.
/// FUNCSEL bits [4:0] of CTRL select the peripheral that drives/reads the pin.
/// Supports IRQ edge/level detection and PROC0_INTE/INTF/INTS interrupt bank.
/// </summary>
public sealed class IoBank0Peripheral : IMemoryMappedDevice
{
    private const int GPIO_COUNT = 30;

    // Register layout offsets
    private const uint GPIO_CTRL_LAST  = 0x0EC;  // last byte of GPIO pair area
    private const uint INTR_BASE       = 0x0F0;  // INTR0-3 raw interrupt (write 1 to clear edge)
    private const uint PROC0_INTE_BASE = 0x100;  // PROC0_INTE0-3
    private const uint PROC0_INTF_BASE = 0x110;  // PROC0_INTF0-3
    private const uint PROC0_INTS_BASE = 0x120;  // PROC0_INTS0-3 (RO)
    private const uint PROC1_INTE_BASE = 0x130;  // PROC1 (single-core: store only)
    private const uint PROC1_INTF_BASE = 0x140;
    private const uint PROC1_INTS_BASE = 0x150;

    // IRQ event bits per pin (4 bits per pin in INTR registers)
    private const uint IRQ_LEVEL_LOW  = 1u << 0;
    private const uint IRQ_LEVEL_HIGH = 1u << 1;
    private const uint IRQ_EDGE_LOW   = 1u << 2;
    private const uint IRQ_EDGE_HIGH  = 1u << 3;

    // CTRL field masks
    private const uint FUNCSEL_MASK = 0x1F;
    private const uint FUNCSEL_SIO  = 5;

    // IO_IRQ_BANK0 = hardware IRQ 13
    private const int IO_IRQ_BANK0 = 13;

    private readonly CortexM0Plus? _cpu;
    private readonly SioPeripheral _sio;

    private readonly uint[] _ctrl      = new uint[GPIO_COUNT];
    private readonly bool[] _gpioInput = new bool[GPIO_COUNT];  // current input state
    private readonly uint[] _intrEdge  = new uint[GPIO_COUNT];  // edge IRQ bits per pin (bits 2-3)

    private readonly uint[] _proc0Inte = new uint[4];
    private readonly uint[] _proc0Intf = new uint[4];
    private readonly uint[] _proc1Inte = new uint[4];
    private readonly uint[] _proc1Intf = new uint[4];

    public uint Size => 0x160;

    public IoBank0Peripheral(SioPeripheral sio, CortexM0Plus? cpu = null)
    {
        _sio = sio;
        _cpu = cpu;
        // Default FUNCSEL=31 (NULL / hi-Z) for all pins
        Array.Fill(_ctrl, 0x1Fu);
    }

    // ── GPIO input update ────────────────────────────────────────────

    /// <summary>
    /// Notify that a GPIO input pin changed value. This detects edges and
    /// updates INTR edge bits, then fires the NVIC interrupt if enabled.
    /// </summary>
    public void UpdatePinInput(int pin, bool value)
    {
        if (pin < 0 || pin >= GPIO_COUNT) return;

        var old = _gpioInput[pin];
        _gpioInput[pin] = value;

        if (!old && value) _intrEdge[pin] |= IRQ_EDGE_HIGH;
        if (old && !value) _intrEdge[pin] |= IRQ_EDGE_LOW;

        CheckInterrupts();
    }

    // ── IMemoryMappedDevice ──────────────────────────────────────────

    public uint ReadWord(uint address)
    {
        if (address <= GPIO_CTRL_LAST)
        {
            var pinPair = address >> 3;
            if (pinPair >= GPIO_COUNT) return 0;
            return (address & 4) != 0 ? _ctrl[pinPair] : ReadStatus((int)pinPair);
        }

        if (address >= INTR_BASE && address < PROC0_INTE_BASE)
            return BuildIntr((int)((address - INTR_BASE) >> 2));

        if (address >= PROC0_INTE_BASE && address < PROC0_INTF_BASE)
            return _proc0Inte[(address - PROC0_INTE_BASE) >> 2];

        if (address >= PROC0_INTF_BASE && address < PROC0_INTS_BASE)
            return _proc0Intf[(address - PROC0_INTF_BASE) >> 2];

        if (address >= PROC0_INTS_BASE && address < PROC1_INTE_BASE)
        {
            var reg = (int)((address - PROC0_INTS_BASE) >> 2);
            return (BuildIntr(reg) | _proc0Intf[reg]) & _proc0Inte[reg];
        }

        if (address >= PROC1_INTE_BASE && address < PROC1_INTF_BASE)
            return _proc1Inte[(address - PROC1_INTE_BASE) >> 2];

        if (address >= PROC1_INTF_BASE && address < PROC1_INTS_BASE)
            return _proc1Intf[(address - PROC1_INTF_BASE) >> 2];

        return 0;
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        if (address <= GPIO_CTRL_LAST)
        {
            var pinPair = address >> 3;
            if (pinPair >= GPIO_COUNT) return;
            if ((address & 4) != 0) _ctrl[pinPair] = value;
            // STATUS is read-only
            return;
        }

        if (address >= INTR_BASE && address < PROC0_INTE_BASE)
        {
            // Write 1 to clear edge IRQ bits
            var reg = (int)((address - INTR_BASE) >> 2);
            ClearEdgeBits(reg, value);
            return;
        }

        if (address >= PROC0_INTE_BASE && address < PROC0_INTF_BASE)
        {
            _proc0Inte[(address - PROC0_INTE_BASE) >> 2] = value;
            CheckInterrupts();
            return;
        }

        if (address >= PROC0_INTF_BASE && address < PROC0_INTS_BASE)
        {
            _proc0Intf[(address - PROC0_INTF_BASE) >> 2] = value;
            CheckInterrupts();
            return;
        }

        if (address >= PROC1_INTE_BASE && address < PROC1_INTF_BASE)
        {
            _proc1Inte[(address - PROC1_INTE_BASE) >> 2] = value;
            return;
        }

        if (address >= PROC1_INTF_BASE && address < PROC1_INTS_BASE)
        {
            _proc1Intf[(address - PROC1_INTF_BASE) >> 2] = value;
            return;
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

    // ── Private helpers ──────────────────────────────────────────────

    private uint ReadStatus(int pin)
    {
        var status = 0u;
        var funcsel = _ctrl[pin] & FUNCSEL_MASK;
        if (funcsel == FUNCSEL_SIO)
        {
            if ((_sio.GpioOe & (1u << pin)) != 0) status |= 1u << 13;  // OETOPAD
            if ((_sio.GpioOut & (1u << pin)) != 0) status |= 1u << 9;  // OUTTOPAD
        }
        if (_gpioInput[pin]) status |= (1u << 17) | (1u << 19);  // INFROMPAD + INTOPERI
        return status;
    }

    /// <summary>
    /// Build INTR register N (8 GPIOs per register, 4 bits each).
    /// LEVEL bits computed from current input; EDGE bits from stored state.
    /// </summary>
    private uint BuildIntr(int reg)
    {
        var result = 0u;
        for (var i = 0; i < 8; i++)
        {
            var pin = reg * 8 + i;
            if (pin >= GPIO_COUNT) break;

            uint bits = 0;
            bits |= !_gpioInput[pin] ? IRQ_LEVEL_LOW  : 0u;
            bits |= _gpioInput[pin]  ? IRQ_LEVEL_HIGH : 0u;
            bits |= _intrEdge[pin] & (IRQ_EDGE_LOW | IRQ_EDGE_HIGH);
            result |= bits << (i * 4);
        }
        return result;
    }

    private void ClearEdgeBits(int reg, uint mask)
    {
        for (var i = 0; i < 8; i++)
        {
            var pin = reg * 8 + i;
            if (pin >= GPIO_COUNT) break;
            var bits = (mask >> (i * 4)) & 0xF;
            _intrEdge[pin] &= ~(bits & (IRQ_EDGE_LOW | IRQ_EDGE_HIGH));
        }
        CheckInterrupts();
    }

    private void CheckInterrupts()
    {
        if (_cpu is null) return;
        var active = false;
        for (var reg = 0; reg < 4 && !active; reg++)
            active = ((BuildIntr(reg) | _proc0Intf[reg]) & _proc0Inte[reg]) != 0;
        _cpu.SetInterrupt(IO_IRQ_BANK0, active);
    }
}
