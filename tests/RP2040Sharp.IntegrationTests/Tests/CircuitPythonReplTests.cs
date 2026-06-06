using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// REPL-level integration tests for CircuitPython on the RP2040 emulator.
///
/// The key test in this suite exercises the official Adafruit "blink LED" example:
///
/// <code>
/// # SPDX-FileCopyrightText: 2021 Kattni Rembor for Adafruit Industries
/// # SPDX-License-Identifier: MIT
/// """Example for Pico. Turns on the built-in LED."""
/// import board
/// import digitalio
///
/// led = digitalio.DigitalInOut(board.LED)
/// led.direction = digitalio.Direction.OUTPUT
///
/// while True:
///     led.value = True
/// </code>
///
/// The <c>while True</c> loop is intentionally not injected; instead the test
/// verifies that the LED (GPIO 25) is driven high after <c>led.value = True</c>
/// executes, which is the meaningful observable behaviour of the snippet.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CircuitPythonReplTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    private const string Version = "9.2.1";

    private static async Task<CircuitPythonRunner?> BootToReplAsync()
    {
        var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return null;
        runner.WaitForPrompt(timeoutMs: 20_000)
            .Should().BeTrue($"CircuitPython {Version} must reach REPL within 20 s");
        // Run 200 ms of simulation to drain any pending USB ZLP reads that
        // accumulated during WaitForPrompt; without this the first Execute()
        // may only deliver a partial command to the firmware.
        runner.Simulation.RunMilliseconds(200);
        runner.UsbCdc.Clear();
        return runner;
    }

    // ── Basic arithmetic ──────────────────────────────────────────────────────

    [Fact]
    public async Task Repl_CanEvaluateArithmeticExpression()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        var found = runner.ExecuteAndWait("print(1 + 2)", "3");
        found.Should().BeTrue("1 + 2 should evaluate to 3");
    }

    // ── sys.version ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Repl_CanReadSysVersion()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        var found = runner.ExecuteAndWait("import sys; print(sys.version)", "CircuitPython");
        found.Should().BeTrue("sys.version should contain 'CircuitPython'");
    }

    // ── Function definition ───────────────────────────────────────────────────

    [Fact]
    public async Task Repl_CanDefineAndCallFunction()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        runner.ExecuteCompound("def greet(name): return 'Hi ' + name");

        var found = runner.ExecuteAndWait("print(greet('world'))", "Hi world");
        found.Should().BeTrue("user-defined function should be callable from REPL");
    }

    // ── Variable state ────────────────────────────────────────────────────────

    [Fact]
    public async Task Repl_MultipleCommands_ProduceCorrectOutput()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        runner.ExecuteAndWait("x = 10", ">>> ");
        runner.ExecuteAndWait("y = 32", ">>> ");
        var found = runner.ExecuteAndWait("print(x + y)", "42");
        found.Should().BeTrue("accumulated variable state should be preserved across REPL lines");
    }

    // ── Adafruit LED example ──────────────────────────────────────────────────
    //
    //   SPDX-FileCopyrightText: 2021 Kattni Rembor for Adafruit Industries
    //   SPDX-License-Identifier: MIT
    //
    //   Original: "Example for Pico. Turns on the built-in LED."
    //
    //   The infinite loop (while True: led.value = True) is omitted intentionally
    //   because it would never yield back to the REPL; all meaningful state
    //   is established before it.

    /// <summary>
    /// Imports <c>board</c> and <c>digitalio</c>, creates a DigitalInOut on <c>board.LED</c>
    /// (GPIO 25), sets its direction to OUTPUT, and asserts that GPIO 25 reads high after
    /// assigning <c>led.value = True</c>.
    /// </summary>
    [Fact]
    public async Task Repl_LedCode_SetsBoardLedHigh()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        // import board
        runner.Execute("import board");
        runner.WaitForPrompt(timeoutMs: 3_000)
            .Should().BeTrue("'import board' should not raise an error");

        // import digitalio
        runner.Execute("import digitalio");
        runner.WaitForPrompt(timeoutMs: 3_000)
            .Should().BeTrue("'import digitalio' should not raise an error");

        // led = digitalio.DigitalInOut(board.LED)
        runner.Execute("led = digitalio.DigitalInOut(board.LED)");
        runner.WaitForPrompt(timeoutMs: 3_000)
            .Should().BeTrue("DigitalInOut constructor should succeed on board.LED (GPIO 25)");

        // led.direction = digitalio.Direction.OUTPUT
        runner.Execute("led.direction = digitalio.Direction.OUTPUT");
        runner.WaitForPrompt(timeoutMs: 3_000)
            .Should().BeTrue("Setting direction to OUTPUT should succeed");

        // led.value = True  — this is the observable action of the snippet
        runner.Execute("led.value = True");
        runner.WaitForPrompt(timeoutMs: 3_000)
            .Should().BeTrue("Setting led.value = True should succeed");

        // Allow any pending GPIO writes to propagate
        runner.Simulation.RunMilliseconds(10);

        // GPIO 25 is the onboard LED on the Pico; it should now be driven high
        runner.Simulation.Gpio[25].Should().BeHigh(
            "board.LED (GPIO 25) must be high after led.value = True");
    }

    /// <summary>
    /// Verifies that the LED can be turned off after being turned on — tests the
    /// complementary path of the same digitalio pattern.
    /// </summary>
    [Fact]
    public async Task Repl_LedCode_ClearsBoardLedLow()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        runner.Execute("import board");
        runner.WaitForPrompt(timeoutMs: 3_000);
        runner.Execute("import digitalio");
        runner.WaitForPrompt(timeoutMs: 3_000);
        runner.Execute("led = digitalio.DigitalInOut(board.LED)");
        runner.WaitForPrompt(timeoutMs: 3_000);
        runner.Execute("led.direction = digitalio.Direction.OUTPUT");
        runner.WaitForPrompt(timeoutMs: 3_000);

        runner.Execute("led.value = True");
        runner.WaitForPrompt(timeoutMs: 3_000);
        runner.Execute("led.value = False");
        runner.WaitForPrompt(timeoutMs: 3_000);

        runner.Simulation.RunMilliseconds(10);

        runner.Simulation.Gpio[25].Should().BeLow(
            "GPIO 25 must be low after led.value = False");
    }

    // ── board module ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Repl_BoardModule_ExposesLedPin()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        runner.Execute("import board");
        runner.WaitForPrompt(timeoutMs: 3_000);

        // board.LED should be a valid pin object (its repr contains "GP25" or "LED")
        // In CircuitPython 9.x on Pico, print(board.LED) outputs "board.LED" (the attribute path).
        var found = runner.ExecuteAndWait("print(board.LED)", "board.LED");
        found.Should().BeTrue("board.LED should print 'board.LED'");
    }

    // ── Multiline for-loop ────────────────────────────────────────────────────

    [Fact]
    public async Task Repl_ForLoop_PrintsAllLines()
    {
        if (ShouldSkip) return;

        await using var runner = await BootToReplAsync();
        if (runner is null) return;

        runner.ExecuteCompound("for i in range(3): print('line', i)");
        var found = runner.WaitForOutput(text =>
            text.Contains("line 0") && text.Contains("line 1") && text.Contains("line 2"));

        found.Should().BeTrue("all three for-loop iterations should appear in output");
    }
}
