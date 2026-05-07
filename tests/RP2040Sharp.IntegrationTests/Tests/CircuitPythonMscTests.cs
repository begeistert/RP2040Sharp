using FluentAssertions;
using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Tests for the USB Mass Storage Class (MSC) host driver with CircuitPython.
///
/// CircuitPython exposes its CIRCUITPY FAT filesystem as a USB MSC device alongside its
/// CDC REPL.  These tests verify:
/// <list type="bullet">
///   <item>The MSC interface is enumerated (TEST_UNIT_READY + READ_CAPACITY succeed).</item>
///   <item>Individual sectors can be read via the BOT protocol.</item>
///   <item>The FAT VBR (sector 0) is parseable and reports a valid disk geometry.</item>
///   <item>Writing a file via MSC (<see cref="CircuitPythonRunner.WriteFileViaMsc"/>) and
///         then running a soft reset causes CircuitPython to execute the new script.</item>
/// </list>
///
/// All tests are network-gated: when <c>SKIP_INTEGRATION_TESTS=1</c> they return early
/// (still pass) so CI builds without internet access are not affected.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CircuitPythonMscTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    private const string Version = "9.2.1";

    // ── MSC enumeration ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the USB MSC interface is enumerated alongside the CDC REPL:
    /// TEST_UNIT_READY and READ_CAPACITY complete successfully.
    /// </summary>
    [Fact]
    public async Task Msc_Enumeration_SucceedsAlongsideCdc()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue("CDC REPL must be ready before checking MSC");

        // After the REPL is ready the MSC stack in CircuitPython should also be
        // initialised.  Allow a little extra time for the BOT init sequence to complete.
        var msc = runner.Simulation.UsbMsc;
        var elapsed = 0.0;
        while (!msc.IsConnected && elapsed < 5_000)
        {
            runner.Simulation.RunMilliseconds(100);
            elapsed += 100;
        }

        msc.IsConnected.Should().BeTrue("MSC initialisation (TEST_UNIT_READY + READ_CAPACITY) must complete");
        msc.BlockSize.Should().Be(512, "standard FAT sector size");
        msc.BlockCount.Should().BeGreaterThan(0, "disk must have at least one block");
    }

    // ── Sector read ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reads sector 0 (the FAT VBR) via USB MSC and verifies it contains a valid
    /// FAT boot sector signature (0x55 0xAA at bytes 510–511).
    /// </summary>
    [Fact]
    public async Task Msc_ReadSector0_ContainsFatBootSignature()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue("CDC REPL must be ready");

        var msc = runner.Simulation.UsbMsc;
        var elapsed = 0.0;
        while (!msc.IsConnected && elapsed < 5_000)
        {
            runner.Simulation.RunMilliseconds(100);
            elapsed += 100;
        }
        msc.IsConnected.Should().BeTrue();

        byte[]? sector0 = null;
        msc.RequestRead(0, data => sector0 = data);

        elapsed = 0.0;
        while (sector0 is null && elapsed < 5_000)
        {
            runner.Simulation.RunMilliseconds(100);
            elapsed += 100;
        }

        sector0.Should().NotBeNull("READ(10) of LBA 0 must complete");
        sector0![510].Should().Be(0x55, "FAT boot sector signature byte 510 must be 0x55");
        sector0![511].Should().Be(0xAA, "FAT boot sector signature byte 511 must be 0xAA");
    }

    // ── File write via MSC ────────────────────────────────────────────────────

    /// <summary>
    /// Writes a custom <c>code.py</c> via the USB MSC FAT path, then performs a soft
    /// reset and verifies the new script's output appears on the CDC channel.
    ///
    /// This test exercises the full write path:
    ///   UsbMscHost → TinyUSB tud_msc_write10_cb → flash_range_program hook → PtrFlash
    /// and confirms the persisted flash is re-executed by CircuitPython's boot sequence.
    /// </summary>
    [Fact]
    public async Task Msc_WriteCodePy_PersistsAcrossSoftReset()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue("CDC REPL must be ready before MSC write");

        var msc = runner.Simulation.UsbMsc;
        var elapsed = 0.0;
        while (!msc.IsConnected && elapsed < 5_000)
        {
            runner.Simulation.RunMilliseconds(100);
            elapsed += 100;
        }
        msc.IsConnected.Should().BeTrue("MSC must be connected before writing");

        const string script = "print('hello from msc')\n";
        runner.WriteFileViaMsc("code.py", script, timeoutMs: 30_000)
              .Should().BeTrue("WriteFileViaMsc must succeed");

        runner.UsbCdc.Clear();
        runner.SoftReset(timeoutMs: 20_000)
              .Should().BeTrue("CircuitPython must return to REPL after soft reset");

        runner.UsbCdc.Text
              .Should().Contain("hello from msc",
                  "the MSC-written code.py must execute on soft reset");
    }

    // ── MSC + CDC coexistence ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that MSC and CDC work concurrently: the CDC REPL remains responsive
    /// while MSC READ operations are in progress.
    /// </summary>
    [Fact]
    public async Task Msc_CdcRemainsResponsiveDuringMscRead()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue("CDC REPL must be ready");

        var msc = runner.Simulation.UsbMsc;
        var elapsed = 0.0;
        while (!msc.IsConnected && elapsed < 5_000)
        {
            runner.Simulation.RunMilliseconds(100);
            elapsed += 100;
        }
        msc.IsConnected.Should().BeTrue();

        // Issue a sector read while simultaneously exercising the REPL.
        byte[]? sector = null;
        msc.RequestRead(0, data => sector = data);

        runner.UsbCdc.Clear();
        runner.Execute("1+1");
        runner.WaitForPrompt(timeoutMs: 5_000)
              .Should().BeTrue("CDC REPL must remain responsive during MSC reads");

        runner.UsbCdc.Text.Should().Contain("2");

        // Sector should also have arrived.
        runner.Simulation.RunMilliseconds(500);
        sector.Should().NotBeNull("MSC read must complete even while CDC is active");
    }
}
