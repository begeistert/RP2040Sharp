using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Clock examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClocksTests
{
    // ── hello_48MHz ───────────────────────────────────────────────────────────

    [Fact]
    public void Hello48MHz_NoHardFault_AfterClockSwitch()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.Hello48MHz)!;

        pico.LoadFlash(flash);

        // The firmware reconfigures the system clock to 48 MHz (from 125 MHz)
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur after clock reconfiguration");
    }

    [Fact]
    public void Hello48MHz_Uart0_PrintsFrequencyInfo()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.Hello48MHz)!;

        pico.LoadFlash(flash);

        // hello_48MHz prints the measured clock frequencies after the switch
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_48MHz must print clock frequency info over UART0");
    }

    [Fact]
    public void Hello48MHz_Cpu_SurvivesClockReconfiguration()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.Hello48MHz)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after clock source switch");
        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "no HardFault after clock change");
    }
}
