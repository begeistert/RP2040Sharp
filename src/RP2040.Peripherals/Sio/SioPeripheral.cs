using RP2040.Core.Cpu;
using RP2040.Core.Memory;

namespace RP2040.Peripherals.Sio;

/// <summary>
/// Single-Cycle I/O (SIO) peripheral.
/// Base address: 0xD0000000. Register with BusInterconnect via MapDevice(0xD, sio).
/// Addresses received are already masked (address &amp; 0x0FFFFFFF); since SIO is the
/// only device in region 0xD, local offset = address directly.
/// </summary>
public sealed class SioPeripheral : IMemoryMappedDevice
{
    // ── GPIO (offsets from SIO base) ─────────────────────────────────
    private const uint GPIO_IN      = 0x004;  // Read GPIO pin state
    private const uint GPIO_OUT     = 0x010;  // Direct set GPIO output
    private const uint GPIO_OUT_SET = 0x014;  // Atomic set
    private const uint GPIO_OUT_CLR = 0x018;  // Atomic clear
    private const uint GPIO_OUT_XOR = 0x01C;  // Atomic XOR
    private const uint GPIO_OE      = 0x020;  // Output enable
    private const uint GPIO_OE_SET  = 0x024;
    private const uint GPIO_OE_CLR  = 0x028;
    private const uint GPIO_OE_XOR  = 0x02C;

    // ── Hardware divider ─────────────────────────────────────────────
    private const uint DIV_UDIVIDEND = 0x060;
    private const uint DIV_UDIVISOR  = 0x064;
    private const uint DIV_SDIVIDEND = 0x068;
    private const uint DIV_SDIVISOR  = 0x06C;
    private const uint DIV_QUOTIENT  = 0x070;
    private const uint DIV_REMAINDER = 0x074;
    private const uint DIV_CSR       = 0x078;  // bit0=DIRTY, bit1=READY

    // ── Spinlocks ────────────────────────────────────────────────────
    private const uint SPINLOCK_BASE = 0x100;
    private const uint SPINLOCK_END  = 0x17F;

    private readonly CortexM0Plus _cpu;

    // GPIO state
    private uint _gpioOut;
    private uint _gpioOe;
    private uint _gpioIn;   // driven by IoBank0 / external input

    // Hardware divider state
    private uint _divUdividend, _divUdivisor;
    private int  _divSdividend, _divSdivisor;
    private uint _divQuotient, _divRemainder;
    private uint _divCsr;          // DIRTY | READY
    private bool _divSigned;

    // Spinlocks: bit N = 1 means spinlock N is taken
    private uint _spinLocks;

    public uint Size => 0x1000;

    /// <summary>Optionally feed current GPIO input state from IO_BANK0.</summary>
    public uint GpioIn
    {
        get => _gpioIn;
        set => _gpioIn = value;
    }

    /// <summary>Current GPIO direction mask (1 = output).</summary>
    public uint GpioOe => _gpioOe;

    /// <summary>Current GPIO output value.</summary>
    public uint GpioOut => _gpioOut;

    public SioPeripheral(CortexM0Plus cpu)
    {
        _cpu = cpu;
    }

    // ── IMemoryMappedDevice — reads ──────────────────────────────────

    public uint ReadWord(uint address)
    {
        if (address >= SPINLOCK_BASE && address <= SPINLOCK_END)
            return ReadSpinlock((int)((address - SPINLOCK_BASE) >> 2));

        return address switch
        {
            GPIO_IN      => _gpioIn,
            GPIO_OUT     => _gpioOut,
            GPIO_OE      => _gpioOe,
            DIV_UDIVIDEND => _divUdividend,
            DIV_UDIVISOR  => _divUdivisor,
            DIV_SDIVIDEND => (uint)_divSdividend,
            DIV_SDIVISOR  => (uint)_divSdivisor,
            DIV_QUOTIENT  => _divQuotient,
            DIV_REMAINDER => _divRemainder,
            DIV_CSR       => _divCsr,
            _ => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    // ── IMemoryMappedDevice — writes ─────────────────────────────────

    public void WriteWord(uint address, uint value)
    {
        if (address >= SPINLOCK_BASE && address <= SPINLOCK_END)
        {
            // Any write releases the spinlock
            var bit = 1u << (int)((address - SPINLOCK_BASE) >> 2);
            _spinLocks &= ~bit;
            return;
        }

        switch (address)
        {
            case GPIO_OUT:     _gpioOut  =  value; break;
            case GPIO_OUT_SET: _gpioOut |=  value; break;
            case GPIO_OUT_CLR: _gpioOut &= ~value; break;
            case GPIO_OUT_XOR: _gpioOut ^=  value; break;

            case GPIO_OE:      _gpioOe  =  value; break;
            case GPIO_OE_SET:  _gpioOe |=  value; break;
            case GPIO_OE_CLR:  _gpioOe &= ~value; break;
            case GPIO_OE_XOR:  _gpioOe ^=  value; break;

            case DIV_UDIVIDEND:
                _divUdividend = value;
                break;

            case DIV_UDIVISOR:
                _divUdivisor = value;
                _divSigned   = false;
                PerformDivide();
                break;

            case DIV_SDIVIDEND:
                _divSdividend = (int)value;
                break;

            case DIV_SDIVISOR:
                _divSdivisor = (int)value;
                _divSigned   = true;
                PerformDivide();
                break;

            case DIV_QUOTIENT:
                _divQuotient = value;
                _divCsr |= 1;   // DIRTY
                break;

            case DIV_REMAINDER:
                _divRemainder = value;
                _divCsr |= 1;   // DIRTY
                break;
        }
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

    // ── Private helpers ──────────────────────────────────────────────

    private void PerformDivide()
    {
        // Hardware divider takes 8 cycles
        _cpu.Cycles += 8;
        _divCsr = 0x2;   // READY, not DIRTY

        if (_divSigned)
        {
            if (_divSdivisor == 0)
            {
                // Division by zero: quotient = ±1, remainder = dividend
                _divQuotient  = _divSdividend >= 0 ? 1u : 0xFFFFFFFF;
                _divRemainder = (uint)_divSdividend;
            }
            else
            {
                _divQuotient  = (uint)(_divSdividend / _divSdivisor);
                _divRemainder = (uint)(_divSdividend % _divSdivisor);
            }
        }
        else
        {
            if (_divUdivisor == 0)
            {
                _divQuotient  = 0xFFFFFFFF;
                _divRemainder = _divUdividend;
            }
            else
            {
                _divQuotient  = _divUdividend / _divUdivisor;
                _divRemainder = _divUdividend % _divUdivisor;
            }
        }
    }

    private uint ReadSpinlock(int index)
    {
        var bit = 1u << index;
        if ((_spinLocks & bit) != 0)
            return 0;   // already taken

        _spinLocks |= bit;
        return bit;   // return bitmask of the acquired lock
    }
}
