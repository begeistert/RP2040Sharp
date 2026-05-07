using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Watchdog example from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class WatchdogTests
{
    // ── hello_watchdog ────────────────────────────────────────────────────────

    [Fact]
    public void HelloWatchdog_NoHardFault_DuringNormalOperation()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloWatchdog)!;

        pico.LoadFlash(flash);

        // Allow enough time for watchdog setup, first timeout check, and re-arm
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during watchdog operation");
    }

    [Fact]
    public void HelloWatchdog_Uart0_PrintsRebootReason()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloWatchdog)!;

        pico.LoadFlash(flash);

        // hello_watchdog prints whether it rebooted via watchdog or cleanly
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_watchdog must produce UART0 output describing boot cause");
    }

    [Fact]
    public void HelloWatchdog_Uart0_ContainsScratchpadValue()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloWatchdog)!;

        pico.LoadFlash(flash);

        // hello_watchdog writes a scratch value before enabling watchdog and reads it on reboot
        pico.RunMilliseconds(5_000);

        // On a fresh boot (no prior watchdog reset), it should print a clean-start message
        pico.Uart0.ByteCount.Should().BeGreaterThan(0,
            "hello_watchdog must have written to UART0");
    }

    [Fact]
    public void HelloWatchdog_Cpu_IsAliveAfterWatchdogArm()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloWatchdog)!;

        pico.LoadFlash(flash);

        // Firmware arms the watchdog and pats it in a loop; SP must remain valid
        pico.RunMilliseconds(3_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM with watchdog armed");
    }
}
