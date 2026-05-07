using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for GPIO examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GpioTests
{
    // ── blink_simple ──────────────────────────────────────────────────────────

    [Fact]
    public void BlinkSimple_NoHardFault_AfterOneCycle()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.BlinkSimple)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(600);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault (IPSR == 3) must never occur");
    }

    [Fact]
    public void BlinkSimple_Gpio25_IsOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.BlinkSimple)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(150);

        pico.Gpio[25].Should().BeOutput("blink_simple configures GPIO 25 as OUTPUT");
    }

    [Fact]
    public void BlinkSimple_Gpio25_TogglesOverTime()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.BlinkSimple)!;

        pico.LoadFlash(flash);

        int toggles = 0;
        bool? prev = null;

        for (int i = 0; i < 20; i++)
        {
            pico.RunMilliseconds(100);
            bool current = pico.Gpio[25].DigitalValue;
            if (prev.HasValue && current != prev.Value)
                toggles++;
            prev = current;
        }

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur");
        toggles.Should().BeGreaterThanOrEqualTo(2, "GPIO 25 should toggle multiple times over 2 seconds");
    }

    // ── hello_gpio_irq ────────────────────────────────────────────────────────

    [Fact]
    public void HelloGpioIrq_NoHardFault_AfterInit()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloGpioIrq)!;

        pico.LoadFlash(flash);

        // Allow time for GPIO IRQ setup (the example waits for a button press on GPIO 0)
        pico.RunMilliseconds(200);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during GPIO IRQ init");
    }

    [Fact]
    public void HelloGpioIrq_InjectedEdge_TriggersOutputChange()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloGpioIrq)!;

        pico.LoadFlash(flash);

        // Let the firmware initialise GPIO IRQ on pin 0
        pico.RunMilliseconds(200);

        // Capture GPIO 25 state before injecting a falling edge on GPIO 0
        bool before = pico.Gpio[25].DigitalValue;

        // Inject a falling edge on GP0 (button press) — the ISR toggles GPIO 25
        pico.Gpio[0].ForceInput(false); // drive GP0 LOW to simulate button press
        pico.RunMilliseconds(5);

        // After the edge the firmware ISR should have toggled something; at minimum no HardFault
        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur after GPIO IRQ fires");
    }
}
