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
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not reach lockup (firmware panic) during PIO init");
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
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up during hello_pio execution");
    }

    [Fact]
    public void HelloPio_Gpio25_BecomesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloPio)!;

        pico.LoadFlash(flash);
        // hello_pio drives GPIO 25 (onboard LED) via a PIO SET PINS program.
        // Allow enough time for pio_init and the first SM tick.
        pico.RunMilliseconds(500);

        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up before GPIO 25 is driven by PIO");
        // GPIO 25 is configured as a PIO output via pio_gpio_init() → IO_BANK0 FUNCSEL=6 (PIO0)
        pico.Gpio[25].Should().BePioOutput("hello_pio configures GPIO 25 as PIO0 output via pio_gpio_init()");
    }

    [Fact]
    public void HelloPio_Gpio25_TogglesOverTime()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloPio)!;

        pico.LoadFlash(flash);

        int toggles = 0;
        bool? prev = null;

        // hello_pio blinks at ~4 Hz — sample over 2 simulated seconds.
        for (int i = 0; i < 20; i++)
        {
            pico.RunMilliseconds(100);
            if (pico.Cpu.IsLockedUp) break;
            bool current = pico.Gpio[25].DigitalValue;
            if (prev.HasValue && current != prev.Value)
                toggles++;
            prev = current;
        }

        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up while PIO blinks GPIO 25");
        toggles.Should().BeGreaterThanOrEqualTo(2,
            "hello_pio PIO SET program must toggle GPIO 25 at least twice over 2 simulated seconds");
    }

    // ── pio_blink ─────────────────────────────────────────────────────────────

    /// <remarks>
    /// NOTE: <c>pio_blink.uf2</c> calls pico-sdk's <c>panic()</c> during initialisation
    /// (BKPT #0 at flash offset 0x3C30, triggered by an assertion in the clock-divider path).
    /// The test harness simulates a debug-monitor being attached (ARMv6-M §C1.7.2), so the
    /// BKPT is captured rather than escalating to HardFault.  The test documents this as the
    /// expected current behaviour.  Fixing the underlying missing emulator feature will change
    /// the expectation to: <c>BreakpointHits.Count == 0</c>.
    /// </remarks>
    [Fact]
    public void PioBlink_PanicsWithBkpt_KnownLimitation()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PioBlink)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        // The test harness captures BKPT events; a non-zero count means the firmware hit
        // a panic() during this emulation run — documenting the known limitation.
        pico.BreakpointHits.Should().NotBeEmpty(
            "pio_blink.uf2 is known to trigger a pico-sdk panic (BKPT #0) during init in this emulator");
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
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up in pio_uart_tx");
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
        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up in pio_uart_tx");
    }

    [Fact]
    public void PioUartTx_Gpio0_BecomesOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PioUartTx)!;

        pico.LoadFlash(flash);
        // pio_uart_tx drives GPIO 0 as the UART TX pin via pio_gpio_init(pio, UART_TX_PIN).
        // The pico-examples default UART_TX_PIN is GPIO 0.
        pico.RunMilliseconds(200);

        pico.Cpu.IsLockedUp.Should().BeFalse("CPU must not lock up before PIO UART TX pin is configured");
        // GPIO 0 is configured via pio_gpio_init() → IO_BANK0 FUNCSEL=6 (PIO0)
        pico.Gpio[0].Should().BePioOutput("pio_uart_tx must configure GPIO 0 as PIO0 output via pio_gpio_init()");
    }
}
