using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for RTC examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RtcTests
{
    // ── hello_rtc ─────────────────────────────────────────────────────────────

    [Fact]
    public void HelloRtc_NoHardFault_AfterStartup()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloRtc)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during RTC init");
    }

    [Fact]
    public void HelloRtc_Uart0_PrintsDateTime()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloRtc)!;

        pico.LoadFlash(flash);

        // hello_rtc sets the RTC to a fixed date/time and then prints it via UART0
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_rtc must output date/time over UART0");
    }

    [Fact]
    public void HelloRtc_Uart0_PrintsMultipleTicks()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloRtc)!;

        pico.LoadFlash(flash);

        // hello_rtc uses printf("\r%s ", datetime) with 100ms sleep — no newlines.
        // After 2 seconds (20 ticks at 100ms), the raw UART text should be non-trivial.
        pico.RunMilliseconds(2_000);

        pico.Uart0.Text.Should().NotBeEmpty("hello_rtc should produce datetime output");
        pico.Uart0.Text.Length.Should().BeGreaterThan(20,
            "hello_rtc should produce repeated datetime output over 2 seconds");
    }

    [Fact]
    public void HelloRtc_Cpu_IsAliveAfterSeveralSeconds()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloRtc)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(3_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM while RTC loop is running");
    }

    // ── rtc_alarm ─────────────────────────────────────────────────────────────

    [Fact]
    public void RtcAlarm_NoHardFault_AfterFiring()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.RtcAlarm)!;

        pico.LoadFlash(flash);

        // The alarm is set a few seconds into the future; run long enough for it to fire
        pico.RunMilliseconds(10_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur when RTC alarm fires");
    }

    [Fact]
    public void RtcAlarm_Uart0_PrintsAlarmFired()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.RtcAlarm)!;

        pico.LoadFlash(flash);

        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 15_000);

        found.Should().BeTrue("rtc_alarm must produce UART0 output after the alarm fires");
    }
}
