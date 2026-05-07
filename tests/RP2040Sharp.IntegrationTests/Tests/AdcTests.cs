using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for ADC examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AdcTests
{
    // ── hello_adc ─────────────────────────────────────────────────────────────

    [Fact]
    public void HelloAdc_NoHardFault_AfterFirstRead()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloAdc)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during ADC read");
    }

    [Fact]
    public void HelloAdc_Uart0_ProducesVoltageReading()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloAdc)!;

        pico.LoadFlash(flash);

        // hello_adc reads ADC0 (GP26) repeatedly and prints the converted voltage
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("hello_adc must print ADC readings over UART0");
    }

    [Fact]
    public void HelloAdc_Uart0_ReceivesMultipleReadings()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloAdc)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(3_000);

        pico.Uart0.Lines.Count.Should().BeGreaterThan(2,
            "hello_adc should print multiple ADC readings over 3 seconds");
    }

    // ── onboard_temperature ───────────────────────────────────────────────────

    [Fact]
    public void OnboardTemperature_NoHardFault_AfterFirstRead()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.OnboardTemperature)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur reading internal temperature");
    }

    [Fact]
    public void OnboardTemperature_Uart0_ProducesTemperatureReading()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.OnboardTemperature)!;

        pico.LoadFlash(flash);

        // Reads ADC4 (internal temp sensor) and prints Celsius/Fahrenheit
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("onboard_temperature must print a temperature reading over UART0");
    }

    [Fact]
    public void OnboardTemperature_Cpu_IsAliveAfterMultipleReads()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.OnboardTemperature)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(3_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after multiple ADC temperature reads");
    }
}
