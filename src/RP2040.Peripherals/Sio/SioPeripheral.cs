using System.Runtime.CompilerServices;
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
    // ── CPUID ────────────────────────────────────────────────────────────
    private const uint CPUID = 0x000;  // 0 = Core0, 1 = Core1

    // ── GPIO (offsets from SIO base) ─────────────────────────────────
    private const uint GPIO_IN      = 0x004;
    private const uint GPIO_HI_IN   = 0x008;  // QSPI GPIO input
    private const uint GPIO_OUT     = 0x010;
    private const uint GPIO_OUT_SET = 0x014;
    private const uint GPIO_OUT_CLR = 0x018;
    private const uint GPIO_OUT_XOR = 0x01C;
    private const uint GPIO_OE      = 0x020;
    private const uint GPIO_OE_SET  = 0x024;
    private const uint GPIO_OE_CLR  = 0x028;
    private const uint GPIO_OE_XOR  = 0x02C;
    private const uint GPIO_HI_OUT      = 0x030;  // QSPI output
    private const uint GPIO_HI_OUT_SET  = 0x034;
    private const uint GPIO_HI_OUT_CLR  = 0x038;
    private const uint GPIO_HI_OUT_XOR  = 0x03C;
    private const uint GPIO_HI_OE       = 0x040;
    private const uint GPIO_HI_OE_SET   = 0x044;
    private const uint GPIO_HI_OE_CLR   = 0x048;
    private const uint GPIO_HI_OE_XOR   = 0x04C;

    // ── Multicore FIFO ────────────────────────────────────────────────
    private const uint FIFO_ST  = 0x050;   // FIFO status
    private const uint FIFO_WR  = 0x054;   // write to TX FIFO (to other core)
    private const uint FIFO_RD  = 0x058;   // read from RX FIFO (from other core)

    // ── Spinlock status ───────────────────────────────────────────────
    private const uint SPINLOCK_ST = 0x05C;  // bitmask of claimed spinlocks

    // ── Hardware divider ─────────────────────────────────────────────
    private const uint DIV_UDIVIDEND = 0x060;
    private const uint DIV_UDIVISOR  = 0x064;
    private const uint DIV_SDIVIDEND = 0x068;
    private const uint DIV_SDIVISOR  = 0x06C;
    private const uint DIV_QUOTIENT  = 0x070;
    private const uint DIV_REMAINDER = 0x074;
    private const uint DIV_CSR       = 0x078;  // bit0=DIRTY, bit1=READY

    // ── Interpolators (INTERP0: 0x080, INTERP1: 0x0C0, stride 0x40) ───
    private const uint INTERP0_BASE = 0x080;
    private const uint INTERP1_BASE = 0x0C0;

    // Per-interp offsets
    private const uint INTERP_ACCUM0     = 0x00;
    private const uint INTERP_ACCUM1     = 0x04;
    private const uint INTERP_BASE0      = 0x08;
    private const uint INTERP_BASE1      = 0x0C;
    private const uint INTERP_BASE2      = 0x10;
    private const uint INTERP_POP_LANE0  = 0x14;  // read+update
    private const uint INTERP_POP_LANE1  = 0x18;
    private const uint INTERP_POP_FULL   = 0x1C;
    private const uint INTERP_PEEK_LANE0 = 0x20;  // read-only
    private const uint INTERP_PEEK_LANE1 = 0x24;
    private const uint INTERP_PEEK_FULL  = 0x28;
    private const uint INTERP_CTRL_LANE0 = 0x2C;
    private const uint INTERP_CTRL_LANE1 = 0x30;
    private const uint INTERP_ACCUM0_ADD = 0x34;
    private const uint INTERP_ACCUM1_ADD = 0x38;
    private const uint INTERP_BASE_1AND0 = 0x3C;

    // ── Spinlocks ────────────────────────────────────────────────────
    private const uint SPINLOCK_BASE = 0x100;
    private const uint SPINLOCK_END  = 0x17C;

    private readonly CortexM0Plus _cpu;

    // GPIO state
    private uint _gpioOut;
    private uint _gpioOe;
    private uint _gpioIn;
    private uint _gpioHiOut;
    private uint _gpioHiOe;

    // Divider state
    private uint _divUdividend, _divUdivisor;
    private int  _divSdividend, _divSdivisor;
    private uint _divQuotient, _divRemainder;
    private uint _divCsr;
    private bool _divSigned;

    // Spinlocks
    private uint _spinLocks;

    // Multicore FIFO (single-core sim: TX drains silently, RX always empty)
    // FIFO_ST bits: VLD[0]=RX not empty, RDY[1]=TX not full, WOF[2], ROE[3]
    private const uint FIFO_ST_RDY = 1u << 1;   // TX always has space

    // Interpolators
    private InterpState _interp0;
    private InterpState _interp1;

    public uint Size => 0x10000;  // wide enough to cover spinlocks at 0x100–0x17C

    /// <summary>Optionally feed current GPIO input state from IO_BANK0.</summary>
    public uint GpioIn
    {
        get => _gpioIn;
        set => _gpioIn = value;
    }

    public uint GpioOe  => _gpioOe;
    public uint GpioOut => _gpioOut;

    public SioPeripheral(CortexM0Plus cpu)
    {
        _cpu = cpu;
    }

    // ── IMemoryMappedDevice — reads ──────────────────────────────────

    public uint ReadWord(uint address)
    {
        // Spinlocks 0–31
        if (address >= SPINLOCK_BASE && address <= SPINLOCK_END)
            return ReadSpinlock((int)((address - SPINLOCK_BASE) >> 2));

        // Interpolator 0
        if (address >= INTERP0_BASE && address < INTERP0_BASE + 0x40)
            return ReadInterp(ref _interp0, address - INTERP0_BASE);

        // Interpolator 1
        if (address >= INTERP1_BASE && address < INTERP1_BASE + 0x40)
            return ReadInterp(ref _interp1, address - INTERP1_BASE);

        return address switch
        {
            CPUID        => 0,           // always Core0 in single-core simulation
            GPIO_IN      => _gpioIn,
            GPIO_HI_IN   => 0,
            GPIO_OUT     => _gpioOut,
            GPIO_HI_OUT  => _gpioHiOut,
            GPIO_OE      => _gpioOe,
            GPIO_HI_OE   => _gpioHiOe,
            DIV_UDIVIDEND => _divUdividend,
            DIV_UDIVISOR  => _divUdivisor,
            DIV_SDIVIDEND => (uint)_divSdividend,
            DIV_SDIVISOR  => (uint)_divSdivisor,
            DIV_QUOTIENT  => _divQuotient,
            DIV_REMAINDER => _divRemainder,
            DIV_CSR       => _divCsr,
            FIFO_ST       => FIFO_ST_RDY,  // TX always ready; RX always empty
            FIFO_RD       => 0,            // RX empty (Core1 doesn't exist)
            SPINLOCK_ST   => _spinLocks,
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
        // Spinlocks — any write releases
        if (address >= SPINLOCK_BASE && address <= SPINLOCK_END)
        {
            _spinLocks &= ~(1u << (int)((address - SPINLOCK_BASE) >> 2));
            return;
        }

        // Interpolator 0
        if (address >= INTERP0_BASE && address < INTERP0_BASE + 0x40)
        {
            WriteInterp(ref _interp0, address - INTERP0_BASE, value);
            return;
        }

        // Interpolator 1
        if (address >= INTERP1_BASE && address < INTERP1_BASE + 0x40)
        {
            WriteInterp(ref _interp1, address - INTERP1_BASE, value);
            return;
        }

        switch (address)
        {
            case GPIO_OUT:         _gpioOut  =  value; break;
            case GPIO_OUT_SET:     _gpioOut |=  value; break;
            case GPIO_OUT_CLR:     _gpioOut &= ~value; break;
            case GPIO_OUT_XOR:     _gpioOut ^=  value; break;
            case GPIO_OE:          _gpioOe  =  value; break;
            case GPIO_OE_SET:      _gpioOe |=  value; break;
            case GPIO_OE_CLR:      _gpioOe &= ~value; break;
            case GPIO_OE_XOR:      _gpioOe ^=  value; break;
            case GPIO_HI_OUT:      _gpioHiOut  =  value; break;
            case GPIO_HI_OUT_SET:  _gpioHiOut |=  value; break;
            case GPIO_HI_OUT_CLR:  _gpioHiOut &= ~value; break;
            case GPIO_HI_OUT_XOR:  _gpioHiOut ^=  value; break;
            case GPIO_HI_OE:       _gpioHiOe  =  value; break;
            case GPIO_HI_OE_SET:   _gpioHiOe |=  value; break;
            case GPIO_HI_OE_CLR:   _gpioHiOe &= ~value; break;
            case GPIO_HI_OE_XOR:   _gpioHiOe ^=  value; break;

            case FIFO_WR:
                // TX to Core1: silently drop (Core1 doesn't exist in simulation)
                break;

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
                _divCsr |= 1;
                break;
            case DIV_REMAINDER:
                _divRemainder = value;
                _divCsr |= 1;
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

    // ── Interpolator implementation ──────────────────────────────────

    private static uint ReadInterp(ref InterpState st, uint offset)
    {
        return offset switch
        {
            INTERP_ACCUM0     => st.Accum0,
            INTERP_ACCUM1     => st.Accum1,
            INTERP_BASE0      => st.Base0,
            INTERP_BASE1      => st.Base1,
            INTERP_BASE2      => st.Base2,
            INTERP_CTRL_LANE0 => st.Ctrl0,
            INTERP_CTRL_LANE1 => st.Ctrl1,
            INTERP_PEEK_LANE0 => ComputeLane(ref st, 0),
            INTERP_PEEK_LANE1 => ComputeLane(ref st, 1),
            INTERP_PEEK_FULL  => ComputeFull(ref st),
            INTERP_POP_LANE0  => PopLane(ref st, 0),
            INTERP_POP_LANE1  => PopLane(ref st, 1),
            INTERP_POP_FULL   => PopFull(ref st),
            INTERP_ACCUM0_ADD => st.Accum0,
            INTERP_ACCUM1_ADD => st.Accum1,
            _                 => 0,
        };
    }

    private static void WriteInterp(ref InterpState st, uint offset, uint value)
    {
        switch (offset)
        {
            case INTERP_ACCUM0:     st.Accum0 = value; break;
            case INTERP_ACCUM1:     st.Accum1 = value; break;
            case INTERP_BASE0:      st.Base0  = value; break;
            case INTERP_BASE1:      st.Base1  = value; break;
            case INTERP_BASE2:      st.Base2  = value; break;
            case INTERP_CTRL_LANE0: st.Ctrl0  = value; break;
            case INTERP_CTRL_LANE1: st.Ctrl1  = value; break;
            case INTERP_ACCUM0_ADD: st.Accum0 += value; break;
            case INTERP_ACCUM1_ADD: st.Accum1 += value; break;
            case INTERP_BASE_1AND0:
                // sets BASE0 and BASE1 from a combined 32-bit write:
                // BASE0 = bits [15:0], BASE1 = bits [31:16]
                st.Base0 = (ushort)value;
                st.Base1 = value >> 16;
                break;
        }
    }

    /// <summary>
    /// Compute the result for one lane.
    /// RP2040 TRM §2.3.1.3: masked-and-shifted accumulator OR'd with base.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeLane(ref InterpState st, int lane)
    {
        var ctrl = lane == 0 ? st.Ctrl0 : st.Ctrl1;
        var shift    = (int)(ctrl & 0x1F);
        var maskLsb  = (int)((ctrl >> 5) & 0x1F);
        var maskMsb  = (int)((ctrl >> 10) & 0x1F);
        var signed   = (ctrl & (1u << 15)) != 0;
        var crossIn  = (ctrl & (1u << 16)) != 0;
        // CROSS_RESULT: lane 0 reads lane 1's result — handled at full-result level

        uint accum = crossIn
            ? (lane == 0 ? st.Accum1 : st.Accum0)
            : (lane == 0 ? st.Accum0 : st.Accum1);

        uint shifted = signed
            ? (uint)((int)accum >> shift)
            : accum >> shift;

        // Build mask covering [maskMsb:maskLsb]
        uint mask = BuildMask(maskLsb, maskMsb);
        uint masked = shifted & mask;

        // Sign-extend at maskMsb if SIGNED
        if (signed && maskMsb < 31)
        {
            uint signBit = 1u << maskMsb;
            if ((masked & signBit) != 0)
                masked |= ~mask;   // extend sign
        }

        var baseVal = lane == 0 ? st.Base0 : st.Base1;
        // Base replaces the unmasked bits (bits NOT in mask are set to base's bits)
        return (masked & mask) | (baseVal & ~mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeFull(ref InterpState st)
    {
        var crossResult0 = (st.Ctrl0 & (1u << 17)) != 0;
        var crossResult1 = (st.Ctrl1 & (1u << 17)) != 0;

        uint r0 = ComputeLane(ref st, 0);
        uint r1 = ComputeLane(ref st, 1);

        // CROSS_RESULT: each lane uses the other lane's primary result
        uint l0 = crossResult0 ? r1 : r0;
        return l0 + st.Base2;
    }

    private static uint PopLane(ref InterpState st, int lane)
    {
        uint result = ComputeLane(ref st, lane);
        // POP: update accum[lane] = full result (lane0) or lane1 result
        // RP2040 TRM: after POP, ACCUM feeds the full result back
        if (lane == 0) st.Accum0 = ComputeFull(ref st);
        else            st.Accum1 = result;
        return result;
    }

    private static uint PopFull(ref InterpState st)
    {
        uint result = ComputeFull(ref st);
        st.Accum0 = result;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BuildMask(int lsb, int msb)
    {
        if (msb < lsb) return 0;
        int bits = msb - lsb + 1;
        uint mask = bits >= 32 ? 0xFFFFFFFF : (1u << bits) - 1;
        return mask << lsb;
    }

    // ── Divider ──────────────────────────────────────────────────────

    private void PerformDivide()
    {
        _cpu.Cycles += 8;
        _divCsr = 0x2;  // READY, not DIRTY

        if (_divSigned)
        {
            if (_divSdivisor == 0)
            {
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

    // ── Spinlocks ─────────────────────────────────────────────────────

    private uint ReadSpinlock(int index)
    {
        var bit = 1u << index;
        if ((_spinLocks & bit) != 0)
            return 0;   // already taken

        _spinLocks |= bit;
        return bit;
    }
}

// ── Interpolator state ────────────────────────────────────────────────────────
internal struct InterpState
{
    public uint Accum0, Accum1;
    public uint Base0, Base1, Base2;
    public uint Ctrl0, Ctrl1;
}
