using RP2040.TestKit;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Probes;

namespace RP2040Sharp.IntegrationTests.Infrastructure;

/// <summary>
/// High-level runner that boots MicroPython on the RP2040 emulator and exposes a REPL
/// interface for driving tests via UART injection.
///
/// Usage:
/// <code>
/// await using var mp = await MicroPythonRunner.CreateAsync("v1.21.0");
/// mp.Should().NotBeNull("firmware should be available");
///
/// bool booted = mp.WaitForPrompt();
/// booted.Should().BeTrue();
///
/// mp.Execute("print('hello')");
/// mp.WaitForOutput("hello").Should().BeTrue();
/// </code>
/// </summary>
public sealed class MicroPythonRunner : IAsyncDisposable
{
    private readonly PicoSimulation _sim;

    public UartProbe Uart => _sim.Uart0;
    public UsbCdcProbe UsbCdc => _sim.UsbCdc;
    public PicoSimulation Simulation => _sim;

    private MicroPythonRunner(PicoSimulation sim)
    {
        _sim = sim;
    }

    /// <summary>
    /// Create a runner loaded with MicroPython <paramref name="version"/>.
    /// Returns <c>null</c> when the firmware is not available (no network / not cached).
    /// </summary>
    public static async Task<MicroPythonRunner?> CreateAsync(string version)
    {
        var uf2Path = await FirmwareCache.GetMicroPythonAsync(version);
        if (uf2Path is null)
            return null;

        var uf2Bytes = await File.ReadAllBytesAsync(uf2Path);
        var flashImage = Uf2Reader.ToFlashImage(uf2Bytes);

        var sim = new PicoSimulation();
        sim.LoadFlash(flashImage);
        return new MicroPythonRunner(sim);
    }

    // ── REPL helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Run the simulation until the MicroPython REPL prompt (<c>&gt;&gt;&gt; </c>) appears on UART
    /// or USB-CDC, or until <paramref name="timeoutMs"/> elapses.
    /// </summary>
    public bool WaitForPrompt(double timeoutMs = 15_000)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs)
        {
            _sim.RunMilliseconds(batchMs);
            if (Uart.Text.Contains(">>> ", StringComparison.Ordinal)
                || UsbCdc.Text.Contains(">>> ", StringComparison.Ordinal))
                return true;
            elapsed += batchMs;
        }
        return false;
    }

    /// <summary>
    /// Inject a line of Python code into the REPL (appends <c>\r\n</c>).
    /// Call <see cref="WaitForPrompt"/> first to ensure the REPL is ready.
    /// </summary>
    public void Execute(string pythonLine)
    {
        Uart.Clear();
        _sim.Uart0.InjectString(pythonLine + "\r\n");
    }

    /// <summary>
    /// Run the simulation until <paramref name="expectedText"/> appears in the UART output
    /// captured since the last <see cref="Execute"/> call.
    /// </summary>
    public bool WaitForOutput(string expectedText, double timeoutMs = 5_000) =>
        _sim.RunUntilOutput(Uart, expectedText, timeoutMs);

    /// <summary>
    /// Inject a Python line and wait for <paramref name="expectedOutput"/>.
    /// Returns <c>true</c> if the expected text appeared before the timeout.
    /// </summary>
    public bool ExecuteAndWait(string pythonLine, string expectedOutput, double timeoutMs = 5_000)
    {
        Execute(pythonLine);
        return WaitForOutput(expectedOutput, timeoutMs);
    }

    /// <summary>
    /// Run simulation in batches until <paramref name="predicate"/> over UART text returns true.
    /// </summary>
    public bool WaitForOutput(Func<string, bool> predicate, double timeoutMs = 5_000) =>
        _sim.RunUntilOutput(Uart, predicate, timeoutMs);

    public ValueTask DisposeAsync()
    {
        _sim.Dispose();
        return ValueTask.CompletedTask;
    }
}
