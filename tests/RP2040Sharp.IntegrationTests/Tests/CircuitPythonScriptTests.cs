using FluentAssertions;
using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Tests for CircuitPython's script-execution pipeline and filesystem.
///
/// <b>Emulator limitations — filesystem writes:</b>
/// CircuitPython's FAT filesystem (CIRCUITPY drive) flushes data to the RP2040's QSPI flash
/// via the SSI peripheral.  The emulator does not yet implement the SSI flash-programming
/// command sequence, so all writes to the filesystem are no-ops.  This means:
///   - <see cref="CircuitPythonRunner.WriteFile"/> cannot be used to persist files across
///     sessions or soft resets for CircuitPython.
///   - MicroPython is not affected because its LittleFS operates entirely in SRAM, which
///     survives a soft reset in the emulator.
///
/// The tests here cover what <i>does</i> work:
///   - Default boot: the <c>code.py</c> baked into the CircuitPython 9.2.1 firmware image
///   - Read-side filesystem: listing and reading the files that ship in the firmware image
///   - Soft-reset lifecycle
/// </summary>
[Trait("Category", "Integration")]
public sealed class CircuitPythonScriptTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    private const string Version = "9.2.1";

    // ── Default boot behaviour ────────────────────────────────────────────────

    /// <summary>
    /// CircuitPython 9.2.1 ships with a <c>code.py</c> that prints "Hello World!".
    /// Verifies the firmware's default script runs automatically on soft reset.
    /// </summary>
    [Fact]
    public async Task Script_DefaultCodePy_RunsOnSoftReset()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue("CircuitPython must reach REPL");

        runner.SoftReset(timeoutMs: 20_000)
              .Should().BeTrue("CircuitPython must return to REPL after running code.py");

        var text = runner.UsbCdc.IsConnected ? runner.UsbCdc.Text : runner.Uart.Text;
        text.Should().Contain("Hello World!",
            "the default code.py shipped with CircuitPython 9.2.1 must print 'Hello World!'");
    }

    // ── Read-side filesystem ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the CIRCUITPY filesystem is mounted and <c>code.py</c> is visible
    /// in the root directory listing.
    /// </summary>
    [Fact]
    public async Task Script_Filesystem_ListdirShowsCodePy()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000).Should().BeTrue();
        runner.Simulation.RunMilliseconds(200);
        runner.UsbCdc.Clear();

        var found = runner.ExecuteAndWait("import os; print(os.listdir('/'))", "code.py");
        found.Should().BeTrue("os.listdir('/') must include 'code.py' from the firmware image");
    }

    /// <summary>
    /// Opens the built-in <c>code.py</c> via <c>open()</c> and verifies its content
    /// contains the expected print statement.
    /// </summary>
    [Fact]
    public async Task Script_DefaultCodePy_ContentIsReadable()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000).Should().BeTrue();
        runner.Simulation.RunMilliseconds(200);
        runner.UsbCdc.Clear();

        // The default code.py ships with a print("Hello World!") call
        var found = runner.ExecuteAndWait(
            "print(open('code.py').read())",
            "Hello World");
        found.Should().BeTrue("reading code.py must return its source containing 'Hello World'");
    }

    /// <summary>
    /// Executes the built-in <c>code.py</c> in-session via <c>exec(open('code.py').read())</c>
    /// and verifies it produces the expected output.
    /// </summary>
    [Fact]
    public async Task Script_DefaultCodePy_ExecRunsInSession()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000).Should().BeTrue();
        runner.Simulation.RunMilliseconds(200);
        runner.UsbCdc.Clear();

        var found = runner.ExecuteAndWait(
            "exec(open('code.py').read())",
            "Hello World!");
        found.Should().BeTrue("exec(open('code.py').read()) must reproduce 'Hello World!'");
    }
}

