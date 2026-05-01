using System.Runtime.InteropServices;
using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals.Adc;
using RP2040.Peripherals.Ahb;
using RP2040.Peripherals.Apb;
using RP2040.Peripherals.Dma;
using RP2040.Peripherals.Gpio;
using RP2040.Peripherals.I2c;
using RP2040.Peripherals.Pio;
using RP2040.Peripherals.Ppb;
using RP2040.Peripherals.Pwm;
using RP2040.Peripherals.Sio;
using RP2040.Peripherals.Spi;
using RP2040.Peripherals.Timer;
using RP2040.Peripherals.Uart;

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

    // ── Core ────────────────────────────────────────────────────────
    public BusInterconnect Bus  { get; }
    public CortexM0Plus    Cpu  { get; }

    // ── Peripherals ─────────────────────────────────────────────────
    public PpbPeripheral     Ppb    { get; }
    public SioPeripheral     Sio    { get; }
    public UartPeripheral    Uart0  { get; }
    public UartPeripheral    Uart1  { get; }
    public TimerPeripheral   Timer  { get; }
    public IoBank0Peripheral IoBank0 { get; }
    public DmaPeripheral     Dma    { get; }
    public PwmPeripheral     Pwm    { get; }
    public AdcPeripheral     Adc    { get; }
    public SpiPeripheral     Spi0   { get; }
    public SpiPeripheral     Spi1   { get; }
    public I2cPeripheral     I2c0   { get; }
    public I2cPeripheral     I2c1   { get; }
    public PioPeripheral     Pio0   { get; }
    public PioPeripheral     Pio1   { get; }
    public IReadOnlyList<GpioPin> Gpio { get; }

    private readonly ITickable[] _tickables;

    public RP2040Machine()
    {
        Bus = new BusInterconnect();
        Cpu = new CortexM0Plus(Bus);

        // ── PPB (0xE) ────────────────────────────────────────────────
        Ppb = new PpbPeripheral(Cpu);
        Bus.MapDevice(0xE, Ppb);

        // ── SIO (0xD) ────────────────────────────────────────────────
        Sio = new SioPeripheral(Cpu);
        Bus.MapDevice(0xD, Sio);

        // ── APB bridge (0x4) ─────────────────────────────────────────
        var apb = new ApbBridge();
        Bus.MapDevice(4, apb);

        // UART0 @ 0x40034000, UART1 @ 0x40038000
        Uart0 = new UartPeripheral();
        Uart1 = new UartPeripheral();
        apb.Register(0x40034000, Uart0);
        apb.Register(0x40038000, Uart1);

        // Timer @ 0x40054000
        Timer = new TimerPeripheral(Cpu, CLK_HZ);
        apb.Register(0x40054000, Timer);

        // IO_BANK0 @ 0x40014000
        IoBank0 = new IoBank0Peripheral(Sio);
        apb.Register(0x40014000, IoBank0);

        // PWM @ 0x40050000
        Pwm = new PwmPeripheral(Cpu);
        apb.Register(0x40050000, Pwm);

        // ADC @ 0x4004C000
        Adc = new AdcPeripheral(Cpu);
        apb.Register(0x4004C000, Adc);

        // SPI0 @ 0x4003C000, SPI1 @ 0x40040000
        Spi0 = new SpiPeripheral();
        Spi1 = new SpiPeripheral();
        apb.Register(0x4003C000, Spi0);
        apb.Register(0x40040000, Spi1);

        // I2C0 @ 0x40044000, I2C1 @ 0x40048000
        I2c0 = new I2cPeripheral();
        I2c1 = new I2cPeripheral();
        apb.Register(0x40044000, I2c0);
        apb.Register(0x40048000, I2c1);

        // ── AHB bridge (0x5): DMA + PIO ─────────────────────────────
        var ahb = new AhbBridge();
        Bus.MapDevice(5, ahb);

        // DMA @ 0x50000000
        Dma = new DmaPeripheral(Bus, Cpu);
        ahb.Register(0x50000000, Dma);

        // PIO0 @ 0x50200000, PIO1 @ 0x50300000
        Pio0 = new PioPeripheral(Cpu, 0);
        Pio1 = new PioPeripheral(Cpu, 1);
        ahb.Register(0x50200000, Pio0);
        ahb.Register(0x50300000, Pio1);

        // ── GPIO pins ────────────────────────────────────────────────
        var pins = new GpioPin[30];
        for (var i = 0; i < 30; i++)
            pins[i] = new GpioPin(i, Sio);
        Gpio = pins;

        // ── Tickable list (fixed-size, no allocation in hot path) ────
        _tickables = [Ppb, Timer, Pwm, Pio0, Pio1];
    }

    /// <summary>
    /// Load a binary image into Flash starting at 0x10000000.
    /// The image size must not exceed 2 MB.
    /// </summary>
    public unsafe void LoadFlash(ReadOnlySpan<byte> image)
    {
        if (image.Length > BusInterconnect.MASK_FLASH + 1)
            throw new ArgumentException("Flash image exceeds 2 MB");

        image.CopyTo(new Span<byte>(Bus.PtrFlash, image.Length));
        Cpu.Reset();
    }

    /// <summary>
    /// Load a binary image into BootROM at 0x00000000.
    /// </summary>
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

    /// <summary>Reset the CPU and clear peripheral state.</summary>
    public void Reset()
    {
        Cpu.Reset();
    }

    public void Dispose()
    {
        Bus.Dispose();
    }
}
