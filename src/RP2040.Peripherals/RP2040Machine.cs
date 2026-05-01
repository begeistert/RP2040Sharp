using System.Runtime.InteropServices;
using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals.Apb;
using RP2040.Peripherals.Gpio;
using RP2040.Peripherals.Ppb;
using RP2040.Peripherals.Sio;
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
    public PpbPeripheral    Ppb   { get; }
    public SioPeripheral    Sio   { get; }
    public UartPeripheral   Uart0 { get; }
    public UartPeripheral   Uart1 { get; }
    public TimerPeripheral  Timer { get; }
    public IoBank0Peripheral IoBank0 { get; }
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

        // ── GPIO pins ────────────────────────────────────────────────
        var pins = new GpioPin[30];
        for (var i = 0; i < 30; i++)
            pins[i] = new GpioPin(i, Sio);
        Gpio = pins;

        // ── Tickable list (fixed-size, no allocation in hot path) ────
        _tickables = [Ppb, Timer];
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
