using RP2040.Peripherals;
using RP2040.Peripherals.Gpio;
using RP2040.Peripherals.Uart;
using RP2040.Peripherals.Usb;
using RP2040.TestKit.Probes;

namespace RP2040.TestKit;

/// <summary>
/// Fluent test harness for the RP2040 emulator.
/// <example>
/// <code>
/// var sim = RP2040TestSimulation.Create()
///     .WithFrequency(125_000_000)
///     .WithBinary(flashBytes)
///     .AddUart(0, out var uart);
///
/// sim.RunMilliseconds(10);
/// uart.Should().Contain("Hello");
/// </code>
/// </example>
/// </summary>
public class RP2040TestSimulation : IDisposable
{
    protected readonly RP2040Machine Machine;

    /// <summary>Direct CPU access for low-level assertions.</summary>
    public RP2040.Core.Cpu.CortexM0Plus Cpu => Machine.Cpu;

    /// <summary>
    /// Direct access to the RP2040 machine for advanced probe scenarios
    /// (e.g. attaching SPI callbacks, injecting GPIO signals).
    /// </summary>
    public RP2040Machine Rp2040 => Machine;

    private uint _clkHz = RP2040Machine.CLK_HZ;

    protected RP2040TestSimulation()
    {
        Machine = new RP2040Machine();
    }

    /// <summary>Create a new simulation instance.</summary>
    public static RP2040TestSimulation Create() => new();

    // ── Configuration ────────────────────────────────────────────────

    /// <summary>Override the simulated CPU frequency (default 125 MHz).</summary>
    public RP2040TestSimulation WithFrequency(uint hz)
    {
        _clkHz = hz;
        return this;
    }

    /// <summary>Load a binary image into Flash at 0x10000000 and reset the CPU.</summary>
    public RP2040TestSimulation WithBinary(ReadOnlySpan<byte> bytes)
    {
        Machine.LoadFlash(bytes);
        return this;
    }

    /// <summary>Load a binary image into BootROM at 0x00000000.</summary>
    public RP2040TestSimulation WithBootRom(ReadOnlySpan<byte> bytes)
    {
        Machine.LoadBootRom(bytes);
        return this;
    }

    /// <summary>Attach a <see cref="UartProbe"/> to the specified UART (0 or 1).</summary>
    public RP2040TestSimulation AddUart(int index, out UartProbe probe)
    {
        var uart = index == 0 ? Machine.Uart0 : Machine.Uart1;
        probe = new UartProbe();
        probe.Attach(uart);
        return this;
    }

    /// <summary>Lazily-created CDC-ACM host driver bound to the device USB peripheral.</summary>
    public UsbCdcHost UsbCdcHost => _usbCdcHost ??= new UsbCdcHost(Machine.Usb);
    private UsbCdcHost? _usbCdcHost;

    /// <summary>Attach a <see cref="UsbCdcProbe"/> to the auto-enumerated USB-CDC channel.</summary>
    public RP2040TestSimulation AddUsbCdc(out UsbCdcProbe probe)
    {
        probe = new UsbCdcProbe().Attach(UsbCdcHost);
        return this;
    }

    /// <summary>
    /// Get a reference to a GPIO pin for assertions.
    /// Pin numbers are 0-29.
    /// </summary>
    public RP2040TestSimulation AddGpio(int pin, out GpioPin gpioPin)
    {
        gpioPin = Machine.Gpio[pin];
        return this;
    }

    // ── Execution ────────────────────────────────────────────────────

    /// <summary>Execute exactly <paramref name="instructions"/> instructions.</summary>
    public RP2040TestSimulation RunInstructions(int instructions)
    {
        Machine.Run(instructions);
        return this;
    }

    /// <summary>Execute for approximately <paramref name="cycles"/> CPU cycles.</summary>
    public RP2040TestSimulation RunCycles(long cycles)
    {
        // Run in batches so time-aware peripherals (Timer, Watchdog, …) are ticked
        // frequently enough for interrupt-driven wakeups (e.g. sleep_ms via WFE) to work.
        // Batch ≈ 500 000 cycles (~4 ms at 125 MHz) gives ms-level timer accuracy while
        // reducing bookkeeping overhead 10× vs the former 50 K batch — a measurable speedup
        // for multi-second simulations such as MicroPython boot (≈60 simulated seconds).
        const int BatchSize = 500_000;
        while (cycles > 0)
        {
            var batch = (int)Math.Min(cycles, BatchSize);
            Machine.Run(batch);
            cycles -= batch;
        }
        return this;
    }

    /// <summary>Execute for <paramref name="microseconds"/> simulated microseconds.</summary>
    public RP2040TestSimulation RunMicroseconds(double microseconds)
    {
        var cycles = (long)(microseconds * _clkHz / 1_000_000.0);
        return RunCycles(cycles);
    }

    /// <summary>Execute for <paramref name="milliseconds"/> simulated milliseconds.</summary>
    public RP2040TestSimulation RunMilliseconds(double milliseconds)
        => RunMicroseconds(milliseconds * 1000.0);

    /// <summary>Execute a single instruction.</summary>
    public RP2040TestSimulation Step()
    {
        Machine.Cpu.Step();
        return this;
    }

    /// <summary>
    /// Execute until <paramref name="predicate"/> returns true or <paramref name="maxInstructions"/>
    /// is reached.
    /// </summary>
    public RP2040TestSimulation RunUntil(Func<RP2040TestSimulation, bool> predicate,
        int maxInstructions = 1_000_000)
    {
        for (var i = 0; i < maxInstructions && !predicate(this); i++)
            Machine.Cpu.Step();
        return this;
    }

    /// <summary>Execute until a BKPT instruction is encountered (or limit is reached).</summary>
    public RP2040TestSimulation RunToBreak(int maxInstructions = 1_000_000)
    {
        byte? received = null;
        var prev = Machine.Cpu.OnBreakpoint;
        Machine.Cpu.OnBreakpoint = b => received = b;

        for (var i = 0; i < maxInstructions && received is null; i++)
            Machine.Cpu.Step();

        Machine.Cpu.OnBreakpoint = prev;
        return this;
    }

    /// <summary>Reset the CPU to its initial state.</summary>
    public RP2040TestSimulation Reset()
    {
        Machine.Reset();
        return this;
    }

    // ── Output capture helpers ────────────────────────────────────────

    /// <summary>
    /// Run the simulation in batches until <paramref name="expectedText"/> appears in
    /// <paramref name="uart"/>'s captured output, or <paramref name="timeoutMs"/> elapses.
    /// Returns <c>true</c> when the expected text was found.
    /// </summary>
    public bool RunUntilOutput(UartProbe uart, string expectedText, double timeoutMs = 10_000)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs)
        {
            RunMilliseconds(batchMs);
            if (uart.Text.Contains(expectedText, StringComparison.Ordinal))
                return true;
            elapsed += batchMs;
        }
        return false;
    }

    /// <summary>
    /// Run the simulation in batches until <paramref name="predicate"/> over the captured UART
    /// text returns true, or <paramref name="timeoutMs"/> elapses.
    /// </summary>
    public bool RunUntilOutput(UartProbe uart, Func<string, bool> predicate, double timeoutMs = 10_000)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs)
        {
            RunMilliseconds(batchMs);
            if (predicate(uart.Text))
                return true;
            elapsed += batchMs;
        }
        return false;
    }

    public void Dispose() => Machine.Dispose();
}

public static class UsbCdcProbeRunExtensions
{
    /// <summary>Run the simulation until <paramref name="expectedText"/> appears in the CDC stream.</summary>
    public static bool RunUntilOutput(this RP2040TestSimulation sim, UsbCdcProbe cdc, string expectedText, double timeoutMs = 10_000)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs)
        {
            sim.RunMilliseconds(batchMs);
            if (cdc.Text.Contains(expectedText, StringComparison.Ordinal))
                return true;
            elapsed += batchMs;
        }
        return false;
    }

    /// <summary>Run the simulation until <paramref name="predicate"/> over the CDC text returns true.</summary>
    public static bool RunUntilOutput(this RP2040TestSimulation sim, UsbCdcProbe cdc, Func<string, bool> predicate, double timeoutMs = 10_000)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs)
        {
            sim.RunMilliseconds(batchMs);
            if (predicate(cdc.Text)) return true;
            elapsed += batchMs;
        }
        return false;
    }
}
