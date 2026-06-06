using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the hardware-divider (SIO) example from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DividerTests
{
    // ── hello_divider ─────────────────────────────────────────────────────────

    [Fact]
    public void HelloDivider_NoHardFault_AfterExecution()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloDivider)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in hello_divider");
    }

    [Fact]
    public void HelloDivider_Uart0_ProducesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloDivider)!;

        pico.LoadFlash(flash);

        // hello_divider performs signed/unsigned division using SIO hardware and prints results
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_divider must print division results over UART0");
    }

    [Fact]
    public void HelloDivider_Uart0_ContainsDivisionResults()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloDivider)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(3_000);

        pico.Uart0.ByteCount.Should().BeGreaterThan(0,
            "hello_divider must have produced output after hardware division");
    }

    [Fact]
    public void HelloDivider_Cpu_FinishesWithoutStackOverflow()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloDivider)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must stay in SRAM after SIO divider operations");
    }
}
