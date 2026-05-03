using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals.Adc;
using RP2040.Peripherals.Ahb;
using RP2040.Peripherals.Apb;
using RP2040.Peripherals.Busctrl;
using RP2040.Peripherals.Clocks;
using RP2040.Peripherals.Dma;
using RP2040.Peripherals.Gpio;
using RP2040.Peripherals.I2c;
using RP2040.Peripherals.IoQspi;
using RP2040.Peripherals.Pads;
using RP2040.Peripherals.Pio;
using RP2040.Peripherals.Pll;
using RP2040.Peripherals.Ppb;
using RP2040.Peripherals.Psm;
using RP2040.Peripherals.Pwm;
using RP2040.Peripherals.Resets;
using RP2040.Peripherals.Rosc;
using RP2040.Peripherals.Rtc;
using RP2040.Peripherals.Sio;
using RP2040.Peripherals.Spi;
using RP2040.Peripherals.Ssi;
using RP2040.Peripherals.SysCfg;
using RP2040.Peripherals.SysInfo;
using RP2040.Peripherals.Tbman;
using RP2040.Peripherals.Timer;
using RP2040.Peripherals.Uart;
using RP2040.Peripherals.Usb;
using RP2040.Peripherals.Vreg;
using RP2040.Peripherals.Watchdog;
using RP2040.Peripherals.Xosc;

namespace RP2040.Peripherals;

/// <summary>
/// Root class that wires all RP2040 peripherals together.
/// Typical usage:
/// <code>
/// var machine = new RP2040Machine();
/// machine.LoadFlash(bytes);
/// machine.Run(1_000_000);
/// </code>
/// </summary>
public sealed class RP2040Machine : IDisposable
{
    public const uint CLK_HZ = 125_000_000;

    // ── Core ────────────────────────────────────────────────────────────
    public BusInterconnect Bus { get; }
    public CortexM0Plus    Cpu { get; }

    // ── System peripherals ──────────────────────────────────────────────
    public PpbPeripheral      Ppb      { get; }
    public SioPeripheral      Sio      { get; }
    public SysInfoPeripheral  SysInfo  { get; }
    public SysCfgPeripheral   SysCfg   { get; }
    public PsmPeripheral      Psm      { get; }
    public ResetsPeripheral   Resets   { get; }
    public ClocksPeripheral   Clocks   { get; }
    public XoscPeripheral     Xosc     { get; }
    public WatchdogPeripheral Watchdog { get; }
    public BusctrlPeripheral  Busctrl  { get; }
    public TbmanPeripheral    Tbman    { get; }
    public PllPeripheral      PllSys   { get; }
    public PllPeripheral      PllUsb   { get; }
    public RoscPeripheral     Rosc     { get; }
    public VregPeripheral     Vreg     { get; }
    public SsiPeripheral      Ssi      { get; }
    public IoQspiPeripheral   IoQspi   { get; }

    // ── I/O peripherals ─────────────────────────────────────────────────
    public IoBank0Peripheral IoBank0   { get; }
    public PadsPeripheral    PadsBank0 { get; }
    public PadsPeripheral    PadsQspi  { get; }
    public TimerPeripheral   Timer     { get; }
    public UartPeripheral    Uart0     { get; }
    public UartPeripheral    Uart1     { get; }
    public SpiPeripheral     Spi0      { get; }
    public SpiPeripheral     Spi1      { get; }
    public I2cPeripheral     I2c0      { get; }
    public I2cPeripheral     I2c1      { get; }
    public AdcPeripheral     Adc       { get; }
    public PwmPeripheral     Pwm       { get; }
    public RtcPeripheral     Rtc       { get; }
    public DmaPeripheral     Dma       { get; }
    public PioPeripheral     Pio0      { get; }
    public PioPeripheral     Pio1      { get; }
    public UsbPeripheral     Usb       { get; }
    public IReadOnlyList<GpioPin> Gpio { get; }

    private readonly ITickable[] _tickables;

    public RP2040Machine(uint flashSize = 2 * 1024 * 1024)
    {
        Bus = new BusInterconnect(flashSize);
        Cpu = new CortexM0Plus(Bus);

        // ── PPB (0xE) ────────────────────────────────────────────────────
        Ppb = new PpbPeripheral(Cpu);
        Bus.MapDevice(0xE, Ppb);

        // ── SIO (0xD) ────────────────────────────────────────────────────
        Sio = new SioPeripheral(Cpu);
        Bus.MapDevice(0xD, Sio);

        // ── APB bridge (0x4) ─────────────────────────────────────────────
        var apb = new ApbBridge();
        Bus.MapDevice(4, apb);

        // System info / config (slots 0–1)
        SysInfo = new SysInfoPeripheral();
        apb.Register(0x40000000, SysInfo);

        SysCfg = new SysCfgPeripheral();
        apb.Register(0x40004000, SysCfg);

        // Clocks @ 0x40008000 (slot 2)
        Clocks = new ClocksPeripheral();
        apb.Register(0x40008000, Clocks);

        // RESETS @ 0x4000C000 (slot 3)
        Resets = new ResetsPeripheral();
        apb.Register(0x4000C000, Resets);

        // PSM @ 0x40010000 (slot 4)
        Psm = new PsmPeripheral();
        apb.Register(0x40010000, Psm);

        // IO_BANK0 @ 0x40014000 (slot 5)
        IoBank0 = new IoBank0Peripheral(Sio, Cpu);
        apb.Register(0x40014000, IoBank0);

        // PADS_BANK0 @ 0x4001C000 (slot 7), PADS_QSPI @ 0x40020000 (slot 8)
        PadsBank0 = new PadsPeripheral();
        apb.Register(0x4001C000, PadsBank0);

        PadsQspi = new PadsPeripheral();
        apb.Register(0x40020000, PadsQspi);

        // XOSC @ 0x40024000 (slot 9)
        Xosc = new XoscPeripheral();
        apb.Register(0x40024000, Xosc);

        // PLL_SYS @ 0x40028000 (slot 10), PLL_USB @ 0x4002C000 (slot 11)
        PllSys = new PllPeripheral();
        apb.Register(0x40028000, PllSys);

        PllUsb = new PllPeripheral();
        apb.Register(0x4002C000, PllUsb);

        // IO_QSPI @ 0x40018000 (slot 6)
        IoQspi = new IoQspiPeripheral();
        apb.Register(0x40018000, IoQspi);

        // BUSCTRL @ 0x40030000 (slot 12)
        Busctrl = new BusctrlPeripheral();
        apb.Register(0x40030000, Busctrl);

        // UART0 @ 0x40034000 (slot 13), UART1 @ 0x40038000 (slot 14)
        Uart0 = new UartPeripheral(Cpu, irq: 20);
        Uart1 = new UartPeripheral(Cpu, irq: 21);
        apb.Register(0x40034000, Uart0);
        apb.Register(0x40038000, Uart1);

        // SPI0 @ 0x4003C000 (slot 15), SPI1 @ 0x40040000 (slot 16)
        Spi0 = new SpiPeripheral(Cpu, irq: 18);
        Spi1 = new SpiPeripheral(Cpu, irq: 19);
        apb.Register(0x4003C000, Spi0);
        apb.Register(0x40040000, Spi1);

        // I2C0 @ 0x40044000 (slot 17), I2C1 @ 0x40048000 (slot 18)
        I2c0 = new I2cPeripheral(Cpu, irq: 23);
        I2c1 = new I2cPeripheral(Cpu, irq: 24);
        apb.Register(0x40044000, I2c0);
        apb.Register(0x40048000, I2c1);

        // ADC @ 0x4004C000 (slot 19)
        Adc = new AdcPeripheral(Cpu);
        apb.Register(0x4004C000, Adc);

        // PWM @ 0x40050000 (slot 20)
        Pwm = new PwmPeripheral(Cpu);
        apb.Register(0x40050000, Pwm);

        // Timer @ 0x40054000 (slot 21)
        Timer = new TimerPeripheral(Cpu, CLK_HZ);
        apb.Register(0x40054000, Timer);

        // Watchdog @ 0x40058000 (slot 22)
        Watchdog = new WatchdogPeripheral();
        apb.Register(0x40058000, Watchdog);

        // RTC @ 0x4005C000 (slot 23)
        Rtc = new RtcPeripheral(Cpu);
        apb.Register(0x4005C000, Rtc);

        // TBMAN @ 0x4006C000 (slot 27)
        Tbman = new TbmanPeripheral();
        apb.Register(0x4006C000, Tbman);

        // ROSC @ 0x40060000 (slot 24), VREG @ 0x40064000 (slot 25)
        Rosc = new RoscPeripheral();
        apb.Register(0x40060000, Rosc);

        Vreg = new VregPeripheral();
        apb.Register(0x40064000, Vreg);

        // SSI at 0x18000000 is within XIP Flash region — registered as sub-device so
        // all accesses to [0x18000000, 0x18FFFFFF] route to SSI registers while
        // [0x10000000, 0x17FFFFFF] continues to use the flash pointer fast path.
        Ssi = new SsiPeripheral();
        Bus.RegisterSsi(Ssi);

        // ── AHB bridge (0x5): DMA + PIO ──────────────────────────────────
        var ahb = new AhbBridge();
        Bus.MapDevice(5, ahb);

        // DMA @ 0x50000000 (slot 0)
        Dma = new DmaPeripheral(Bus, Cpu);
        ahb.Register(0x50000000, Dma);

        // USB @ 0x50100000 (slot 1, covers DPRAM + REGS at 0x50110000)
        Usb = new UsbPeripheral(Cpu);
        ahb.Register(0x50100000, Usb);

        // PIO0 @ 0x50200000 (slot 2), PIO1 @ 0x50300000 (slot 3)
        Pio0 = new PioPeripheral(Cpu, 0);
        Pio1 = new PioPeripheral(Cpu, 1);
        ahb.Register(0x50200000, Pio0);
        ahb.Register(0x50300000, Pio1);

        // ── GPIO pins ─────────────────────────────────────────────────────
        var pins = new GpioPin[30];
        for (var i = 0; i < 30; i++)
            pins[i] = new GpioPin(i, Sio, IoBank0);
        Gpio = pins;

        // ── Tickable list ─────────────────────────────────────────────────
        _tickables = [Ppb, Timer, Pwm, Pio0, Pio1, Rtc, Watchdog];

        // ── DMA DREQ sources ──────────────────────────────────────────────
        // PIO0 TX/RX SM0-3: DREQ 0-3 (TX), 4-7 (RX)
        // PIO1 TX/RX SM0-3: DREQ 8-11 (TX), 12-15 (RX)
        for (var i = 0; i < 4; i++)
        {
            var sm = i;
            Dma.RegisterDreq( 0 + sm, () => Pio0.TxFifoNotFull(sm));
            Dma.RegisterDreq( 4 + sm, () => !Pio0.RxFifoEmpty(sm));
            Dma.RegisterDreq( 8 + sm, () => Pio1.TxFifoNotFull(sm));
            Dma.RegisterDreq(12 + sm, () => !Pio1.RxFifoEmpty(sm));
        }
        // SPI0 TX(16), RX(17), SPI1 TX(18), RX(19)
        Dma.RegisterDreq(16, () => true);              // SPI0 TX always ready
        Dma.RegisterDreq(17, () => Spi0.RxDataAvailable);
        Dma.RegisterDreq(18, () => true);              // SPI1 TX always ready
        Dma.RegisterDreq(19, () => Spi1.RxDataAvailable);
        // UART0 TX(20), RX(21), UART1 TX(22), RX(23)
        Dma.RegisterDreq(20, () => true);              // UART0 TX always ready
        Dma.RegisterDreq(21, () => Uart0.RxDataAvailable);
        Dma.RegisterDreq(22, () => true);              // UART1 TX always ready
        Dma.RegisterDreq(23, () => Uart1.RxDataAvailable);
        // ADC DREQ 36: RX FIFO has data
        Dma.RegisterDreq(36, () => Adc.HasFifoData);

        // ── PIO GPIO integration ───────────────────────────────────────────
        // Shared helpers: read physical GPIO levels; update SIO output and notify IoBank0
        uint ReadGpio() => Sio.GpioIn | Sio.GpioOut;

        void ApplyPins(uint value, uint mask)
        {
            // PIO output: update SIO GpioIn so physical level is visible to CPU reads
            Sio.GpioIn = (Sio.GpioIn & ~mask) | (value & mask);
            // Notify IoBank0 for edge/level interrupt detection on each changed pin
            for (var pin = 0; pin < 30; pin++)
                if ((mask & (1u << pin)) != 0)
                    IoBank0.UpdatePinInput(pin, (value & (1u << pin)) != 0);
        }

        Pio0.ReadGpioIn    = ReadGpio;
        Pio0.WriteGpioPins = ApplyPins;
        Pio0.WriteGpioDirs = (value, mask) => { /* dir changes tracked in SM only */ };

        Pio1.ReadGpioIn    = ReadGpio;
        Pio1.WriteGpioPins = ApplyPins;
        Pio1.WriteGpioDirs = (value, mask) => { };
    }

    /// <summary>Load a binary image into Flash starting at 0x10000000.</summary>
    public unsafe void LoadFlash(ReadOnlySpan<byte> image)
    {
        if (image.Length > Bus.FlashSize)
            throw new ArgumentException($"Flash image exceeds configured flash size ({Bus.FlashSize / 1024} KB)");

        image.CopyTo(new Span<byte>(Bus.PtrFlash, image.Length));

        // If no BootROM has been loaded, install the real RP2040 B1 BootROM binary.
        // The real bootrom implements rom_table_lookup, memcpy44, memset4 and all
        // bit-manipulation helpers correctly in native Thumb code.
        // Flash-hardware-accessing functions (connect_internal_flash, flash_exit_xip,
        // flash_flush_cache, flash_enter_cmd_xip) are patched to BX LR so they return
        // immediately without touching SSI registers.
        // flash_range_erase and flash_range_program are intercepted by C# native hooks.
        if (*(uint*)Bus.PtrBootRom == 0 && *(uint*)(Bus.PtrBootRom + 4) == 0)
        {
            LoadRealBootRom(Bus.PtrBootRom);

            if (TryFindVectorTable(Bus.PtrFlash, (int)image.Length, out var sp, out var resetPc,
                    out var vectorTableOffset))
            {
                // Real BootROM sets VTOR to point at the firmware's own vector table
                // before branching to the Reset handler.  pico-sdk code checks VTOR
                // during spinlock initialisation, so this must be done before Reset().
                Cpu.Registers.VTOR = 0x10000000u + (uint)vectorTableOffset;
            }

            // Register C# hooks only for flash erase/program at their real bootrom
            // addresses so MicroPython's LittleFS formatter can modify emulated flash.
            Cpu.RegisterNativeHook(0x237C, FlashEraseHook);
            Cpu.RegisterNativeHook(0x23C4, FlashProgramHook);
        }

        Cpu.Reset();

        // rp2040js-compatible boot: bypass the bootrom reset handler (which tries to
        // configure SSI/QSPI hardware that is not fully emulated) and start execution
        // directly at the flash start address 0x10000000, where boot2 lives.
        // The bootrom is still resident and handles ROM API calls (rom_table_lookup, etc.)
        // The firmware's own SP comes from the vector table entry we found above.
        if (TryFindVectorTable(Bus.PtrFlash, (int)image.Length, out var firmwareSp, out _,
                out _))
        {
            Cpu.Registers.SP = firmwareSp;
        }
        Cpu.Registers.PC = BusInterconnect.FLASH_START_ADDRESS;
    }

    /// <summary>
    /// Scans the flash image for an ARM Cortex-M vector table by looking for a word
    /// whose upper byte places it in SRAM (0x20xxxxxx) followed by a Thumb-mode pointer
    /// into Flash (0x1xxxxxxx with LSB set).
    /// </summary>
    private static unsafe bool TryFindVectorTable(byte* flash, int size,
        out uint sp, out uint resetPc, out int vectorTableOffset)
    {
        // RP2040 SDK firmware: main vector table at offset 0x100 (after 256-byte boot2).
        // Bare Cortex-M firmware (no boot2): vector table at offset 0.
        // Also try 0x200 for exotic layouts.
        ReadOnlySpan<int> offsets = [0x100, 0, 0x200];

        foreach (var off in offsets)
        {
            if (off + 8 > size) continue;

            var candidateSp = *(uint*)(flash + off);
            var candidatePc = *(uint*)(flash + off + 4);

            // SP must be within RP2040 SRAM (0x20000000 – 0x2007FFFF), 4-byte aligned.
            if ((candidateSp >> 19) != (0x20000000u >> 19)) continue;
            if ((candidateSp & 3) != 0) continue;

            // Reset PC must be a Thumb pointer (LSB = 1) into Flash (0x10xxxxxx).
            if ((candidatePc & 1) == 0) continue;
            if ((candidatePc >> 24) != 0x10) continue;

            sp = candidateSp;
            resetPc = candidatePc;
            vectorTableOffset = off;
            return true;
        }

        sp = 0;
        resetPc = 0;
        vectorTableOffset = 0;
        return false;
    }

    // ── Native hook: ROM function lookup ─────────────────────────────────────

    /// <summary>
    /// Function codes for the ROM function lookup table, indexed for fast access.
    /// Key = 16-bit ROM code, Value = BootROM address (even, Thumb bit NOT included).
    /// </summary>
    private static readonly Dictionary<uint, uint> RomFuncTable = new()
    {
        [0x434D] = 0x0100,  // 'MC' = memcpy44
        [0x534D] = 0x0120,  // 'MS' = memset4
        [0x3443] = 0x0100,  // 'C4' = memcpy4 (alias)
        [0x3453] = 0x0120,  // 'S4' = memset4 (alias)
        [0x3350] = 0x01C0,  // 'P3' = popcount32       (native hook at 0x01C0)
        [0x3352] = 0x01D0,  // 'R3' = reverse32        (native hook at 0x01D0)
        [0x334C] = 0x01E0,  // 'L3' = clz32            (native hook at 0x01E0)
        [0x3354] = 0x01F0,  // 'T3' = ctz32            (native hook at 0x01F0)
        [0x4649] = 0x0180,  // 'IF' = connect_internal_flash (no-op)
        [0x5845] = 0x0180,  // 'EX' = flash_exit_xip (no-op)
        [0x4552] = 0x0190,  // 'RE' = flash_range_erase (native hook)
        [0x5052] = 0x01A0,  // 'RP' = flash_range_program (native hook)
        [0x4346] = 0x0180,  // 'FC' = flash_flush_cache (no-op)
        [0x5843] = 0x0180,  // 'CX' = flash_enter_cmd_xip (no-op)
        // Soft-float data table: 'SF' returns pointer to an empty table (terminator only at 0x0250)
        [0x4653] = 0x0250,  // 'SF' = soft_float_table stub
    };

    private static int _romLookupCount = 0;
    private static void RomTableLookupHook(Core.Cpu.CortexM0Plus cpu)
    {
        // r0 = table ptr (uint16_t*), r1 = code  →  r0 = func addr with Thumb bit, or 0
        var code = cpu.Registers.R1 & 0xFFFF;
        if (RomFuncTable.TryGetValue(code, out var addr))
        {
            if (System.Threading.Interlocked.Increment(ref _romLookupCount) <= 20)
                System.Console.Error.WriteLine($"  [romtbl] code=0x{code:X4} ('{(char)(code & 0xFF)}{(char)((code >> 8) & 0xFF)}')->0x{addr | 1u:X4} LR=0x{cpu.Registers.LR:X8}");
            cpu.Registers.R0 = addr | 1u;
        }
        else
        {
            System.Console.Error.WriteLine($"  [romtbl] Unknown ROM code=0x{code:X4} ('{(char)(code & 0xFF)}{(char)((code >> 8) & 0xFF)}') at LR=0x{cpu.Registers.LR:X8} R0=0x{cpu.Registers.R0:X8} R1=0x{cpu.Registers.R1:X8} SP=0x{cpu.Registers.SP:X8} cycles={cpu.Cycles} → returning no-op");
            cpu.Registers.R0 = 0x0181u;  // BX LR (safe no-op instead of NULL)
        }
    }

    /// <summary>
    /// Native hook for <c>flash_range_erase(uint32_t flash_offs, size_t count, ...)</c>.
    /// Fills the specified flash region with 0xFF (erased state).
    /// Called by the CPU when PC = 0x0190 (registered in <see cref="LoadFlash"/>).
    /// </summary>
    private unsafe void FlashEraseHook(Core.Cpu.CortexM0Plus cpu)
    {
        var offset = (int)(cpu.Registers.R0 & (Bus.FlashSize - 1));
        var count  = (int)cpu.Registers.R1;
        if (count < 0 || offset + count > (int)Bus.FlashSize) count = (int)Bus.FlashSize - offset;
        if (count > 0)
            new Span<byte>(Bus.PtrFlash + offset, count).Fill(0xFF);
    }

    /// <summary>
    /// Native hook for <c>flash_range_program(uint32_t flash_offs, const uint8_t* data, size_t count)</c>.
    /// Copies bytes from SRAM (or anywhere in the address space) into the emulated flash.
    /// Called by the CPU when PC = 0x01A0 (registered in <see cref="LoadFlash"/>).
    /// </summary>
    private unsafe void FlashProgramHook(Core.Cpu.CortexM0Plus cpu)
    {
        var flashOffset = (int)(cpu.Registers.R0 & (Bus.FlashSize - 1));
        var srcAddr     = cpu.Registers.R1;
        var count       = (int)cpu.Registers.R2;
        if (count < 0 || flashOffset + count > (int)Bus.FlashSize)
            count = (int)Bus.FlashSize - flashOffset;
        for (var i = 0; i < count; i++)
            Bus.PtrFlash[flashOffset + i] = Bus.ReadByte(srcAddr + (uint)i);
    }

    /// <summary>
    /// Native hook for bootrom memcpy44: copies n bytes (arbitrary count) from src to dst.
    /// Signature: void* memcpy44(void* dst, const void* src, size_t n) → R0=dst
    /// </summary>
    private unsafe void Memcpy44Hook(Core.Cpu.CortexM0Plus cpu)
    {
        var dst = cpu.Registers.R0;
        var src = cpu.Registers.R1;
        var n   = (int)cpu.Registers.R2;
        for (var i = 0; i < n; i++)
            Bus.WriteByte(dst + (uint)i, Bus.ReadByte(src + (uint)i));
        // R0 = original dst (already set, unchanged)
    }

    /// <summary>
    /// Native hook for bootrom memset4: fills n bytes with value c.
    /// Signature: void* memset4(void* dst, uint8_t c, size_t n) → R0=dst
    /// The real RP2040 bootrom 'MS' function handles arbitrary n.
    /// </summary>
    private unsafe void Memset4Hook(Core.Cpu.CortexM0Plus cpu)
    {
        var dst = cpu.Registers.R0;
        var val = (byte)(cpu.Registers.R1 & 0xFF);
        var n   = (int)cpu.Registers.R2;
        for (var i = 0; i < n; i++)
            Bus.WriteByte(dst + (uint)i, val);
        // R0 = original dst (already set, unchanged)
    }

    private static void Popcount32Hook(Core.Cpu.CortexM0Plus cpu)
        => cpu.Registers.R0 = (uint)System.Numerics.BitOperations.PopCount(cpu.Registers.R0);

    private static void Reverse32Hook(Core.Cpu.CortexM0Plus cpu)
    {
        var v = cpu.Registers.R0;
        v = ((v & 0xFFFF0000u) >> 16) | ((v & 0x0000FFFFu) << 16);
        v = ((v & 0xFF00FF00u) >>  8) | ((v & 0x00FF00FFu) <<  8);
        v = ((v & 0xF0F0F0F0u) >>  4) | ((v & 0x0F0F0F0Fu) <<  4);
        v = ((v & 0xCCCCCCCCu) >>  2) | ((v & 0x33333333u) <<  2);
        v = ((v & 0xAAAAAAAAu) >>  1) | ((v & 0x55555555u) <<  1);
        cpu.Registers.R0 = v;
    }

    private static void Clz32Hook(Core.Cpu.CortexM0Plus cpu)
        => cpu.Registers.R0 = (uint)System.Numerics.BitOperations.LeadingZeroCount(cpu.Registers.R0);

    private static void Ctz32Hook(Core.Cpu.CortexM0Plus cpu)
        => cpu.Registers.R0 = (uint)System.Numerics.BitOperations.TrailingZeroCount(cpu.Registers.R0);

    /// <summary>
    /// Loads the real RP2040 B1 bootrom binary (embedded as a resource) into bootrom
    /// memory, then patches flash hardware-accessing functions to BX LR so they return
    /// without touching SSI/QSPI registers that are not fully emulated.
    /// </summary>
    private static unsafe void LoadRealBootRom(byte* rom)
    {
        // Load binary from embedded resource
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("RP2040Sharp.bootrom_b1.bin")
            ?? throw new InvalidOperationException(
                "Embedded resource 'RP2040Sharp.bootrom_b1.bin' not found. " +
                "Ensure bootrom_b1.bin is included as an EmbeddedResource in the project.");
        stream.ReadExactly(new Span<byte>(rom, 16384));

        // Patch flash hardware-accessing bootrom functions to 'BX LR' (0x4770).
        // These functions talk directly to the SSI/QSPI peripheral, which is not
        // fully emulated. They are called by MicroPython's LittleFS flash trampoline
        // (which runs from SRAM) to set up/tear down XIP mode around erase/program ops.
        // Making them no-ops is safe: our C# hooks handle the actual flash data.
        //   0x24A0 = connect_internal_flash
        //   0x23F4 = flash_exit_xip
        //   0x2360 = flash_flush_cache
        //   0x2330 = flash_enter_cmd_xip
        static void PatchBxLr(byte* p, int addr) { p[addr] = 0x70; p[addr + 1] = 0x47; }
        PatchBxLr(rom, 0x24A0);
        PatchBxLr(rom, 0x23F4);
        PatchBxLr(rom, 0x2360);
        PatchBxLr(rom, 0x2330);
    }

    /// 
    /// The stub implements the ROM API (rom_table_lookup, memcpy44, memset4) using
    /// hand-assembled ARM Thumb opcodes.  Entry [0] (initial SP) and entry [1]
    /// (reset PC) are left at zero and must be patched by the caller after
    /// locating the firmware's own vector table.
    /// </summary>
    private static unsafe void WriteBootRomStub(byte* rom)
    {
        // ── helpers ─────────────────────────────────────────────────────────
        static void W16(byte* p, int off, ushort v)
        {
            p[off]     = (byte)(v & 0xFF);
            p[off + 1] = (byte)(v >> 8);
        }
        static void W32(byte* p, int off, uint v)
        {
            p[off]     = (byte)( v        & 0xFF);
            p[off + 1] = (byte)((v >>  8) & 0xFF);
            p[off + 2] = (byte)((v >> 16) & 0xFF);
            p[off + 3] = (byte)( v >> 24);
        }

        // ── Exception vector table (0x0000 – 0x003F + IRQs) ─────────────────
        //   Entry [0] = Initial SP    ← patched later by LoadFlash
        //   Entry [1] = Reset PC      ← patched later by LoadFlash
        //   All others → default_handler (BX LR at 0x0180) with Thumb bit
        const uint defaultHandler = 0x0181u;
        W32(rom, 0x0000, 0x20041000);       // BootROM initial SP (overwritten later)
        for (int i = 1; i < 16; i++)
            W32(rom, i * 4, defaultHandler);
        for (int i = 0; i < 26; i++)        // RP2040 has 26 external IRQs
            W32(rom, 0x0040 + i * 4, defaultHandler);

        // ── ROM API infrastructure (in reserved Cortex-M0+ vector slots) ─────
        //   0x0010 – ROM code magic, 0x0012 – version, 0x0014 – func_table_ptr,
        //   0x0016 – data_table_ptr, 0x0018 – rom_table_lookup fn ptr
        W16(rom, 0x0010, 0x0210);   // ROM code magic (matches real RP2040 BootROM)
        W16(rom, 0x0012, 0x02);     // ROM version 2
        W16(rom, 0x0014, 0x0200);   // function table at 0x0200
        W16(rom, 0x0016, 0x0250);   // data table at 0x0250 (just a terminator)
        W16(rom, 0x0018, 0x0061);   // rom_table_lookup at 0x0060 (Thumb bit = 0x0061)

        // ── default_handler at 0x0180: BX LR ─────────────────────────────────
        W16(rom, 0x0180, 0x4770);   // BX LR

        // ── rom_table_lookup at 0x0060 ────────────────────────────────────────
        //   r0 = table (uint16_t*), r1 = code → r0 = func addr (with Thumb bit) or 0
        //   Branch offsets: ARMv6-M PC = instruction_address + 4 when computing branch target.
        //   loop(0x60): ldrh r2,[r0]; cbz r2,not_found(0x74); uxth r3,r1; cmp r2,r3
        //               beq found(0x6E); adds r0,#4; b loop(0x60)
        //   found(0x6E): ldrh r0,[r0,#2]; bx lr
        //   not_found(0x74): movs r0,#0; bx lr
        ReadOnlySpan<ushort> lookup =
        [
            0x8802,  // 0x0060  LDRH r2, [r0, #0]             ; loop:
            0xB13A,  // 0x0062  CBZ  r2, not_found  ; PC=0x0066, +14 → 0x0074
            0xB28B,  // 0x0064  UXTH r3, r1
            0x429A,  // 0x0066  CMP  r2, r3
            0xD001,  // 0x0068  BEQ  found          ; PC=0x006C, +1×2=2 → 0x006E
            0x3004,  // 0x006A  ADDS r0, r0, #4
            0xE7F8,  // 0x006C  B    loop            ; PC=0x0070, -8×2=-16 → 0x0060
            0x8840,  // 0x006E  LDRH r0, [r0, #2]   ; found:
            0x4770,  // 0x0070  BX   LR
            0x2000,  // 0x0072  MOVS r0, #0          ; not_found:
            0x4770,  // 0x0074  BX   LR
        ];
        for (int i = 0; i < lookup.Length; i++) W16(rom, 0x0060 + i * 2, lookup[i]);

        // ── memcpy44 at 0x0100 ────────────────────────────────────────────────
        //   void *memcpy44(void *dst, const void *src, uint n)  -- n bytes (multiple of 4)
        //   Uses CBZ up-front guard so n=0 returns immediately without corrupting memory.
        //   Layout: 0x0100 – 0x0110 (9 halfwords = 18 bytes)
        ReadOnlySpan<ushort> memcpy44 =
        [
            0xB510,  // 0x0100  PUSH {r4, lr}
            0x4604,  // 0x0102  MOV  r4, r0               ; save original dst
            0xB11A,  // 0x0104  CBZ  r2, done (+6)         ; PC=0x0108, +6 → 0x010E
            0xC908,  // 0x0106  LDMIA r1!, {r3}            ; loop: r3 = *src++
            0xC008,  // 0x0108  STMIA r0!, {r3}            ; *dst++ = r3
            0x3A04,  // 0x010A  SUBS r2, r2, #4
            0xD1FB,  // 0x010C  BNE  loop          (-10)   ; PC=0x0110, -10 → 0x0106
            0x4620,  // 0x010E  MOV  r0, r4                ; done: return original dst
            0xBD10,  // 0x0110  POP  {r4, pc}
        ];
        for (int i = 0; i < memcpy44.Length; i++) W16(rom, 0x0100 + i * 2, memcpy44[i]);

        // ── memset4  at 0x0120 ────────────────────────────────────────────────
        //   void *memset4(void *dst, uint8_t c, uint n)
        //   Fills n bytes (multiple of 4) with word pattern (c,c,c,c); returns dst.
        //   Uses CBZ up-front guard: decrements n AFTER each store (no off-by-one).
        //   Layout: 0x0120 – 0x0138 (13 halfwords = 26 bytes)
        ReadOnlySpan<ushort> memset4 =
        [
            0xB510,  // 0x0120  PUSH {r4, lr}
            0x4604,  // 0x0122  MOV  r4, r0              ; save original dst
            0xB2C9,  // 0x0124  UXTB r1, r1              ; r1 = c & 0xFF (zero-extend)
            0x020B,  // 0x0126  LSLS r3, r1, #8
            0x4319,  // 0x0128  ORRS r1, r3              ; r1 = c | (c<<8)
            0x040B,  // 0x012A  LSLS r3, r1, #16
            0x4319,  // 0x012C  ORRS r1, r3              ; r1 = 4-byte word pattern
            0xB112,  // 0x012E  CBZ  r2, done (+4)        ; PC=0x0132, +4 → 0x0136
            0xC002,  // 0x0130  STMIA r0!, {r1}          ; loop: *dst++ = word
            0x3A04,  // 0x0132  SUBS r2, r2, #4
            0xD1FC,  // 0x0134  BNE  loop          (-8)   ; PC=0x0138, -8 → 0x0130
            0x4620,  // 0x0136  MOV  r0, r4              ; done: return original dst
            0xBD10,  // 0x0138  POP  {r4, pc}
        ];
        for (int i = 0; i < memset4.Length; i++) W16(rom, 0x0120 + i * 2, memset4[i]);

        // ── Native-hook stubs ─────────────────────────────────────────────────
        //   0x0190: flash_range_erase  hook  — BX LR fallback (hook fires first)
        //   0x01A0: flash_range_program hook — BX LR fallback
        //   0x01C0: popcount32, 0x01D0: reverse32, 0x01E0: clz32, 0x01F0: ctz32
        W16(rom, 0x0190, 0x4770);  // BX LR
        W16(rom, 0x01A0, 0x4770);  // BX LR
        W16(rom, 0x01C0, 0x4770);  // BX LR (popcount32 — native hook)
        W16(rom, 0x01D0, 0x4770);  // BX LR (reverse32 — native hook)
        W16(rom, 0x01E0, 0x4770);  // BX LR (clz32 — native hook)
        W16(rom, 0x01F0, 0x4770);  // BX LR (ctz32 — native hook)

        // ── Function lookup table at 0x0200 ───────────────────────────────────
        //   Format: pairs of uint16_t {code, func_ptr}, terminated by {0, 0}.
        //   'RE' and 'RP' point to native-hook stubs so C# code can modify flash.
        ReadOnlySpan<ushort> funcTable =
        [
            0x434D, 0x0101,  // 'MC' = MEMCPY / MEMCPY44     (Thumb bit: 0x0100|1)
            0x534D, 0x0121,  // 'MS' = MEMSET / MEMSET4      (Thumb bit: 0x0120|1)
            0x4649, 0x0181,  // 'IF' = connect_internal_flash (no-op BX LR)
            0x5845, 0x0181,  // 'EX' = flash_exit_xip         (no-op BX LR)
            0x4552, 0x0191,  // 'RE' = flash_range_erase  →  native hook at 0x0190
            0x5052, 0x01A1,  // 'RP' = flash_range_program → native hook at 0x01A0
            0x4346, 0x0181,  // 'FC' = flash_flush_cache       (no-op BX LR)
            0x5843, 0x0181,  // 'CX' = flash_enter_cmd_xip    (no-op BX LR)
            0x0000, 0x0000,  // terminator
        ];
        for (int i = 0; i < funcTable.Length; i++) W16(rom, 0x0200 + i * 2, funcTable[i]);

        // Data table at 0x0250: just a terminator
        W16(rom, 0x0250, 0x0000);
    }

    /// <summary>Load a binary image into BootROM at 0x00000000 (max 16 KB).</summary>
    public unsafe void LoadBootRom(ReadOnlySpan<byte> image)
    {
        if (image.Length > 0x4000)
            throw new ArgumentException("BootROM image exceeds 16 KB");

        image.CopyTo(new Span<byte>(Bus.PtrBootRom, image.Length));
    }

    /// <summary>
    /// Run the CPU for approximately <paramref name="instructions"/> instructions,
    /// then tick all time-aware peripherals.
    /// </summary>
    public void Run(int instructions)
    {
        var before = Cpu.Cycles;
        Cpu.Run(instructions);
        var delta = Cpu.Cycles - before;

        foreach (var t in _tickables)
            t.Tick(delta);
    }

    /// <summary>Reset the CPU.</summary>
    public void Reset() => Cpu.Reset();

    public void Dispose() => Bus.Dispose();
}
