using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for multicore examples from pico-examples.
/// </summary>
/// <remarks>
/// NOTE: Core1 is NOT yet implemented in RP2040Sharp — only Core0 exists.
/// The multicore launch sequence sends a handshake over SIO FIFO; without Core1
/// to respond, Core0 loops forever in the launch stub. All tests in this class
/// are skipped until Core1 emulation is added.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "NotImplemented")]
public sealed class MulticoreTests
{
    private const string SkipReason =
        "Core1 is not implemented in RP2040Sharp. " +
        "The launch sequence hangs waiting for Core1's FIFO acknowledgement.";

    // ── hello_multicore ───────────────────────────────────────────────────────

    [Fact(Skip = SkipReason)]
    public void HelloMulticore_NoHardFault_AfterCore1Launch()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);

        // Allow time for core 0 to launch core 1 via SIO FIFO
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur after core 1 launch");
    }

    [Fact(Skip = SkipReason)]
    public void HelloMulticore_Uart0_ContainsCoreMessages()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);

        // hello_multicore: core 0 sends a value to core 1, core 1 squares it and returns
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_multicore must produce UART0 output after inter-core communication");
    }

    [Fact(Skip = SkipReason)]
    public void HelloMulticore_Cpu_IsAliveAfterRendezvous()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_0000u,
            "SP must remain valid after multicore rendezvous");
    }

    // ── multicore_fifo_irqs ───────────────────────────────────────────────────

    [Fact(Skip = SkipReason)]
    public void MulticoreFifoIrqs_NoHardFault_AfterStart()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.MulticoreFifoIrqs)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in FIFO IRQ example");
    }

    [Fact(Skip = SkipReason)]
    public void MulticoreFifoIrqs_Uart0_ProducesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.MulticoreFifoIrqs)!;

        pico.LoadFlash(flash);

        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("multicore_fifo_irqs must produce UART0 output after IRQ-driven FIFO exchange");
    }

    [Fact(Skip = SkipReason)]
    public void MulticoreFifoIrqs_Cpu_IsAliveAfterMultipleIrqs()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.MulticoreFifoIrqs)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_0000u,
            "SP must remain valid after multiple FIFO IRQ rounds");
    }
}
