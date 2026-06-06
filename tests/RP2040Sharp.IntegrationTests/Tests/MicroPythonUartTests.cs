using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Tests that verify MicroPython UART output — printing integers, strings, and multi-line output.
/// These target the emulated UART TX path and confirm the entire pipeline from Python print()
/// to the UartProbe capture.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MicroPythonUartTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    private const string Version = "v1.21.0";

    [Fact]
    public async Task Uart_PrintInteger_AppearsInCapture()
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt().Should().BeTrue();

        var found = runner.ExecuteAndWait("print(12345)", "12345");
        found.Should().BeTrue();
    }

    [Fact]
    public async Task Uart_PrintMultipleLines_AllCaptured()
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt().Should().BeTrue();

        // for-loop is a compound statement in MicroPython REPL; ExecuteCompound
        // sends the statement, waits for "... " continuation, then sends a blank
        // line to execute it, and waits for the next ">>> " prompt.
        runner.ExecuteCompound("for i in range(3): print('line', i)");
        var found = runner.WaitForOutput(text =>
            text.Contains("line 0") && text.Contains("line 1") && text.Contains("line 2"));

        found.Should().BeTrue("all three lines from the for-loop should appear on UART");
    }

    [Fact]
    public async Task Uart_PrintBytes_HexRepresentationCaptured()
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt().Should().BeTrue();

        var found = runner.ExecuteAndWait("print(bytes([0xDE, 0xAD]))", "\\xde\\xad");
        found.Should().BeTrue("bytes literal should print as expected hex escape");
    }

    [Fact]
    public async Task Uart_MachinePinToggle_OutputsMessage()
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt().Should().BeTrue();

        // Toggle GPIO 25 (onboard LED on Pico) and verify no exception is thrown
        runner.Execute("from machine import Pin");
        runner.WaitForPrompt();
        runner.Execute("led = Pin(25, Pin.OUT)");
        runner.WaitForPrompt();
        runner.Execute("led.toggle(); print('toggled')");
        var found = runner.WaitForOutput("toggled");

        found.Should().BeTrue("GPIO toggle should complete without error");
    }
}
