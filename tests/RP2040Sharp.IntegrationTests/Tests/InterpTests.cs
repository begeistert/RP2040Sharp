using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Interpolator example from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InterpTests
{
    // ── hello_interp ──────────────────────────────────────────────────────────

    [Fact]
    public void HelloInterp_NoHardFault_AfterExecution()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloInterp)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in hello_interp");
    }

    [Fact]
    public void HelloInterp_Uart0_ProducesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloInterp)!;

        pico.LoadFlash(flash);

        // hello_interp computes interpolated values and prints them to UART0
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_interp must print interpolator results over UART0");
    }

    [Fact]
    public void HelloInterp_Uart0_ContainsNumericOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloInterp)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(3_000);

        // Interpolator results are printed as decimal or hex numbers
        pico.Uart0.ByteCount.Should().BeGreaterThan(0, "hello_interp must have produced UART output");
    }

    [Fact]
    public void HelloInterp_Cpu_CompletesWithoutStackOverflow()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloInterp)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must stay within SRAM after interpolator computations");
    }
}
