using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Timer examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TimerTests
{
    // ── hello_timer ───────────────────────────────────────────────────────────

    [Fact]
    public void HelloTimer_NoHardFault_AfterStartup()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloTimer)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur");
    }

    [Fact]
    public void HelloTimer_Uart0_ReceivesTimerFiredOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloTimer)!;

        pico.LoadFlash(flash);

        // hello_timer fires a repeating callback every 1 s and prints to UART0
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_timer must produce UART output after timer fires");
    }

    [Fact]
    public void HelloTimer_Uart0_ReceivesMultipleFirings()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloTimer)!;

        pico.LoadFlash(flash);

        // Run long enough for multiple timer firings (callback every 1 s)
        pico.RunMilliseconds(4_000);

        pico.Uart0.Lines.Count.Should().BeGreaterThan(2,
            "hello_timer should fire at least 3 times in 4 seconds");
    }

    [Fact]
    public void HelloTimer_Cpu_IsAliveAfter3Seconds()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloTimer)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(3_000);

        // SP must remain in valid SRAM range (not corrupted)
        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "stack pointer must stay within SRAM after timer callbacks");
    }

    // ── timer_lowlevel ────────────────────────────────────────────────────────

    [Fact]
    public void TimerLowlevel_NoHardFault_AfterStartup()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.TimerLowlevel)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in timer_lowlevel");
    }

    [Fact]
    public void TimerLowlevel_Uart0_HasOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.TimerLowlevel)!;

        pico.LoadFlash(flash);

        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("timer_lowlevel must produce output after a hardware timer fires");
    }
}
