using System.Runtime.CompilerServices;
using RP2040.Core.Memory;

[module: SkipLocalsInit]

namespace RP2040.Core.Cpu;

public sealed unsafe class CortexM0Plus
{
    public readonly BusInterconnect Bus;
    public Registers Registers;
    public long Cycles;

    private readonly InstructionDecoder _decoder;

    private byte* _fetchPtr;
    private uint _fetchMask;
    private uint _currentRegionId;

    private const uint EXC_RETURN_HANDLER = 0xFFFFFFF1; // Return to Handler mode, using MSP
    private const uint EXC_RETURN_THREAD_MSP = 0xFFFFFFF9; // Return to Thread mode, using MSP
    private const uint EXC_RETURN_THREAD_PSP = 0xFFFFFFFD; // Return to Thread mode, using PSP

    private const uint EXC_NMI = 2;
    private const uint EXC_HARDFAULT = 3;
    private const uint EXC_SVCALL = 11;
    private const uint EXC_PENDSV = 14;
    private const uint EXC_SYSTICK = 15;

    /// <summary>Called when a BKPT instruction is executed. Parameter is the imm8 value.</summary>
    public Action<byte>? OnBreakpoint;

    /// <summary>
    /// Native hooks: when the PC equals a registered address (Thumb bit stripped), the
    /// corresponding delegate is called instead of fetching/executing an instruction.
    /// The delegate is responsible for updating registers as needed.  After the delegate
    /// returns the CPU automatically performs <c>PC = LR &amp; ~1</c> (same as BX LR).
    /// </summary>
    private Dictionary<uint, Action<CortexM0Plus>>? _nativeHooks;
    private uint _nativeHookMax;

    public void RegisterNativeHook(uint address, Action<CortexM0Plus> hook)
    {
        _nativeHooks ??= new Dictionary<uint, Action<CortexM0Plus>>();
        address &= ~1u;
        _nativeHooks[address] = hook;
        if (address > _nativeHookMax) _nativeHookMax = address;
    }

    public CortexM0Plus(BusInterconnect bus)
    {
        Bus = bus;
        _decoder = InstructionDecoder.Instance;
        Reset();
    }

    public void Reset()
    {
        Registers.SP = Bus.ReadWord(0x00000000);
        Registers.PC = Bus.ReadWord(0x00000004) & 0xFFFFFFFE;  // strip Thumb bit, same as ExceptionEntry

        UpdateFetchCache(Registers.PC);

        Registers.N = false;
        Registers.Z = false;
        Registers.C = false;
        Registers.V = false;

        Cycles = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateFetchCache(uint pc)
    {
        _currentRegionId = pc >> 28;

        switch (_currentRegionId)
        {
            case BusInterconnect.REGION_FLASH:
                _fetchPtr = Bus.PtrFlash;
                _fetchMask = Bus.MaskFlash & ~1u;
                break;
            case BusInterconnect.REGION_SRAM:
                _fetchPtr = Bus.PtrSram;
                _fetchMask = BusInterconnect.MASK_SRAM & ~1u;
                break;
            case BusInterconnect.REGION_BOOTROM:
                _fetchPtr = Bus.PtrBootRom;
                _fetchMask = BusInterconnect.MASK_BOOTROM & ~1u;
                break;
            default:
                _fetchPtr = null;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Run(int instructions)
    {
        var decoder = _decoder;

        var fetchPtr = _fetchPtr;
        var fetchMask = _fetchMask;
        var regionId = _currentRegionId;

        while (instructions-- > 0)
        {
            // Interrupt check — predictable branch (nearly always not taken)
            if (Registers.InterruptsUpdated)
            {
                Registers.InterruptsUpdated = false;
                if (CheckForInterrupts())
                {
                    UpdateFetchCache(Registers.PC);
                    fetchPtr = _fetchPtr;
                    fetchMask = _fetchMask;
                    regionId = _currentRegionId;
                }
            }

            // WFI/WFE sleep: bail out of the current batch, crediting the unused
            // instruction budget as elapsed cycles so the outer Machine.Run can
            // advance time-aware peripherals (Timer, Watchdog, ...) and let an
            // alarm IRQ wake us on the next batch.  Without this, a CPU that
            // sleeps on the very first instruction of a batch produces delta=0
            // and the simulation deadlocks: the timer never ticks → the alarm
            // never fires → WFE never returns.
            if (Registers.Waiting)
            {
                Cycles += (uint)(instructions + 1);
                return;
            }

            var pc = Registers.PC;

            // FAST GUARD
            if ((pc >> 28) != regionId)
            {
                // FALLBACK
                UpdateFetchCache(pc);

                fetchPtr = _fetchPtr;
                fetchMask = _fetchMask;
                regionId = _currentRegionId;

                if (fetchPtr == null)
                {
                    // PC landed in an un-executable region — raise HardFault per ARMv6-M spec
                    ExceptionEntry(EXC_HARDFAULT);
                    UpdateFetchCache(Registers.PC);
                    fetchPtr  = _fetchPtr;
                    fetchMask = _fetchMask;
                    regionId  = _currentRegionId;
                    continue;
                }
            }

            // ULTRA-FAST FETCH
            // Check for native hooks — only possible in BootROM (pc < 0x23C5 after LoadFlash).
            if (_nativeHooks != null && pc <= _nativeHookMax && _nativeHooks.TryGetValue(pc, out var nativeHook))
            {
                var pcBeforeHook = Registers.PC; // equals pc (not yet advanced; advance is only in normal dispatch)
                nativeHook(this);
                // If the hook itself changed PC (e.g., to redirect execution), honor that.
                // Otherwise do the standard BX LR return.
                if (Registers.PC == pcBeforeHook)
                {
                    var hookLr = Registers.LR;
                    if (hookLr >= 0xFFFFFFF0)
                        ExceptionReturn(hookLr);
                    else
                        Registers.PC = hookLr & ~1u;
                }
                UpdateFetchCache(Registers.PC);
                fetchPtr  = _fetchPtr;
                fetchMask = _fetchMask;
                regionId  = _currentRegionId;
                Cycles++;
                continue;
            }

            var opcode = Unsafe.ReadUnaligned<ushort>(fetchPtr + (pc & fetchMask));

            // PRE-UPDATE PC (Speculative)
            Registers.PC = pc + 2;

            Cycles++;

            // DISPATCH
            decoder.Dispatch(opcode, this);
        }

        _currentRegionId = regionId;
        _fetchPtr = fetchPtr;
        _fetchMask = fetchMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Step()
    {
        var pc = Registers.PC;
        var opcode = Bus.ReadHalfWord(pc);
        Registers.PC = pc + 2;
        Cycles++;
        _decoder.Dispatch(opcode, this);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // NoInlining (it is not used commonly)
    public void UpdateStackPointerSource()
    {
        if (Registers.IPSR != 0)
            return;

        var switchToPsp = (Registers.CONTROL & 2) != 0;

        if (switchToPsp)
        {
            Registers.MSP_Storage = Registers.SP;
            Registers.SP = Registers.PSP_Storage;
        }
        else
        {
            Registers.PSP_Storage = Registers.SP;
            Registers.SP = Registers.MSP_Storage;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ExceptionEntry(uint exceptionNumber)
    {
        if (exceptionNumber == EXC_HARDFAULT)
            System.Console.Error.WriteLine($"HardFault: callerPC=0x{Registers.PC:X8} LR=0x{Registers.LR:X8} SP=0x{Registers.SP:X8}");
        var framePtr = Registers.SP;

        var needsAlign = (framePtr & 4) != 0;
        var framePtrAlign = needsAlign ? 1u : 0u;

        var stackAdjust = 0x20u + (needsAlign ? 4u : 0u);
        var finalSp = framePtr - stackAdjust;

        var frameBase = finalSp;

        Bus.WriteWord(frameBase + 0x00, Registers.R0);
        Bus.WriteWord(frameBase + 0x04, Registers.R1);
        Bus.WriteWord(frameBase + 0x08, Registers.R2);
        Bus.WriteWord(frameBase + 0x0C, Registers.R3);
        Bus.WriteWord(frameBase + 0x10, Registers.R12);
        Bus.WriteWord(frameBase + 0x14, Registers.LR);
        Bus.WriteWord(frameBase + 0x18, Registers.PC & 0xFFFFFFFE); // Return Address

        var xpsr = Registers.GetxPsr() | (framePtrAlign << 9);
        Bus.WriteWord(frameBase + 0x1C, xpsr);

        if (Registers.IPSR > 0)
        {
            Registers.LR = EXC_RETURN_HANDLER;
        }
        else
        {
            Registers.LR =
                (Registers.CONTROL & 2) != 0 ? EXC_RETURN_THREAD_PSP : EXC_RETURN_THREAD_MSP;
        }

        if ((Registers.CONTROL & 2) != 0)
        {
            Registers.PSP_Storage = finalSp;
            Registers.SP = Registers.MSP_Storage;
        }
        else
        {
            Registers.SP = finalSp;
        }

        Registers.IPSR = exceptionNumber;
        Registers.CONTROL &= ~2u;

        uint vtor = Registers.VTOR;
        var vectorAddress = vtor + (exceptionNumber * 4);

        var targetPc = Bus.ReadWord(vectorAddress);
        Registers.PC = targetPc & 0xFFFFFFFE;

        Cycles += 12; // Exception Entry cost (aprox 12-15 cycles)
    }

    // ================================================================
    // Interrupt / Exception management (called by PPB peripheral)
    // ================================================================

    public void SetInterrupt(int irq, bool pending)
    {
        if (irq is < 0 or > 25) return;
        var bit = 1u << irq;
        if (pending)
            Registers.PendingInterrupts |= bit;
        else
            Registers.PendingInterrupts &= ~bit;
        Registers.InterruptsUpdated = true;
    }

    public void TriggerNmi() { Registers.PendingNMI = true; Registers.InterruptsUpdated = true; }
    public void TriggerSysTick() { Registers.PendingSystick = true; Registers.InterruptsUpdated = true; }
    public void TriggerPendSv() { Registers.PendingPendSV = true; Registers.InterruptsUpdated = true; }
    public void TriggerHardFault() => ExceptionEntry(EXC_HARDFAULT);

    /// <summary>Returns true if an interrupt was taken (PC changed).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CheckForInterrupts()
    {
        if (Registers.PRIMASK != 0 && !Registers.PendingNMI)
            return false;

        // NMI (priority -2, always takes over everything)
        if (Registers.PendingNMI)
        {
            Registers.PendingNMI = false;
            Registers.Waiting = false;
            ExceptionEntry(EXC_NMI);
            return true;
        }

        // SVCall — only when triggered via SVC instruction
        if (Registers.PendingSVCall && Registers.PRIMASK == 0)
        {
            Registers.PendingSVCall = false;
            Registers.Waiting = false;
            ExceptionEntry(EXC_SVCALL);
            return true;
        }

        // SysTick
        if (Registers.PendingSystick && Registers.PRIMASK == 0)
        {
            Registers.PendingSystick = false;
            Registers.Waiting = false;
            ExceptionEntry(EXC_SYSTICK);
            return true;
        }

        // PendSV (lowest priority system exception)
        if (Registers.PendingPendSV && Registers.PRIMASK == 0)
        {
            Registers.PendingPendSV = false;
            Registers.Waiting = false;
            ExceptionEntry(EXC_PENDSV);
            return true;
        }

        // Hardware IRQs
        var pending = Registers.PendingInterrupts & Registers.EnabledInterrupts;
        if (pending != 0 && Registers.PRIMASK == 0)
        {
            var irq = System.Numerics.BitOperations.TrailingZeroCount(pending);
            Registers.PendingInterrupts &= ~(1u << irq);
            Registers.Waiting = false;
            ExceptionEntry((uint)(irq + 16)); // IRQ0 = Exception 16
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ExceptionReturn(uint excReturn)
    {
        var returnToThread = (excReturn & 8) != 0;
        var usePsp = (excReturn & 4) != 0;

        if (!returnToThread && usePsp)
        {
            usePsp = false;
        }

        if (returnToThread)
        {
            Registers.IPSR = 0;

            if (usePsp)
            {
                Registers.MSP_Storage = Registers.SP;
                Registers.SP = Registers.PSP_Storage;
                Registers.CONTROL |= 2;
            }
            else
            {
                Registers.CONTROL &= ~2u;
            }
        }

        var framePtr = Registers.SP;

        Registers.R0 = Bus.ReadWord(framePtr + 0x00);
        Registers.R1 = Bus.ReadWord(framePtr + 0x04);
        Registers.R2 = Bus.ReadWord(framePtr + 0x08);
        Registers.R3 = Bus.ReadWord(framePtr + 0x0C);
        Registers.R12 = Bus.ReadWord(framePtr + 0x10);
        Registers.LR = Bus.ReadWord(framePtr + 0x14);
        var retPC = Bus.ReadWord(framePtr + 0x18);
        var xpsr = Bus.ReadWord(framePtr + 0x1C);

        Registers.N = (xpsr & 0x80000000) != 0;
        Registers.Z = (xpsr & 0x40000000) != 0;
        Registers.C = (xpsr & 0x20000000) != 0;
        Registers.V = (xpsr & 0x10000000) != 0;

        var alignAdjust = (xpsr & (1 << 9)) != 0;
        var stackFree = 0x20u + (alignAdjust ? 4u : 0u);

        Registers.SP += stackFree;
        Registers.PC = retPC & 0xFFFFFFFE;

        Cycles += 10;
        // After returning from an ISR, re-check interrupts so that a still-pending
        // higher-priority IRQ (e.g. USB after SysTick) fires immediately, AND signal
        // that an event was registered so the next WFE consumes it instead of sleeping.
        // Without `EventRegistered = true`, pico-sdk WFE-loops that expect to be woken
        // by the very IRQ we just serviced will deadlock — the alarm fires once, the
        // handler runs, but the WFE that follows sleeps forever waiting for an event
        // that already happened. rp2040js cortex-m0-core.ts:339-341 sets both flags.
        Registers.InterruptsUpdated = true;
        Registers.EventRegistered = true;
    }
}
