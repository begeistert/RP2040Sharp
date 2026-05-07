using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the Reset example from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResetTests
{
    // ── hello_reset ───────────────────────────────────────────────────────────

    [Fact]
    public void HelloReset_NoHardFault_AfterPeripheralReset()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloReset)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during peripheral reset");
    }

    [Fact]
    public void HelloReset_Uart0_ProducesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloReset)!;

        pico.LoadFlash(flash);

        // hello_reset releases and re-claims the UART/SPI peripheral via the RESETS block
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_reset must produce UART0 output after peripheral re-init");
    }

    [Fact]
    public void HelloReset_Cpu_IsAliveAfterPeripheralRelease()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloReset)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after peripheral reset/re-init cycle");
    }
}
