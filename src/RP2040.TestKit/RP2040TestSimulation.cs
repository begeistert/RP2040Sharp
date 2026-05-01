using RP2040.Peripherals;
using RP2040.Peripherals.Gpio;
using RP2040.Peripherals.Uart;
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
        // Approximate: assume ~1 cycle/instruction average
        Machine.Run((int)Math.Min(cycles, int.MaxValue));
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

    public void Dispose() => Machine.Dispose();
}
