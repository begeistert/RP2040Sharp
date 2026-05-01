using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests that boot real MicroPython firmware on the RP2040 emulator
/// and verify the REPL prompt and basic output via UART.
///
/// These tests require network access on the first run to download the firmware.
/// Subsequent runs use the cached UF2 in the system temp directory.
///
/// Set environment variable SKIP_INTEGRATION_TESTS=1 to skip all tests in CI pipelines
/// that cannot access GitHub Releases.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MicroPythonBootTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    [Theory]
    [InlineData("v1.19.1")]
    [InlineData("v1.20.0")]
    [InlineData("v1.21.0")]
    public async Task MicroPython_BootsAndShowsReplPrompt(string version)
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(version);
        if (runner is null) return; // firmware unavailable - skip gracefully

        var booted = runner.WaitForPrompt(timeoutMs: 15_000);

        booted.Should().BeTrue(
            $"MicroPython {version} should produce a REPL prompt within 15 seconds of simulated time");
    }

    [Theory]
    [InlineData("v1.19.1")]
    [InlineData("v1.20.0")]
    [InlineData("v1.21.0")]
    public async Task MicroPython_OutputsVersionHeader(string version)
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(version);
        if (runner is null) return;

        runner.WaitForPrompt();

        runner.Uart.Text.Should()
            .Contain("MicroPython", "the version banner should appear during boot");
    }

    [Theory]
    [InlineData("v1.19.1")]
    [InlineData("v1.20.0")]
    [InlineData("v1.21.0")]
    public async Task MicroPython_OutputsHelloWorld(string version)
    {
        if (ShouldSkip) return;

        await using var runner = await MicroPythonRunner.CreateAsync(version);
        if (runner is null) return;

        var booted = runner.WaitForPrompt();
        booted.Should().BeTrue($"MicroPython {version} must reach REPL before executing code");

        var found = runner.ExecuteAndWait("print('Hello, MicroPython!')", "Hello, MicroPython!");

        found.Should().BeTrue("print() output should appear on UART");
    }
}
