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
using RP2040.Peripherals.Pads;
using RP2040.Peripherals.Pio;
using RP2040.Peripherals.Ppb;
using RP2040.Peripherals.Psm;
using RP2040.Peripherals.Pwm;
using RP2040.Peripherals.Resets;
using RP2040.Peripherals.Rtc;
using RP2040.Peripherals.Sio;
using RP2040.Peripherals.Spi;
using RP2040.Peripherals.SysCfg;
using RP2040.Peripherals.SysInfo;
using RP2040.Peripherals.Tbman;
using RP2040.Peripherals.Timer;
using RP2040.Peripherals.Uart;
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
    public IReadOnlyList<GpioPin> Gpio { get; }

    private readonly ITickable[] _tickables;

    public RP2040Machine()
    {
        Bus = new BusInterconnect();
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
        IoBank0 = new IoBank0Peripheral(Sio);
        apb.Register(0x40014000, IoBank0);

        // PADS_BANK0 @ 0x4001C000 (slot 7), PADS_QSPI @ 0x40020000 (slot 8)
        PadsBank0 = new PadsPeripheral();
        apb.Register(0x4001C000, PadsBank0);

        PadsQspi = new PadsPeripheral();
        apb.Register(0x40020000, PadsQspi);

        // XOSC @ 0x40024000 (slot 9)
        Xosc = new XoscPeripheral();
        apb.Register(0x40024000, Xosc);

        // BUSCTRL @ 0x40030000 (slot 12)
        Busctrl = new BusctrlPeripheral();
        apb.Register(0x40030000, Busctrl);

        // UART0 @ 0x40034000 (slot 13), UART1 @ 0x40038000 (slot 14)
        Uart0 = new UartPeripheral();
        Uart1 = new UartPeripheral();
        apb.Register(0x40034000, Uart0);
        apb.Register(0x40038000, Uart1);

        // SPI0 @ 0x4003C000 (slot 15), SPI1 @ 0x40040000 (slot 16)
        Spi0 = new SpiPeripheral();
        Spi1 = new SpiPeripheral();
        apb.Register(0x4003C000, Spi0);
        apb.Register(0x40040000, Spi1);

        // I2C0 @ 0x40044000 (slot 17), I2C1 @ 0x40048000 (slot 18)
        I2c0 = new I2cPeripheral();
        I2c1 = new I2cPeripheral();
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
        Rtc = new RtcPeripheral();
        apb.Register(0x4005C000, Rtc);

        // TBMAN @ 0x4006C000 (slot 27)
        Tbman = new TbmanPeripheral();
        apb.Register(0x4006C000, Tbman);

        // ── AHB bridge (0x5): DMA + PIO ──────────────────────────────────
        var ahb = new AhbBridge();
        Bus.MapDevice(5, ahb);

        // DMA @ 0x50000000 (slot 0)
        Dma = new DmaPeripheral(Bus, Cpu);
        ahb.Register(0x50000000, Dma);

        // PIO0 @ 0x50200000 (slot 2), PIO1 @ 0x50300000 (slot 3)
        Pio0 = new PioPeripheral(Cpu, 0);
        Pio1 = new PioPeripheral(Cpu, 1);
        ahb.Register(0x50200000, Pio0);
        ahb.Register(0x50300000, Pio1);

        // ── GPIO pins ─────────────────────────────────────────────────────
        var pins = new GpioPin[30];
        for (var i = 0; i < 30; i++)
            pins[i] = new GpioPin(i, Sio);
        Gpio = pins;

        // ── Tickable list ─────────────────────────────────────────────────
        _tickables = [Ppb, Timer, Pwm, Pio0, Pio1];
    }

    /// <summary>Load a binary image into Flash starting at 0x10000000 (max 2 MB).</summary>
    public unsafe void LoadFlash(ReadOnlySpan<byte> image)
    {
        if (image.Length > BusInterconnect.MASK_FLASH + 1)
            throw new ArgumentException("Flash image exceeds 2 MB");

        image.CopyTo(new Span<byte>(Bus.PtrFlash, image.Length));
        Cpu.Reset();
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
