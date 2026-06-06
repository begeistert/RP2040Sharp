using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for multicore examples from pico-examples.
/// Core 1 is launched by the firmware via the SIO FIFO multicore handshake
/// (RP2040 datasheet §2.8.3).  The emulator now implements this handshake natively
/// in <see cref="RP2040.Peripherals.Sio.SioPeripheral"/>: Core 0's 6-word launch
/// sequence (0, 0, 1, VTOR, SP, Entry) is echoed back immediately, and Core 1
/// is configured and started when the sequence completes.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MulticoreTests
{
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

    // ── Full multicore tests ──────────────────────────────────────────────────

    [Fact]
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

    [Fact]
    public void HelloMulticore_Uart0_ContainsCoreMessages()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);

        // hello_multicore: core 0 sends a value to core 1, core 1 squares it and returns
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_multicore must produce UART0 output after inter-core communication");
    }

    [Fact]
    public void HelloMulticore_Cpu_IsAliveAfterRendezvous()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain valid after multicore rendezvous");
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up after multicore rendezvous");
    }

    // ── multicore_fifo_irqs ───────────────────────────────────────────────────

    [Fact]
    public void MulticoreFifoIrqs_NoHardFault_AfterStart()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.MulticoreFifoIrqs)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in FIFO IRQ example");
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up in FIFO IRQ example");
    }

    [Fact]
    public void MulticoreFifoIrqs_Uart0_ProducesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.MulticoreFifoIrqs)!;

        pico.LoadFlash(flash);

        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("multicore_fifo_irqs must produce UART0 output after IRQ-driven FIFO exchange");
    }

    [Fact]
    public void MulticoreFifoIrqs_Cpu_IsAliveAfterMultipleIrqs()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.MulticoreFifoIrqs)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain valid after multiple FIFO IRQ rounds");
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up after FIFO IRQ rounds");
    }

    // ── Dual-core timing ──────────────────────────────────────────────────────

    /// <summary>
    /// Both cores run in parallel on real hardware, so the wall-clock time advanced by a
    /// single <c>Run(n)</c> must be <c>max(core0, core1)</c> — never the sum of both.
    /// Before the fix, summing the two cores' cycles made time-aware peripherals (timer,
    /// PWM, …) run at up to double speed whenever Core 1 was active.
    /// </summary>
    [Fact]
    public void DualCore_ElapsedTime_IsMaxNotSum()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloMulticore)!;

        pico.LoadFlash(flash);

        // Run in fixed batches until Core 0 launches Core 1 via the SIO FIFO handshake.
        const int batch = 100_000;
        var launched = false;
        for (var i = 0; i < 1000 && !launched; i++)   // up to ~100M cycles (~0.8 s @125 MHz)
        {
            pico.Rp2040.Run(batch);
            launched = pico.Rp2040.Core1Launched;
        }

        launched.Should().BeTrue("the test needs both cores active");

        // Both cores run in parallel on real hardware, so the wall-clock advanced by a
        // single Run must be exactly max(core0, core1). The pre-fix code used Core 0's
        // cycles alone, which underran the clock whenever Core 1 did more work.
        var c0Before = pico.Cpu.Cycles;
        var c1Before = pico.Cpu1.Cycles;

        pico.Rp2040.Run(batch);

        var d0 = pico.Cpu.Cycles - c0Before;
        var d1 = pico.Cpu1.Cycles - c1Before;
        pico.Rp2040.LastElapsedCycles.Should().Be(Math.Max(d0, d1),
            "elapsed time is max(core0, core1) — neither Core 0 alone nor the sum");
    }
}

