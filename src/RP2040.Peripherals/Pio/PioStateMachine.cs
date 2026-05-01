namespace RP2040.Peripherals.Pio;

/// <summary>
/// Internal state for one PIO state machine.
/// Carries shift registers, scratch X/Y, FIFO references, and execution state.
/// </summary>
internal sealed class PioStateMachine
{
    private const int FIFO_DEPTH = 4;

    // ── Registers ────────────────────────────────────────────────────
    public uint PC;          // Program counter (0-31)
    public uint X;           // Scratch X
    public uint Y;           // Scratch Y
    public uint ISR;         // Input shift register
    public uint OSR;         // Output shift register
    public uint IsrCount;    // How many bits shifted into ISR
    public uint OsrCount;    // How many bits remain in OSR (32 when full)

    public uint ClkDiv;      // CLKDIV register (8.8 integer+frac)
    public uint ExecCtrl;    // EXECCTRL
    public uint ShiftCtrl;   // SHIFTCTRL
    public uint PinCtrl;     // PINCTRL

    // ── FIFOs ────────────────────────────────────────────────────────
    internal readonly Queue<uint> TxFifo = new(FIFO_DEPTH);
    internal readonly Queue<uint> RxFifo = new(FIFO_DEPTH);

    // ── Execution state ──────────────────────────────────────────────
    public bool Enabled;
    public bool Stalled;         // waiting for FIFO or condition
    internal long FracAccum;     // for sub-cycle fractional clock divisor
    internal uint? ForcedInstr;  // immediate instruction via INSTR write

    // ── GPIO state (driven by this SM) ───────────────────────────────
    public uint GpioPins;    // current SET/OUT output value
    public uint GpioPinDirs; // direction bits (1=output)

    public void Reset()
    {
        PC = 0; X = 0; Y = 0; ISR = 0; OSR = 0;
        IsrCount = 0; OsrCount = 0;
        Stalled = false; FracAccum = 0; ForcedInstr = null;
        TxFifo.Clear(); RxFifo.Clear();
    }

    // ── SHIFTCTRL helpers ─────────────────────────────────────────────
    /// <summary>ISR shift direction: false=left, true=right (SHIFTCTRL bit 18).</summary>
    public bool IsrShiftRight => (ShiftCtrl & (1u << 18)) != 0;
    /// <summary>OSR shift direction: false=left, true=right (SHIFTCTRL bit 19).</summary>
    public bool OsrShiftRight => (ShiftCtrl & (1u << 19)) != 0;
    /// <summary>Autopush threshold 0=32: SHIFTCTRL bits [24:20].</summary>
    public int AutopushThreshold => (int)((ShiftCtrl >> 20) & 0x1F) is 0 ? 32 : (int)((ShiftCtrl >> 20) & 0x1F);
    /// <summary>Autopull threshold 0=32: SHIFTCTRL bits [29:25].</summary>
    public int AutopullThreshold => (int)((ShiftCtrl >> 25) & 0x1F) is 0 ? 32 : (int)((ShiftCtrl >> 25) & 0x1F);
    public bool AutopushEnabled => (ShiftCtrl & (1u << 16)) != 0;
    public bool AutopullEnabled => (ShiftCtrl & (1u << 17)) != 0;

    // ── EXECCTRL helpers ──────────────────────────────────────────────
    /// <summary>Wrap top (inclusive): EXECCTRL bits [16:12].</summary>
    public uint WrapTop    => (ExecCtrl >> 12) & 0x1F;
    /// <summary>Wrap bottom: EXECCTRL bits [11:7].</summary>
    public uint WrapBottom => (ExecCtrl >> 7)  & 0x1F;
    public uint JmpPin     => (ExecCtrl >> 24) & 0x1F;
}
