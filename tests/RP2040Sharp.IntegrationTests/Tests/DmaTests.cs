using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for DMA examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DmaTests
{
    // ── hello_dma ─────────────────────────────────────────────────────────────

    [Fact]
    public void HelloDma_NoHardFault_AfterTransfer()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloDma)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during DMA transfer");
    }

    [Fact]
    public void HelloDma_Uart0_ProducesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloDma)!;

        pico.LoadFlash(flash);

        // hello_dma copies data and prints a status/result line over UART0
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_dma must output a result over UART0 after the DMA transfer");
    }

    [Fact]
    public void HelloDma_Cpu_IsAliveAfterCompletion()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloDma)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after DMA transfer");
    }

    // ── dma_channel_irq ───────────────────────────────────────────────────────

    [Fact]
    public void DmaChannelIrq_NoHardFault_AfterTransfer()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.DmaChannelIrq)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in DMA IRQ example");
    }

    [Fact]
    public void DmaChannelIrq_Cpu_IsAliveAfterRepeatedTransfers()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.DmaChannelIrq)!;

        pico.LoadFlash(flash);

        // dma_channel_irq uses DMA → PIO → LED (no UART output).
        // The IRQ handler restarts the DMA channel in a loop — verify CPU stays alive.
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u,
            "HardFault must not occur during repeated DMA IRQ-driven PIO transfers");
        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after repeated DMA IRQ rounds");
    }
}
