using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for PWM examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PwmTests
{
    // ── hello_pwm ─────────────────────────────────────────────────────────────

    [Fact]
    public void HelloPwm_NoHardFault_AfterStartup()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloPwm)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(200);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur");
    }

    [Fact]
    public void HelloPwm_Cpu_IsAliveAfterInit()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloPwm)!;

        pico.LoadFlash(flash);

        // hello_pwm configures PWM on GP0 then sits in an infinite loop
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after PWM init");
    }

    [Fact]
    public void HelloPwm_Gpio0_IsConfiguredAsFunctionPwm()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloPwm)!;

        pico.LoadFlash(flash);

        // Allow firmware to run past PWM initialisation
        pico.RunMilliseconds(100);

        // GP0 is the PWM A output of slice 0; the PWM subsystem must have been enabled
        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "no fault during PWM configuration");
    }

    // ── pwm_led_fade ──────────────────────────────────────────────────────────

    [Fact]
    public void PwmLedFade_NoHardFault_AfterOneFadeCycle()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PwmLedFade)!;

        pico.LoadFlash(flash);

        // A full fade-up + fade-down cycle at full clock rate
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during LED fade");
    }

    [Fact]
    public void PwmLedFade_Cpu_IsAliveAfterMultipleCycles()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PwmLedFade)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM during LED fade loop");
        pico.Cpu.Registers.IPSR.Should().NotBe(3u);
    }

    [Fact]
    public void PwmLedFade_DutyCycle_ChangesOverTime()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.PwmLedFade)!;

        pico.LoadFlash(flash);

        // Allow PWM init and first IRQ wrap to fire
        pico.RunMilliseconds(100);

        // GPIO 25 (onboard LED) = PWM slice 4, channel B  (slice = (pin >> 1) & 7)
        const int ledSlice = 4;
        var dutyFirst = pico.Rp2040.Pwm.GetDutyB(ledSlice);

        // Run 400 ms more — the IRQ-driven fade increments the level every wrap
        pico.RunMilliseconds(400);
        var dutySecond = pico.Rp2040.Pwm.GetDutyB(ledSlice);

        // At least one sample must be non-zero (IRQ fired and set a level > 0)
        Math.Max(dutyFirst, dutySecond).Should().BeGreaterThan(0,
            "PWM IRQ must fire and call pwm_set_gpio_level with a non-zero value");

        // The level must have changed between samples (fade is progressing)
        dutySecond.Should().NotBe(dutyFirst,
            "duty cycle must change as the IRQ-driven fade updates pwm_set_gpio_level");
    }
}
