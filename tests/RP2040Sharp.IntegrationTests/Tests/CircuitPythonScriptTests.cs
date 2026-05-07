using FluentAssertions;
using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Tests for CircuitPython's script-execution pipeline and QSPI flash filesystem.
///
/// <b>Filesystem write support:</b>
/// CircuitPython's FAT filesystem (CIRCUITPY drive) flushes data to the RP2040's
/// QSPI flash via the SSI peripheral.  The SSI now emulates the full W25Q flash
/// command set (WRITE_ENABLE, SECTOR_ERASE, PAGE_PROGRAM, READ_DATA, etc.), so
/// filesystem writes made via the REPL persist across soft resets.
///
/// Test categories:
/// <list type="bullet">
///   <item>Default boot: the <c>code.py</c> shipped with CircuitPython 9.2.1</item>
///   <item>Read-side filesystem: listing and reading the firmware's files</item>
///   <item>WriteFile + SoftReset: write new scripts via REPL and verify auto-execution</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public sealed class CircuitPythonScriptTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    private const string Version = "9.2.1";

    // ── Shared boot helper ────────────────────────────────────────────────────

    private static async Task<CircuitPythonRunner?> BootToReplAsync()
    {
        var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return null;
        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue($"CircuitPython {Version} must reach REPL within 20 s");
        runner.Simulation.RunMilliseconds(200);
        runner.UsbCdc.Clear();
        return runner;
    }

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

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

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

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

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

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        var found = runner.ExecuteAndWait(
            "exec(open('code.py').read())",
            "Hello World!");
        found.Should().BeTrue("exec(open('code.py').read()) must reproduce 'Hello World!'");
    }

    // ── Shared boot helper (writable FS) ─────────────────────────────────────

    /// <summary>
    /// Boot helper for tests that need to write files.
    /// Boots with USB-CDC so CircuitPython initialises normally, then injects a
    /// <c>boot.py</c> that calls <c>storage.disable_usb_drive()</c> into the FAT
    /// before performing a soft reset so the file takes effect.
    /// After the second boot the REPL is on USB-CDC and the filesystem is writable
    /// from Python code.
    /// </summary>
    private static async Task<CircuitPythonRunner?> BootToReplWritableAsync()
    {
        // CreateWithWritableFsAsync boots CircuitPython, injects boot.py, soft-resets so
        // boot.py runs (disabling USB-MSC), then waits for the REPL again before returning.
        return await CircuitPythonRunner.CreateWithWritableFsAsync(Version);
    }

    // ── WriteFile + SoftReset (QSPI flash write) ──────────────────────────────

    /// <summary>
    /// Verifies that the CIRCUITPY filesystem is writable from Python code.
    /// Runs without USB host so CircuitPython doesn't lock the FAT via USB-MSC.
    /// </summary>
    [Fact]
    public async Task Script_Filesystem_IsWritableFromPython()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplWritableAsync();
        if (runner is null) return;

        // Write a probe file and immediately read it back in the same session.
        runner.WriteFile("write_probe.txt", "probe_content_xyz");

        runner.Simulation.RunMilliseconds(200);
        runner.UsbCdc.Clear();

        var found = runner.ExecuteAndWait(
            "print(open('write_probe.txt').read())",
            "probe_content_xyz");

        found.Should().BeTrue(
            "the CIRCUITPY filesystem must be writable from Python when USB host is absent");
    }

    /// <summary>
    /// Writes a new <c>code.py</c> via REPL, performs a soft reset, and verifies the
    /// written script runs automatically.  Exercises the full QSPI flash write path:
    /// CircuitPython flushes the FAT filesystem via the bootrom flash_range_program hook,
    /// which the emulator applies directly to the flash image.
    /// </summary>
    [Fact]
    public async Task Script_WriteCodePy_RunsAfterSoftReset()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplWritableAsync();
        if (runner is null) return;

        runner.WriteFile("code.py", "print('written by WriteFile')\n")
              .Should().BeTrue("WriteFile must succeed on a ready REPL");

        runner.SoftReset(timeoutMs: 20_000)
              .Should().BeTrue("CircuitPython must return to REPL after soft reset");

        var text = runner.UsbCdc.IsConnected ? runner.UsbCdc.Text : runner.Uart.Text;
        text.Should().Contain("written by WriteFile",
            "the new code.py written via REPL must run automatically after soft reset");
    }

    /// <summary>
    /// Writes a <c>code.py</c> that computes an arithmetic expression, soft resets,
    /// and verifies the correct result is printed.
    /// </summary>
    [Fact]
    public async Task Script_WriteCodePy_ComputesAndPrintsArithmetic()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplWritableAsync();
        if (runner is null) return;

        runner.WriteFile("code.py", "x = 6 * 7\nprint('result:', x)\n")
              .Should().BeTrue();

        runner.SoftReset(timeoutMs: 20_000)
              .Should().BeTrue("must return to REPL after soft reset");

        var text = runner.UsbCdc.IsConnected ? runner.UsbCdc.Text : runner.Uart.Text;
        text.Should().Contain("result: 42",
            "code.py must compute 6 * 7 = 42 and print it on soft reset");
    }
}

