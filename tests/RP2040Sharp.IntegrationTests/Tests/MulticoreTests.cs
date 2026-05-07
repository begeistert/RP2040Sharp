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
/// to respond, Core0 loops forever in the launch stub. All multicore-dependent
/// tests are skipped until Core1 emulation is added.
///
/// The "Core0Boot" tests do not require Core1 and verify that the firmware starts
/// correctly on Core0 before it attempts the inter-core handshake.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class MulticoreTests
{
    private const string SkipReason =
        "Core1 is not implemented in RP2040Sharp. " +
        "The launch sequence hangs waiting for Core1's FIFO acknowledgement.";

    // ── Core0 boot (no Core1 needed) ─────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>hello_multicore</c> boots Core0 without a HardFault or CPU lockup
    /// in the brief window before it attempts to launch Core1.
    /// This test does NOT wait for the inter-core rendezvous.
    /// </summary>
    [Fact]
    public void HelloMulticore_Core0_BootsCleanly()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);

        // Run only long enough to confirm reset + clock init completes on Core0,
        // but short enough that the FIFO-wait loop hasn't consumed all budget.
        pico.RunMilliseconds(50);

        pico.Cpu.IsLockedUp.Should().BeFalse(
            "hello_multicore Core0 must not lock up during early init");
        // RP2040 SRAM: 264 KB = 0x20000000–0x20041FFF; stack top = 0x20042000
        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must be in SRAM after Core0 reset handler");
    }

    /// <summary>
    /// Verifies that <c>multicore_fifo_irqs</c> boots Core0 without a HardFault or lockup.
    /// </summary>
    [Fact]
    public void MulticoreFifoIrqs_Core0_BootsCleanly()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.MulticoreFifoIrqs)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(50);

        pico.Cpu.IsLockedUp.Should().BeFalse(
            "multicore_fifo_irqs Core0 must not lock up during early init");
        // RP2040 SRAM: 264 KB = 0x20000000–0x20041FFF; stack top = 0x20042000
        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must be in SRAM after Core0 reset handler");
    }

    // ── Full multicore tests (skipped until Core1 is implemented) ─────────────

    [Fact(Skip = SkipReason)]
    public void HelloMulticore_NoHardFault_AfterCore1Launch()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);

        // Allow time for core 0 to launch core 1 via SIO FIFO
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur after core 1 launch");
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up after core 1 launch");
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
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up after multicore rendezvous");
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
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up in FIFO IRQ example");
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
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up after FIFO IRQ rounds");
    }
}
