using RP2040.Peripherals;
using RP2040.TestKit;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for USB-CDC example from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UsbTests
{
    // ── hello_usb (hello_world/usb) ───────────────────────────────────────────

    [Fact]
    public void HelloUsb_NoHardFault_AfterStartup()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloUsb)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u, "HardFault must not occur during USB init");
    }

    [Fact]
    public void HelloUsb_CdcDevice_EnumeratesSuccessfully()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloUsb)!;

        pico.LoadFlash(flash);

        // Run until the device transmits its first CDC payload — that proves enumeration completed.
        // _initialized is set before OnSerialData can fire, so receiving data implies IsConnected.
        var found = pico.RunUntilOutput(pico.UsbCdc, "Hello", timeoutMs: 10_000);

        found.Should().BeTrue("USB CDC device should enumerate and transmit data");
        pico.UsbCdc.IsConnected.Should().BeTrue("USB CDC device should complete enumeration");
    }

    [Fact]
    public void HelloUsb_CdcDevice_TransmitsHelloWorld()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloUsb)!;

        pico.LoadFlash(flash);

        var found = pico.RunUntilOutput(pico.UsbCdc, "Hello, world!", timeoutMs: 10_000);

        found.Should().BeTrue("hello_usb prints 'Hello, world!' over USB CDC");
    }

    [Fact]
    public void HelloUsb_CdcDevice_RepeatsOutput()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.HelloUsb)!;

        pico.LoadFlash(flash);

        // Run in batches until 3 repetitions appear (no lambda overload for UsbCdcProbe)
        const double batchMs = 100.0;
        double elapsed = 0;
        bool found = false;
        while (elapsed < 15_000)
        {
            pico.RunMilliseconds(batchMs);
            elapsed += batchMs;
            if (pico.UsbCdc.Text.Split('\n').Count(l => l.Contains("Hello, world!")) >= 3)
            {
                found = true;
                break;
            }
        }

        found.Should().BeTrue("hello_usb should repeat the message multiple times");
    }
}
