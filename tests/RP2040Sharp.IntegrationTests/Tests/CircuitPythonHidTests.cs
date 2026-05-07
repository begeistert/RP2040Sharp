using FluentAssertions;
using RP2040Sharp.IntegrationTests.Infrastructure;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Tests for the USB HID host driver with CircuitPython.
///
/// CircuitPython exposes a composite USB configuration that includes a HID interface
/// (keyboard + mouse reports) alongside CDC and MSC.  These tests verify:
/// <list type="bullet">
///   <item>The HID interface is enumerated after SET_CONFIGURATION.</item>
///   <item>CircuitPython can be directed (via REPL) to send a keyboard HID report,
///         and the report is captured by <see cref="RP2040.TestKit.Probes.UsbHidProbe"/>.</item>
/// </list>
///
/// All tests are network-gated: when <c>SKIP_INTEGRATION_TESTS=1</c> they return early
/// (still pass) so CI builds without internet access are not affected.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CircuitPythonHidTests
{
    private static bool ShouldSkip =>
        Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "1";

    private const string Version = "9.2.1";

    // ── HID enumeration ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the USB HID interface is detected during descriptor parsing.
    /// CircuitPython 9.2.1 on Pico includes a composite HID interface (keyboard + mouse)
    /// in its default USB configuration.
    /// </summary>
    [Fact]
    public async Task Hid_Enumeration_InterfaceDiscoveredAfterCdcInit()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue("CDC REPL must be ready before checking HID");

        var hid = runner.Simulation.UsbHid;

        // HID connection is signalled synchronously with OnConfigurationComplete.
        // By the time WaitForPrompt returns, HID should already be connected.
        hid.IsConnected.Should().BeTrue(
            "CircuitPython's composite descriptor includes a HID interface; " +
            "UsbHidHost should detect it during enumeration");
    }

    // ── HID report capture ────────────────────────────────────────────────────

    /// <summary>
    /// Directs CircuitPython's <c>usb_hid</c> module (via the REPL) to send a keyboard
    /// HID report and verifies that the report is captured by the host probe.
    /// </summary>
    [Fact]
    public async Task Hid_KeyboardReport_CapturedByProbe()
    {
        if (ShouldSkip) return;

        await using var runner = await CircuitPythonRunner.CreateAsync(Version);
        if (runner is null) return;

        runner.WaitForPrompt(timeoutMs: 20_000)
              .Should().BeTrue("CDC REPL must be ready");

        var hid = runner.Simulation.UsbHid;
        hid.IsConnected.Should().BeTrue("HID interface must be enumerated");

        hid.Clear();

        // Ask CircuitPython to send a single key-press ('a') via usb_hid.
        // The report format is: [modifier, 0, keycode, 0, 0, 0, 0, 0] (boot keyboard).
        runner.Execute(
            "import usb_hid\n" +
            "kb = usb_hid.devices[0]\n" +
            "kb.send_report(bytes([0,0,4,0,0,0,0,0]))\n");  // keycode 4 = 'a'

        var elapsed = 0.0;
        while (hid.ReportCount == 0 && elapsed < 5_000)
        {
            runner.Simulation.RunMilliseconds(100);
            elapsed += 100;
        }

        hid.ReportCount.Should().BeGreaterThan(0, "CircuitPython must have sent at least one HID report");
        hid.LatestReport.Should().HaveCountGreaterThanOrEqualTo(8, "keyboard boot report is 8 bytes");
    }
}
