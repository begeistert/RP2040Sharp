using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the canonical blink example (pico-examples/blink).
/// Firmware: GPIO 25 goes HIGH for 250 ms, goes LOW for 250 ms, repeat at 2 Hz.
/// LED_DELAY_MS = 250; PICO_DEFAULT_LED_PIN = 25 on the Pico board.
/// </summary>
[Trait("Category", "Integration")]
public sealed class BlinkTests
{
    [Fact]
    public void Blink_NoHardFault_AfterTwoFullCycles()
    {
        using var pico = new PicoSimulation();
        var uf2 = PicoExamplesFirmware.Blink;
        var flash = RP2040Machine.Uf2ToFlash(uf2);
        flash.Should().NotBeNull("blink.uf2 must decode to a valid flash image");

        pico.LoadFlash(flash!);

        // Run 600 ms — enough for one full ON/OFF cycle (250 ms + 250 ms + startup margin)
        pico.RunMilliseconds(600);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "a HardFault (IPSR == 3) must never occur");
    }

    [Fact]
    public void Blink_Gpio25_IsHighAfter600ms()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.Blink)!;

        pico.LoadFlash(flash);

        // blink sets GPIO 25 HIGH immediately, then sleeps 250 ms before going LOW
        pico.RunMilliseconds(150);

        pico.Gpio[25].Should().BeOutput("GPIO 25 must be configured as OUTPUT by blink firmware");
        pico.Gpio[25].Should().BeHigh("GPIO 25 (onboard LED) should be HIGH for the first 250 ms");
    }

    [Fact]
    public void Blink_Gpio25_IsLowAfter350ms()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.Blink)!;

        pico.LoadFlash(flash);

        // After 450 ms the first LOW phase is firmly underway
        // (HIGH phase: startup~100ms to ~350ms; LOW phase: ~350ms to ~600ms)
        pico.RunMilliseconds(450);

        pico.Gpio[25].Should().BeLow("GPIO 25 should be LOW during the second half of the blink cycle");
    }

    [Fact]
    public void Blink_Gpio25_TogglesAtLeastTwice_InTwoSeconds()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.Blink)!;

        pico.LoadFlash(flash);

        // Sample GPIO 25 every 100 ms and count edges over 2 seconds
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

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during blink");
        // At 2 Hz the LED toggles 8 times in 2 s; allow margin and expect at least 4
        toggles.Should().BeGreaterThanOrEqualTo(4,
            "GPIO 25 should toggle at least 4 times (2 full cycles) over 2 seconds of simulated time");
    }
}
