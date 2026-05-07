using RP2040.TestKit;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Probes;

namespace RP2040Sharp.IntegrationTests.Infrastructure;

/// <summary>
/// High-level runner that boots CircuitPython on the RP2040 emulator and exposes a REPL
/// interface for driving tests via UART injection.
///
/// CircuitPython routes its REPL through USB-CDC (TinyUSB) when USB is available,
/// falling back to the hardware UART otherwise.  The runner detects which transport
/// the REPL prompt appears on and routes all subsequent I/O through the same channel.
///
/// Usage:
/// <code>
/// await using var cp = await CircuitPythonRunner.CreateAsync("9.2.1");
/// cp.Should().NotBeNull("firmware should be available");
///
/// bool booted = cp.WaitForPrompt();
/// booted.Should().BeTrue();
///
/// cp.Execute("print('hello')");
/// cp.WaitForOutput("hello").Should().BeTrue();
/// </code>
/// </summary>
public sealed class CircuitPythonRunner : IAsyncDisposable
{
    private readonly PicoSimulation _sim;

    // CircuitPython also routes its REPL through USB-CDC when available.
    private bool _replViaUsbCdc;

    public UartProbe Uart    => _sim.Uart0;
    public UsbCdcProbe UsbCdc => _sim.UsbCdc;
    public PicoSimulation Simulation => _sim;

    private CircuitPythonRunner(PicoSimulation sim)
    {
        _sim = sim;
    }

    /// <summary>
    /// Create a runner loaded with CircuitPython <paramref name="version"/> (e.g. "9.2.1").
    /// Returns <c>null</c> when the firmware is not available (no network / not cached).
    /// </summary>
    public static async Task<CircuitPythonRunner?> CreateAsync(string version)
    {
        var uf2Path = await FirmwareCache.GetCircuitPythonAsync(version);
        if (uf2Path is null)
            return null;

        var uf2Bytes = await File.ReadAllBytesAsync(uf2Path);
        var flashImage = Uf2Reader.ToFlashImage(uf2Bytes);

        var sim = new PicoSimulation();
        sim.LoadFlash(flashImage);
        return new CircuitPythonRunner(sim);
    }

    // ── REPL helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Run the simulation until the CircuitPython REPL prompt (<c>&gt;&gt;&gt; </c>) appears on
    /// UART or USB-CDC, or until <paramref name="timeoutMs"/> elapses.
    /// CircuitPython may also emit a "Press any key to enter the REPL" message before the
    /// prompt; this helper sends a key press automatically when that message is detected.
    /// </summary>
    public bool WaitForPrompt(double timeoutMs = 20_000)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        var keySent = false;
        while (elapsed < timeoutMs)
        {
            _sim.RunMilliseconds(batchMs);
            elapsed += batchMs;

            // CircuitPython may ask for a keypress to enter the REPL when no code.py exists.
            // Only send it once — the text is accumulated so "Press any key" stays visible
            // across iterations and we must not flood the fifo with extra keypresses.
            if (!keySent)
            {
                if (UsbCdc.Text.Contains("Press any key", StringComparison.OrdinalIgnoreCase))
                {
                    UsbCdc.InjectString("\r");
                    keySent = true;
                }
                else if (Uart.Text.Contains("Press any key", StringComparison.OrdinalIgnoreCase))
                {
                    Uart.InjectString("\r");
                    keySent = true;
                }
            }

            if (Uart.Text.Contains(">>> ", StringComparison.Ordinal))
            {
                _replViaUsbCdc = false;
                // Run one extra batch so any pending USB endpoint reads that
                // accumulated during the wait are drained before the caller
                // injects the next command.  Without this drain, the first
                // Execute() after WaitForPrompt can be partially swallowed by
                // a stale ZLP→re-arm cycle in the USB peripheral.
                _sim.RunMilliseconds(100);
                return true;
            }
            if (UsbCdc.Text.Contains(">>> ", StringComparison.Ordinal))
            {
                _replViaUsbCdc = true;
                _sim.RunMilliseconds(100); // same drain on the CDC path
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Inject a line of Python code into the REPL (appends <c>\r\n</c>).
    /// Call <see cref="WaitForPrompt"/> first to ensure the REPL is ready.
    /// </summary>
    public void Execute(string pythonLine)
    {
        if (_replViaUsbCdc)
        {
            UsbCdc.Clear();
            UsbCdc.InjectString(pythonLine + "\r\n");
        }
        else
        {
            Uart.Clear();
            Uart.InjectString(pythonLine + "\r\n");
        }
    }

    /// <summary>
    /// Run the simulation until <paramref name="expectedText"/> appears in the output
    /// captured since the last <see cref="Execute"/> call.
    /// </summary>
    public bool WaitForOutput(string expectedText, double timeoutMs = 5_000) =>
        _replViaUsbCdc
            ? _sim.RunUntilOutput(UsbCdc, expectedText, timeoutMs)
            : _sim.RunUntilOutput(Uart, expectedText, timeoutMs);

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
    /// Run simulation in batches until <paramref name="predicate"/> over the output text returns true.
    /// </summary>
    public bool WaitForOutput(Func<string, bool> predicate, double timeoutMs = 5_000) =>
        _replViaUsbCdc
            ? _sim.RunUntilOutput(UsbCdc, predicate, timeoutMs)
            : _sim.RunUntilOutput(Uart, predicate, timeoutMs);

    /// <summary>
    /// Inject a compound statement (<c>def</c>, <c>for</c>, <c>class</c>, <c>if</c>, etc.) into
    /// the REPL.  Sends the statement, waits for the <c>... </c> continuation prompt, then sends
    /// a blank line to execute it, and finally waits for the next <c>&gt;&gt;&gt; </c> prompt.
    /// Returns <c>true</c> if the REPL prompt was seen before the timeout.
    /// </summary>
    public bool ExecuteCompound(string pythonLine, double timeoutMs = 15_000)
    {
        if (_replViaUsbCdc)
        {
            UsbCdc.Clear();
            UsbCdc.InjectString(pythonLine + "\r\n");
        }
        else
        {
            Uart.Clear();
            Uart.InjectString(pythonLine + "\r\n");
        }

        const double batchMs = 100.0;
        var elapsed = 0.0;
        var gotContinuation = false;
        while (elapsed < timeoutMs)
        {
            _sim.RunMilliseconds(batchMs);
            var text = _replViaUsbCdc ? UsbCdc.Text : Uart.Text;
            if (text.Contains(">>> ", StringComparison.Ordinal)) return true;
            if (text.Contains("... ", StringComparison.Ordinal)) { gotContinuation = true; break; }
            elapsed += batchMs;
        }

        if (!gotContinuation) return false;

        if (_replViaUsbCdc)
            UsbCdc.InjectString("\r\n");
        else
            Uart.InjectString("\r\n");

        return WaitForPrompt(timeoutMs - elapsed);
    }

    // ── Filesystem helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Write <paramref name="content"/> to <paramref name="path"/> on the CircuitPython
    /// virtual filesystem (FAT, exposed as the <c>CIRCUITPY</c> drive, mounted after boot).
    ///
    /// The primary entry point for CircuitPython is <c>code.py</c> (with <c>main.py</c>,
    /// <c>code.txt</c>, and <c>main.txt</c> as fall-backs in that order).
    ///
    /// The file is written via REPL injection.  Call <see cref="WaitForPrompt"/> first
    /// to ensure the REPL is ready.  Content is sent in chunks of 150 escaped characters
    /// so it always fits within the REPL line buffer.
    /// </summary>
    /// <returns><c>true</c> if the file was written successfully before the timeout.</returns>
    public bool WriteFile(string path, string content, double timeoutMs = 5_000)
    {
        const int chunkSize = 150;

        var escapedPath    = EscapePythonString(path);
        var escapedContent = EscapePythonString(content);

        if (!ExecuteAndWait($"_wf=open('{escapedPath}','w')", ">>> ", timeoutMs))
            return false;

        var pos = 0;
        while (pos < escapedContent.Length)
        {
            var end = Math.Min(pos + chunkSize, escapedContent.Length);

            // Don't split in the middle of a \-escape sequence.
            while (end > pos + 1)
            {
                var slashes = 0;
                for (var k = end - 1; k >= pos && escapedContent[k] == '\\'; k--)
                    slashes++;
                if (slashes % 2 == 0) break;
                end--;
            }

            var chunk = escapedContent.Substring(pos, end - pos);
            if (!ExecuteAndWait($"_wf.write('{chunk}')", ">>> ", timeoutMs))
                return false;

            pos = end;
        }

        return ExecuteAndWait("_wf.close()", ">>> ", timeoutMs);
    }

    /// <summary>
    /// Write <paramref name="content"/> bytes to <paramref name="filename"/> (a simple
    /// filename in the root directory, e.g. <c>"code.py"</c>) by directly manipulating
    /// the FAT filesystem via USB MSC sector writes.
    ///
    /// Unlike <see cref="WriteFile"/>, this path bypasses the Python REPL and writes
    /// data directly to the flash via the CircuitPython MSC device interface.  Writes
    /// are intercepted by the emulator's <c>flash_range_program</c> hook and therefore
    /// persist in the emulated flash, surviving soft resets.
    ///
    /// Prerequisites: <see cref="WaitForPrompt"/> must have been called first (ensures
    /// the USB MSC interface is enumerated and the CIRCUITPY filesystem is mounted).
    /// </summary>
    /// <returns><c>true</c> if the sectors were written successfully before the timeout.</returns>
    public bool WriteFileViaMsc(string filename, byte[] content, double timeoutMs = 15_000)
    {
        var msc = _sim.UsbMsc;
        if (!msc.IsConnected) return false;

        // Open the FAT volume using the MSC probe as the sector transport.
        var fat = FatVolume.Open(
            lba  => MscReadSector(lba, timeoutMs),
            (lba, data) => MscWriteSector(lba, data, timeoutMs));
        if (fat is null || !fat.IsValid) return false;

        return fat.WriteFile(filename, content);
    }

    /// <summary>
    /// Convenience overload that encodes <paramref name="content"/> as UTF-8.
    /// </summary>
    public bool WriteFileViaMsc(string filename, string content, double timeoutMs = 15_000)
        => WriteFileViaMsc(filename, System.Text.Encoding.UTF8.GetBytes(content), timeoutMs);

    // ── MSC sector transport helpers ──────────────────────────────────────────

    private byte[] MscReadSector(uint lba, double timeoutMs)
    {
        byte[]? result = null;
        _sim.UsbMsc.RequestRead(lba, data => result = data);
        const double batchMs = 20.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs && result is null)
        {
            _sim.RunMilliseconds(batchMs);
            elapsed += batchMs;
        }
        return result ?? new byte[512];
    }

    private void MscWriteSector(uint lba, byte[] data, double timeoutMs)
    {
        var done = false;
        _sim.UsbMsc.RequestWrite(lba, data, _ => done = true);
        const double batchMs = 20.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs && !done)
        {
            _sim.RunMilliseconds(batchMs);
            elapsed += batchMs;
        }
    }

    /// <summary>
    /// Perform a CircuitPython soft reset (CTRL+D).  The VM re-runs <c>code.py</c>
    /// (or the first available fall-back: <c>main.py</c>, <c>code.txt</c>,
    /// <c>main.txt</c>).  Any output is captured in <see cref="UsbCdc"/> /
    /// <see cref="Uart"/>.  Returns <c>true</c> when the <c>&gt;&gt;&gt;&nbsp;</c>
    /// prompt reappears within <paramref name="timeoutMs"/>.
    /// </summary>
    public bool SoftReset(double timeoutMs = 20_000)
    {
        if (_replViaUsbCdc)
        {
            UsbCdc.Clear();
            UsbCdc.InjectString("\x04");
        }
        else
        {
            Uart.Clear();
            Uart.InjectString("\x04");
        }
        return WaitForPrompt(timeoutMs);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string EscapePythonString(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\'': sb.Append("\\'");  break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20 || c > 0x7E)
                        sb.Append($"\\x{(int)c:x2}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    public ValueTask DisposeAsync()
    {
        _sim.Dispose();
        return ValueTask.CompletedTask;
    }
}
