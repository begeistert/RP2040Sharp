using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for UART examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UartTests
{
    // ── hello_serial (hello_world/serial) ─────────────────────────────────────

    [Fact]
    public void HelloSerial_NoHardFault_AfterStartup()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloSerial)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(200);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur");
    }

    [Fact]
    public void HelloSerial_Uart0_ContainsHelloWorld()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloSerial)!;

        pico.LoadFlash(flash);

        var found = pico.RunUntilOutput(pico.Uart0, "Hello, world!", timeoutMs: 5_000);

        found.Should().BeTrue("hello_serial prints 'Hello, world!' over UART0");
    }

    [Fact]
    public void HelloSerial_Uart0_HasMultipleLines()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloSerial)!;

        pico.LoadFlash(flash);

        // hello_serial loops forever printing; wait for 3 repetitions
        var found = pico.RunUntilOutput(
            pico.Uart0,
            text => text.Split('\n').Count(l => l.Contains("Hello, world!")) >= 3,
            timeoutMs: 10_000);

        found.Should().BeTrue("hello_serial should repeat 'Hello, world!' multiple times");
    }

    // ── hello_uart (uart/hello_uart) ──────────────────────────────────────────

    [Fact]
    public void HelloUart_NoHardFault_AfterStartup()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloUart)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(200);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur");
    }

    [Fact]
    public void HelloUart_Uart0_ContainsHello()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloUart)!;

        pico.LoadFlash(flash);

        var found = pico.RunUntilOutput(pico.Uart0, "Hello", timeoutMs: 5_000);

        found.Should().BeTrue("hello_uart sends a greeting over UART0");
    }

    [Fact]
    public void HelloUart_Uart0_HasOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloUart)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        pico.Uart0.ByteCount.Should().BeGreaterThan(0, "hello_uart must transmit bytes over UART0");
    }
}
