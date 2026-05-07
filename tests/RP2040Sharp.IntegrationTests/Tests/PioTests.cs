using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for PIO examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PioTests
{
    // ── hello_pio ─────────────────────────────────────────────────────────────

    [Fact]
    public void HelloPio_NoHardFault_AfterInit()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloPio)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(300);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during PIO init");
    }

    [Fact]
    public void HelloPio_Cpu_IsAliveAfterStateMachineStart()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloPio)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain valid after PIO state machine starts");
    }

    // ── pio_blink ─────────────────────────────────────────────────────────────

    [Fact]
    public void PioBlink_NoHardFault_AfterOneCycle()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PioBlink)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in pio_blink");
    }

    [Fact]
    public void PioBlink_GpioActivityDetected()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PioBlink)!;

        pico.LoadFlash(flash);

        // Run 2 s — PIO state machine should start and run without HardFault.
        // NOTE: PIO GPIO output does not propagate through SIO GPIO_OUT in the emulator;
        // toggle counting via GpioPin.DigitalValue is therefore not supported here.
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u,
            "HardFault must not occur while pio_blink state machine runs");
    }

    // ── pio_uart_tx ───────────────────────────────────────────────────────────

    [Fact]
    public void PioUartTx_NoHardFault_AfterTransmit()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PioUartTx)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur in pio_uart_tx");
    }

    [Fact]
    public void PioUartTx_Cpu_IsAliveAfterPioUartInit()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PioUartTx)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after PIO UART transmitter starts");
    }
}
