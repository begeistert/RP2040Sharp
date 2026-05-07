using FluentAssertions;
using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Tests that write Python script files (boot.py / main.py) onto the MicroPython
/// virtual filesystem via <see cref="MicroPythonRunner.WriteFile"/> and verify that
/// they are executed automatically on soft-reset.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MicroPythonScriptTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    private const string Version = "v1.21.0";

    // ── main.py ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a <c>main.py</c> that prints a sentinel string and verifies the output
    /// appears automatically on the next soft reset, without any REPL injection.
    /// </summary>
    [Fact]
    public async Task Script_MainPy_RunsAutomaticallyOnBoot()
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt().Should().BeTrue("MicroPython must reach REPL to write files");

        // Write main.py to the VFS
        runner.WriteFile("main.py", "print('hello from main.py')\n")
              .Should().BeTrue("WriteFile should succeed when the REPL is ready");

        // Soft-reset: MicroPython re-runs boot.py then main.py
        runner.SoftReset(timeoutMs: 20_000)
              .Should().BeTrue("MicroPython must return to REPL after running main.py");

        // The sentinel output must have appeared during boot
        var text = runner.UsbCdc.IsConnected ? runner.UsbCdc.Text : runner.Uart.Text;
        text.Should().Contain("hello from main.py",
            "main.py output must be captured between soft-reset and the next REPL prompt");
    }

    /// <summary>
    /// Verifies that arithmetic computed in <c>main.py</c> is output correctly
    /// (sanity-checks that the script interpreter runs fully).
    /// </summary>
    [Fact]
    public async Task Script_MainPy_ComputesAndPrintsArithmetic()
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt().Should().BeTrue();

        runner.WriteFile("main.py", "x = 6 * 7\nprint('result:', x)\n")
              .Should().BeTrue();

        runner.SoftReset(timeoutMs: 20_000)
              .Should().BeTrue();

        var text = runner.UsbCdc.IsConnected ? runner.UsbCdc.Text : runner.Uart.Text;
        text.Should().Contain("result: 42",
            "main.py should execute and print the computed value");
    }

    // ── boot.py + main.py ─────────────────────────────────────────────────────

    /// <summary>
    /// Writes both <c>boot.py</c> and <c>main.py</c> and verifies the ordering of their
    /// output: <c>boot.py</c> always runs before <c>main.py</c> on a MicroPython soft reset.
    /// </summary>
    [Fact]
    public async Task Script_BootPy_RunsBeforeMainPy()
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt().Should().BeTrue();

        runner.WriteFile("boot.py",  "print('--- boot.py ---')\n") .Should().BeTrue();
        runner.WriteFile("main.py",  "print('--- main.py ---')\n") .Should().BeTrue();

        runner.SoftReset(timeoutMs: 20_000)
              .Should().BeTrue();

        var text = runner.UsbCdc.IsConnected ? runner.UsbCdc.Text : runner.Uart.Text;

        text.Should().Contain("--- boot.py ---", "boot.py must run on soft reset");
        text.Should().Contain("--- main.py ---", "main.py must run on soft reset");

        var bootIdx = text.IndexOf("--- boot.py ---", StringComparison.Ordinal);
        var mainIdx = text.IndexOf("--- main.py ---", StringComparison.Ordinal);
        bootIdx.Should().BeLessThan(mainIdx, "boot.py output must precede main.py output");
    }
}
