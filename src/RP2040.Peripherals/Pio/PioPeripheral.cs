using RP2040.Core.Cpu;
using RP2040.Core.Memory;

namespace RP2040.Peripherals.Pio;

/// <summary>
/// RP2040 PIO block.
/// PIO0 base: 0x50200000, PIO1 base: 0x50300000.
/// 4 state machines. 32 × 16-bit instruction words.
/// Implements ITickable; tick at system clock granularity.
/// </summary>
public sealed class PioPeripheral : IMemoryMappedDevice, ITickable
{
    private const int SM_COUNT    = 4;
    private const int INSTR_COUNT = 32;

    // ── Register offsets ─────────────────────────────────────────────
    private const uint REG_CTRL            = 0x000;
    private const uint REG_FSTAT           = 0x004;
    private const uint REG_FDEBUG          = 0x008;
    private const uint REG_FLEVEL          = 0x00C;
    private const uint REG_TXF_BASE        = 0x010;   // TXF0-TXF3, 4 bytes each
    private const uint REG_RXF_BASE        = 0x020;   // RXF0-RXF3, 4 bytes each
    private const uint REG_IRQ             = 0x030;
    private const uint REG_IRQ_FORCE       = 0x034;
    private const uint REG_INPUT_SYNC      = 0x038;
    private const uint REG_DBG_PADOUT      = 0x03C;
    private const uint REG_DBG_PADOE       = 0x040;
    private const uint REG_DBG_CFGINFO     = 0x044;
    private const uint REG_INSTR_MEM_BASE  = 0x048;   // 32 entries × 4 bytes = 0x048..0x0C4
    private const uint REG_SM_BASE         = 0x0C8;   // SM0 starts here, each SM = 6 regs × 4 = 0x18
    private const uint REG_INTR            = 0x128;
    private const uint REG_IRQ0_INTE       = 0x12C;
    private const uint REG_IRQ0_INTF       = 0x130;
    private const uint REG_IRQ0_INTS       = 0x134;
    private const uint REG_IRQ1_INTE       = 0x138;
    private const uint REG_IRQ1_INTF       = 0x13C;
    private const uint REG_IRQ1_INTS       = 0x140;

    private const uint SM_STRIDE = 0x18;  // 6 registers × 4 bytes
    private const uint SM_OFF_CLKDIV    = 0x00;
    private const uint SM_OFF_EXECCTRL  = 0x04;
    private const uint SM_OFF_SHIFTCTRL = 0x08;
    private const uint SM_OFF_ADDR      = 0x0C;
    private const uint SM_OFF_INSTR     = 0x10;
    private const uint SM_OFF_PINCTRL   = 0x14;

    // PIO instruction opcodes (bits [15:13])
    private const int OP_JMP  = 0;
    private const int OP_WAIT = 1;
    private const int OP_IN   = 2;
    private const int OP_OUT  = 3;
    private const int OP_PUSH_PULL = 4;
    private const int OP_MOV  = 5;
    private const int OP_IRQ  = 6;
    private const int OP_SET  = 7;

    private readonly CortexM0Plus _cpu;
    private readonly uint _blockIndex;  // 0=PIO0, 1=PIO1 (for IRQ routing)

    private readonly ushort[] _instrMem = new ushort[INSTR_COUNT];
    private readonly PioStateMachine[] _sm;

    private uint _irq;       // 8-bit IRQ flags
    private uint _fdebug;    // TXOVER/RXUNDER/TXSTALL/RXSTALL per SM
    private uint _irq0Inte;
    private uint _irq0Intf;
    private uint _irq1Inte;
    private uint _irq1Intf;

    public uint Size => 0x100000;  // up to 1 MB address space per block

    /// <summary>Read current physical GPIO input levels (used by WAIT GPIO, IN PINS).</summary>
    public Func<uint>? ReadGpioIn { get; set; }
    /// <summary>Write physical GPIO output pins: (pinValue, pinMask).</summary>
    public Action<uint, uint>? WriteGpioPins { get; set; }
    /// <summary>Write physical GPIO pin directions: (dirValue, pinMask).</summary>
    public Action<uint, uint>? WriteGpioDirs { get; set; }

    public PioPeripheral(CortexM0Plus cpu, uint blockIndex)
    {
        _cpu = cpu;
        _blockIndex = blockIndex;
        _sm = new PioStateMachine[SM_COUNT];
        for (var i = 0; i < SM_COUNT; i++)
        {
            _sm[i] = new PioStateMachine();
            // Default wrap: top=31, bottom=0
            _sm[i].ExecCtrl = (31u << 12);
        }
    }

    // ── ITickable ────────────────────────────────────────────────────

    public void Tick(long deltaCycles)
    {
        for (var s = 0; s < SM_COUNT; s++)
        {
            var sm = _sm[s];
            if (!sm.Enabled) continue;

            // Clock divisor: bits[31:16]=integer (0=65536), bits[15:8]=frac
            var divInt  = (int)((sm.ClkDiv >> 16) & 0xFFFF);
            var divFrac = (int)((sm.ClkDiv >> 8) & 0xFF);
            if (divInt == 0) divInt = 65536;

            var divisor = divInt * 256 + divFrac;
            sm.FracAccum += deltaCycles * 256;
            var steps = sm.FracAccum / divisor;
            sm.FracAccum %= divisor;

            for (var i = 0L; i < steps; i++)
                ExecuteStep(sm, s);
        }
        CheckInterrupts();
    }

    // ── IMemoryMappedDevice ──────────────────────────────────────────

    public uint ReadWord(uint address)
    {
        // Strip the base (top 20 bits may vary between PIO0/1)
        var off = address & 0xFFFFF;

        if (off >= REG_INSTR_MEM_BASE && off < REG_INSTR_MEM_BASE + INSTR_COUNT * 4)
            return _instrMem[(off - REG_INSTR_MEM_BASE) / 4];

        if (off >= REG_SM_BASE && off < REG_SM_BASE + SM_COUNT * SM_STRIDE)
            return ReadSmReg(off);

        if (off >= REG_TXF_BASE && off < REG_TXF_BASE + SM_COUNT * 4)
            return 0;  // TXF is write-only

        if (off >= REG_RXF_BASE && off < REG_RXF_BASE + SM_COUNT * 4)
        {
            var smIdx = (int)((off - REG_RXF_BASE) / 4);
            return _sm[smIdx].RxFifo.TryDequeue(out var v) ? v : 0;
        }

        return off switch
        {
            REG_CTRL        => BuildCtrl(),
            REG_FSTAT       => BuildFstat(),
            REG_FDEBUG      => _fdebug,
            REG_FLEVEL      => BuildFlevel(),
            REG_IRQ         => _irq,
            REG_IRQ_FORCE   => 0,
            REG_DBG_CFGINFO => (SM_COUNT << 16) | (INSTR_COUNT << 8) | 2u,
            REG_INTR        => BuildIntr(),
            REG_IRQ0_INTE   => _irq0Inte,
            REG_IRQ0_INTF   => _irq0Intf,
            REG_IRQ0_INTS   => (BuildIntr() | _irq0Intf) & _irq0Inte,
            REG_IRQ1_INTE   => _irq1Inte,
            REG_IRQ1_INTF   => _irq1Intf,
            REG_IRQ1_INTS   => (BuildIntr() | _irq1Intf) & _irq1Inte,
            _ => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        var off = address & 0xFFFFF;

        if (off >= REG_INSTR_MEM_BASE && off < REG_INSTR_MEM_BASE + INSTR_COUNT * 4)
        {
            _instrMem[(off - REG_INSTR_MEM_BASE) / 4] = (ushort)value;
            return;
        }

        if (off >= REG_SM_BASE && off < REG_SM_BASE + SM_COUNT * SM_STRIDE)
        {
            WriteSmReg(off, value);
            return;
        }

        if (off >= REG_TXF_BASE && off < REG_TXF_BASE + SM_COUNT * 4)
        {
            var smIdx = (int)((off - REG_TXF_BASE) / 4);
            var sm = _sm[smIdx];
            if (sm.TxFifo.Count < sm.TxDepth)
                sm.TxFifo.Enqueue(value);
            else
                _fdebug |= 1u << (8 + smIdx);  // TXOVER bits [11:8]
            return;
        }

        switch (off)
        {
            case REG_CTRL:      WriteCtrl(value); break;
            case REG_FDEBUG:    _fdebug &= ~value; break;  // write 1 to clear
            case REG_IRQ:       _irq &= ~value; break;       // write 1 to clear
            case REG_IRQ_FORCE: _irq |= value & 0xFF; break;
            case REG_IRQ0_INTE: _irq0Inte = value & 0xFFF; break;
            case REG_IRQ0_INTF: _irq0Intf = value & 0xFFF; break;
            case REG_IRQ1_INTE: _irq1Inte = value & 0xFFF; break;
            case REG_IRQ1_INTF: _irq1Intf = value & 0xFFF; break;
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

    // ── Public helpers ───────────────────────────────────────────────

    /// <summary>Read the current output pins for state machine <paramref name="smIndex"/>.</summary>
    public uint GetPins(int smIndex) => _sm[smIndex].GpioPins;

    /// <summary>Returns true if RX FIFO of <paramref name="smIndex"/> has data.</summary>
    public bool RxFifoEmpty(int smIndex) => _sm[smIndex].RxFifo.Count == 0;
    /// <summary>DREQ source for DMA TX: true when TX FIFO has space to accept data.</summary>
    public bool TxFifoNotFull(int smIndex) => _sm[smIndex].TxFifo.Count < _sm[smIndex].TxDepth;

    /// <summary>Inject a value directly into the RX FIFO of <paramref name="smIndex"/>.</summary>
    public void InjectRxData(int smIndex, uint value)
    {
        var sm = _sm[smIndex];
        if (sm.RxFifo.Count < sm.RxDepth)
            sm.RxFifo.Enqueue(value);
    }

    // ── Private: Ctrl ────────────────────────────────────────────────

    private uint BuildCtrl()
    {
        uint ctrl = 0;
        for (var i = 0; i < SM_COUNT; i++)
            if (_sm[i].Enabled)
                ctrl |= 1u << i;
        return ctrl;
    }

    private void WriteCtrl(uint value)
    {
        // Bits [3:0]: SM_ENABLE
        for (var i = 0; i < SM_COUNT; i++)
            _sm[i].Enabled = (value & (1u << i)) != 0;

        // Bits [7:4]: SM_RESTART — reset PC and shift state
        for (var i = 0; i < SM_COUNT; i++)
            if ((value & (1u << (4 + i))) != 0)
            {
                _sm[i].PC = _sm[i].WrapBottom;
                _sm[i].ISR = 0; _sm[i].IsrCount = 0;
                _sm[i].OSR = 0; _sm[i].OsrCount = 0;
                _sm[i].Stalled = false;
            }

        // Bits [11:8]: CLKDIV_RESTART — reset fractional accumulator
        for (var i = 0; i < SM_COUNT; i++)
            if ((value & (1u << (8 + i))) != 0)
                _sm[i].FracAccum = 0;
    }

    // ── Private: FSTAT ───────────────────────────────────────────────

    private uint BuildFstat()
    {
        // Bit layout from datasheet: [3:0]=TXFULL, [11:8]=TXEMPTY, [19:16]=RXFULL, [27:24]=RXEMPTY
        uint result = 0;
        for (var i = 0; i < SM_COUNT; i++)
        {
            var sm = _sm[i];
            if (sm.TxFifo.Count >= sm.TxDepth) result |= 1u << i;         // TX full [3:0]
            if (sm.TxFifo.Count == 0)           result |= 1u << (8  + i);  // TX empty [11:8]
            if (sm.RxFifo.Count >= sm.RxDepth) result |= 1u << (16 + i);  // RX full [19:16]
            if (sm.RxFifo.Count == 0)           result |= 1u << (24 + i);  // RX empty [27:24]
        }
        return result;
    }

    // ── Private: FLEVEL ──────────────────────────────────────────────

    private uint BuildFlevel()
    {
        uint result = 0;
        for (var i = 0; i < SM_COUNT; i++)
        {
            var txLevel = (uint)(_sm[i].TxFifo.Count & 0xF);
            var rxLevel = (uint)(_sm[i].RxFifo.Count & 0xF);
            result |= (txLevel | (rxLevel << 4)) << (i * 8);
        }
        return result;
    }

    // ── Private: INTR (dynamic) ──────────────────────────────────────
    // Bits [3:0]=RX not empty per SM, [7:4]=TX not full per SM, [11:8]=IRQ flags 0-3

    private uint BuildIntr()
    {
        uint intr = _irq & 0xF;  // IRQ flags 0-3 in bits [11:8] form, shifted to [11:8]
        intr <<= 8;
        for (var i = 0; i < SM_COUNT; i++)
        {
            var sm = _sm[i];
            if (sm.RxFifo.Count > 0)                  intr |= 1u << i;        // RX not empty [3:0]
            if (sm.TxFifo.Count < sm.TxDepth)          intr |= 1u << (4 + i);  // TX not full [7:4]
        }
        return intr;
    }

    // ── Private: interrupt routing to NVIC ──────────────────────────
    // PIO0_IRQ0=7, PIO0_IRQ1=8, PIO1_IRQ0=9, PIO1_IRQ1=10

    private void CheckInterrupts()
    {
        var intr = BuildIntr();
        var irq0Active = ((intr | _irq0Intf) & _irq0Inte) != 0;
        var irq1Active = ((intr | _irq1Intf) & _irq1Inte) != 0;
        _cpu.SetInterrupt((int)(7 + _blockIndex * 2),     irq0Active);
        _cpu.SetInterrupt((int)(7 + _blockIndex * 2 + 1), irq1Active);
    }

    // ── Private: SM register read/write ─────────────────────────────

    private uint ReadSmReg(uint off)
    {
        var smIdx = (int)((off - REG_SM_BASE) / SM_STRIDE);
        var reg   = (off - REG_SM_BASE) % SM_STRIDE;
        var sm    = _sm[smIdx];
        return reg switch
        {
            SM_OFF_CLKDIV    => sm.ClkDiv,
            SM_OFF_EXECCTRL  => sm.ExecCtrl,
            SM_OFF_SHIFTCTRL => sm.ShiftCtrl,
            SM_OFF_ADDR      => sm.PC,
            SM_OFF_INSTR     => _instrMem[sm.PC & 0x1F],
            SM_OFF_PINCTRL   => sm.PinCtrl,
            _ => 0,
        };
    }

    private void WriteSmReg(uint off, uint value)
    {
        var smIdx = (int)((off - REG_SM_BASE) / SM_STRIDE);
        var reg   = (off - REG_SM_BASE) % SM_STRIDE;
        var sm    = _sm[smIdx];
        switch (reg)
        {
            case SM_OFF_CLKDIV:    sm.ClkDiv    = value; break;
            case SM_OFF_EXECCTRL:  sm.ExecCtrl  = value; break;
            case SM_OFF_SHIFTCTRL: sm.ShiftCtrl = value; break;
            case SM_OFF_ADDR:      break; // read-only
            case SM_OFF_INSTR:     sm.ForcedInstr = (ushort)value; break;  // immediate execute
            case SM_OFF_PINCTRL:   sm.PinCtrl   = value; break;
        }
    }

    // ── Private: instruction execution ──────────────────────────────

    private void ExecuteStep(PioStateMachine sm, int smIdx)
    {
        // Burn delay cycles
        if (sm.DelayCounter > 0)
        {
            sm.DelayCounter--;
            return;
        }

        ushort instr;
        if (sm.ForcedInstr.HasValue)
        {
            instr = (ushort)sm.ForcedInstr.Value;
            sm.ForcedInstr = null;
        }
        else
        {
            if (sm.PC >= INSTR_COUNT) sm.PC = (uint)(sm.WrapBottom & 0x1F);
            instr = _instrMem[sm.PC];
        }

        var opcode = (instr >> 13) & 0x7;

        sm.PcJumped = false;

        // Apply sideset from bits[12:8] BEFORE execution (as hardware does)
        ApplySideset(sm, instr);

        ExecuteInstr(sm, instr, opcode);

        // Update FDEBUG stall bits (sticky — cleared by writing 1 to FDEBUG)
        if (sm.Stalled && opcode == OP_PUSH_PULL)
        {
            if ((instr & 0x80) != 0)
                _fdebug |= 1u << smIdx;          // TXSTALL bits [3:0]
            else
                _fdebug |= 1u << (24 + smIdx);   // RXSTALL bits [27:24]
        }

        // Compute delay after execution (delay only counted on non-stall)
        if (!sm.Stalled)
        {
            var sidesetCount = (int)sm.SidesetCount + (sm.SideEn != 0 ? 1 : 0);
            var delayBits = 5 - sidesetCount;
            if (delayBits > 0)
            {
                var delay = (int)((instr >> 8) & ((1 << delayBits) - 1));
                sm.DelayCounter = delay;
            }

            if (!sm.PcJumped)
            {
                sm.PC++;
                if (sm.PC > sm.WrapTop)
                    sm.PC = sm.WrapBottom;
            }
        }
    }

    // Apply sideset bits from instruction field [12:8]
    private static void ApplySideset(PioStateMachine sm, ushort instr)
    {
        var sidesetCount = (int)sm.SidesetCount;
        if (sidesetCount == 0) return;

        var field = (instr >> 8) & 0x1F;  // 5 bits: delay+sideset

        // If sideEn bit is set in EXECCTRL, MSB of the field is the enable
        var sideEn = sm.SideEn != 0;
        int sideValue;
        if (sideEn)
        {
            if ((field & (1 << (sidesetCount - 1 + 1))) == 0) return;  // enable bit=0 → no sideset
            sideValue = (field >> (5 - sidesetCount)) & ((1 << (sidesetCount - 1)) - 1);
        }
        else
        {
            sideValue = (field >> (5 - sidesetCount)) & ((1 << sidesetCount) - 1);
        }

        var sideBase  = (int)sm.SidesetBase;
        var sidePinDir = sm.SidePinDir != 0;

        for (var bit = 0; bit < sidesetCount; bit++)
        {
            var pin = (sideBase + bit) & 0x1F;
            var v = (sideValue >> bit) & 1;
            if (sidePinDir)
                sm.GpioPinDirs = (sm.GpioPinDirs & ~(1u << pin)) | ((uint)v << pin);
            else
                sm.GpioPins = (sm.GpioPins & ~(1u << pin)) | ((uint)v << pin);
        }
    }

    private void ExecuteInstr(PioStateMachine sm, ushort instr, int opcode)
    {
        switch (opcode)
        {
            case OP_JMP:      ExecJmp(sm, instr);  break;
            case OP_WAIT:     ExecWait(sm, instr); break;
            case OP_IN:       ExecIn(sm, instr);   break;
            case OP_OUT:      ExecOut(sm, instr);  break;
            case OP_PUSH_PULL:
                if ((instr & 0x80) == 0) ExecPush(sm, instr);
                else                     ExecPull(sm, instr);
                break;
            case OP_MOV:      ExecMov(sm, instr);  break;
            case OP_IRQ:      ExecIrq(sm, instr);  break;
            case OP_SET:      ExecSet(sm, instr);  break;
        }
    }

    // JMP: bits [7:5]=condition, [4:0]=target
    private void ExecJmp(PioStateMachine sm, ushort instr)
    {
        var cond   = (instr >> 5) & 0x7;
        var target = (uint)(instr & 0x1F);
        bool taken = cond switch
        {
            0 => true,                              // always
            1 => sm.X == 0,                         // !X
            2 => sm.X-- != 0,                       // X-- (post-dec, take if was non-zero)
            3 => sm.Y == 0,                         // !Y
            4 => sm.Y-- != 0,                       // Y--
            5 => sm.X != sm.Y,                      // X!=Y
            6 => (sm.GpioPins & (1u << (int)sm.JmpPin)) != 0,  // PIN
            7 => sm.OsrCount < 32,                  // !OSRE
            _ => false,
        };
        if (taken)
        {
            sm.PC = target;
            sm.PcJumped = true;
            sm.Stalled = false;
            return;
        }
        sm.Stalled = false;
    }

    // WAIT: bits [7]=polarity, [6:5]=source, [4:0]=index
    private void ExecWait(PioStateMachine sm, ushort instr)
    {
        var polarity = (instr >> 7) & 1;
        var source   = (instr >> 5) & 3;
        var index    = (uint)(instr & 0x1F);

        bool condition = source switch
        {
            0 => (((ReadGpioIn?.Invoke() ?? sm.GpioPins) >> (int)index) & 1) == polarity,  // GPIO (absolute)
            1 => (((ReadGpioIn?.Invoke() ?? sm.GpioPins) >> (int)((index + sm.InBase) & 0x1F)) & 1) == polarity,  // PIN relative to IN_BASE
            2 => ((_irq >> (int)(index & 7)) & 1) == polarity,   // IRQ flag
            _ => true,
        };

        sm.Stalled = !condition;
        if (condition && source == 2 && polarity == 1)
            _irq &= ~(1u << (int)(index & 7));  // clear IRQ on successful WAIT IRQ
    }

    // IN: bits [7:5]=source, [4:0]=bit count (0=32)
    private void ExecIn(PioStateMachine sm, ushort instr)
    {
        var source   = (instr >> 5) & 0x7;
        var bitCount = (int)(instr & 0x1F);
        if (bitCount == 0) bitCount = 32;

        uint data = source switch
        {
            0 => (ReadGpioIn?.Invoke() ?? sm.GpioPins) >> (int)sm.InBase,  // PINS: read from InBase
            1 => sm.X,
            2 => sm.Y,
            3 => 0,             // NULL
            6 => sm.ISR,
            7 => sm.OSR,
            _ => 0,
        };

        if (sm.IsrShiftRight)
        {
            sm.ISR = (sm.ISR >> bitCount) | (data << (32 - bitCount));
        }
        else
        {
            sm.ISR = (sm.ISR << bitCount) | (data & ((1u << bitCount) - 1));
        }
        sm.IsrCount += (uint)bitCount;

        if (sm.AutopushEnabled && sm.IsrCount >= (uint)sm.AutopushThreshold)
            DoPush(sm, false);
    }

    // OUT: bits [7:5]=destination, [4:0]=bit count (0=32)
    private void ExecOut(PioStateMachine sm, ushort instr)
    {
        var dest     = (instr >> 5) & 0x7;
        var bitCount = (int)(instr & 0x1F);
        if (bitCount == 0) bitCount = 32;

        uint data;
        if (sm.OsrShiftRight)
        {
            data = sm.OSR & ((1u << bitCount) - 1);
            sm.OSR >>= bitCount;
        }
        else
        {
            data = sm.OSR >> (32 - bitCount);
            sm.OSR <<= bitCount;
        }
        unchecked { sm.OsrCount -= (uint)bitCount; }

        switch (dest)
        {
            case 0: {  // PINS: write bitCount pins at OutBase
                var outBase = (int)sm.OutBase;
                var pinMask = bitCount < 32 ? ((1u << bitCount) - 1) << outBase : 0xFFFFFFFFu;
                var pinValue = (data & (bitCount < 32 ? (1u << bitCount) - 1 : 0xFFFFFFFFu)) << outBase;
                sm.GpioPins = (sm.GpioPins & ~pinMask) | pinValue;
                WriteGpioPins?.Invoke(pinValue, pinMask);
                break;
            }
            case 1: sm.X = data; break;
            case 2: sm.Y = data; break;
            case 3: break;  // NULL
            case 4: {  // PINDIRS: write bitCount dirs at OutBase
                var outBase = (int)sm.OutBase;
                var pinMask = bitCount < 32 ? ((1u << bitCount) - 1) << outBase : 0xFFFFFFFFu;
                var pinValue = (data & (bitCount < 32 ? (1u << bitCount) - 1 : 0xFFFFFFFFu)) << outBase;
                sm.GpioPinDirs = (sm.GpioPinDirs & ~pinMask) | pinValue;
                WriteGpioDirs?.Invoke(pinValue, pinMask);
                break;
            }
            case 5: sm.PC = data & 0x1F; sm.PcJumped = true; sm.Stalled = false; return;  // PC
            case 6: sm.ISR = data; sm.IsrCount = (uint)bitCount; break;
            case 7: ExecuteInstr(sm, (ushort)data, (int)((data >> 13) & 7)); return;  // EXEC
        }

        if (sm.AutopullEnabled && sm.OsrCount == 0)
            DoPull(sm, false);
    }

    // PUSH: bits [6]=IfFull, [5]=Block
    private void ExecPush(PioStateMachine sm, ushort instr)
    {
        var ifFull = (instr & 0x40) != 0;
        var block  = (instr & 0x20) != 0;

        if (ifFull && sm.IsrCount < (uint)sm.AutopushThreshold)
        {
            sm.Stalled = false;
            return;
        }

        DoPush(sm, block);
    }

    private void DoPush(PioStateMachine sm, bool block)
    {
        if (sm.RxFifo.Count >= sm.RxDepth)
        {
            sm.Stalled = block;
            return;
        }
        sm.RxFifo.Enqueue(sm.ISR);
        sm.ISR = 0;
        sm.IsrCount = 0;
        sm.Stalled = false;
    }

    // PULL: bits [6]=IfEmpty, [5]=Block
    private void ExecPull(PioStateMachine sm, ushort instr)
    {
        var ifEmpty = (instr & 0x40) != 0;
        var block   = (instr & 0x20) != 0;

        if (ifEmpty && sm.OsrCount > 0)
        {
            sm.Stalled = false;
            return;
        }

        DoPull(sm, block);
    }

    private void DoPull(PioStateMachine sm, bool block)
    {
        if (sm.TxFifo.Count == 0)
        {
            if (block) { sm.Stalled = true; return; }
            sm.OSR = sm.X;  // refill with X when non-blocking
        }
        else
        {
            sm.OSR = sm.TxFifo.Dequeue();
        }
        sm.OsrCount = 32;
        sm.Stalled = false;
    }

    // MOV: bits [7:5]=dest, [4:3]=op, [2:0]=source
    private void ExecMov(PioStateMachine sm, ushort instr)
    {
        var dest   = (instr >> 5) & 0x7;
        var op     = (instr >> 3) & 0x3;  // 0=none, 1=invert, 2=bit-reverse, 3=reserved
        var source = instr & 0x7;

        uint data = source switch
        {
            0 => ReadGpioIn?.Invoke() ?? sm.GpioPins,  // PINS: read physical GPIO
            1 => sm.X,
            2 => sm.Y,
            3 => 0,
            5 => ComputeStatus(sm),
            6 => sm.ISR,
            7 => sm.OSR,
            _ => 0,
        };

        data = op switch
        {
            1 => ~data,
            2 => BitReverse(data),
            _ => data,
        };

        switch (dest)
        {
            case 0: {  // PINS: write via OutBase/OutCount
                var outBase  = (int)sm.OutBase;
                var outCount = (int)sm.OutCount;
                var pinMask  = outCount > 0 ? ((1u << outCount) - 1) << outBase : 0xFFFFFFFFu;
                var pinValue = outCount > 0 ? (data & ((1u << outCount) - 1)) << outBase : data;
                sm.GpioPins = (sm.GpioPins & ~pinMask) | pinValue;
                WriteGpioPins?.Invoke(pinValue, pinMask);
                break;
            }
            case 1: sm.X = data; break;
            case 2: sm.Y = data; break;
            case 4: ExecuteInstr(sm, (ushort)data, (int)((data >> 13) & 7)); return;  // EXEC
            case 5: sm.PC = data & 0x1F; sm.PcJumped = true; sm.Stalled = false; return;  // PC
            case 6: sm.ISR = data; sm.IsrCount = 32; break;
            case 7: sm.OSR = data; sm.OsrCount = 32; break;
        }
        sm.Stalled = false;
    }

    // IRQ: bits [6]=clear, [5]=wait, [4:0]=index (bit 4 = REL flag)
    private void ExecIrq(PioStateMachine sm, ushort instr)
    {
        var smIdx   = Array.IndexOf(_sm, sm);
        var doClear = (instr & 0x40) != 0;
        var doWait  = (instr & 0x20) != 0;
        var index   = (uint)(instr & 0x1F);
        var rel     = (index & 0x10) != 0;
        // If REL: bits[3:2] unchanged, bits[1:0] = (index + smIdx) mod 4
        var flagIdx = rel ? (int)((index & 0x1C) | (((index & 3) + (uint)smIdx) & 3))
                         : (int)(index & 0x7);

        if (doClear)
        {
            _irq &= ~(1u << flagIdx);
        }
        else
        {
            _irq |= 1u << flagIdx;
        }

        if (doWait && !doClear)
            sm.Stalled = (_irq & (1u << flagIdx)) != 0;  // wait for flag to be cleared
        else
            sm.Stalled = false;
    }

    // SET: bits [7:5]=dest, [4:0]=data
    private void ExecSet(PioStateMachine sm, ushort instr)
    {
        var dest = (instr >> 5) & 0x7;
        var data = (uint)(instr & 0x1F);

        switch (dest)
        {
            case 0: {  // PINS: SET_COUNT pins at SET_BASE
                var setBase  = (int)sm.SetBase;
                var setCount = (int)sm.SetCount;
                var pinMask  = setCount > 0 ? ((1u << setCount) - 1) << setBase : 0u;
                var pinValue = setCount > 0 ? (data & ((1u << setCount) - 1)) << setBase : 0u;
                sm.GpioPins = (sm.GpioPins & ~pinMask) | pinValue;
                WriteGpioPins?.Invoke(pinValue, pinMask);
                break;
            }
            case 1: sm.X           = data; break;
            case 2: sm.Y           = data; break;
            case 4: {  // PINDIRS: SET_COUNT dirs at SET_BASE
                var setBase  = (int)sm.SetBase;
                var setCount = (int)sm.SetCount;
                var pinMask  = setCount > 0 ? ((1u << setCount) - 1) << setBase : 0u;
                var pinValue = setCount > 0 ? (data & ((1u << setCount) - 1)) << setBase : 0u;
                sm.GpioPinDirs = (sm.GpioPinDirs & ~pinMask) | pinValue;
                WriteGpioDirs?.Invoke(pinValue, pinMask);
                break;
            }
        }
        sm.Stalled = false;
    }

    private static uint BitReverse(uint v)
    {
        v = ((v >> 1) & 0x55555555u) | ((v & 0x55555555u) << 1);
        v = ((v >> 2) & 0x33333333u) | ((v & 0x33333333u) << 2);
        v = ((v >> 4) & 0x0F0F0F0Fu) | ((v & 0x0F0F0F0Fu) << 4);
        v = ((v >> 8) & 0x00FF00FFu) | ((v & 0x00FF00FFu) << 8);
        return (v >> 16) | (v << 16);
    }

    // STATUS source for MOV: 0xFFFFFFFF if FIFO count < STATUS_N, else 0
    private static uint ComputeStatus(PioStateMachine sm)
    {
        var n = (int)sm.StatusN;
        var count = sm.StatusSel == 0
            ? sm.TxFifo.Count   // TX FIFO level
            : sm.RxFifo.Count;  // RX FIFO level
        return (uint)(count < n ? 0xFFFFFFFF : 0);
    }
}
