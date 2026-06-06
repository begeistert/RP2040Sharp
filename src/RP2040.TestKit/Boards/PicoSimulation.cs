using RP2040.Peripherals.Gpio;
using RP2040.TestKit.Probes;

namespace RP2040.TestKit.Boards;

/// <summary>
/// Pre-configured simulation of a Raspberry Pi Pico board (125 MHz, UART0/1 probed, GPIO 0-29).
/// <example>
/// <code>
/// using var pico = new PicoSimulation();
/// pico.LoadFlash(firmware);
/// pico.RunMilliseconds(100);
/// pico.Uart0.Should().Contain("Hello");
/// pico.Gpio[25].Should().BeHigh("onboard LED should be on");
/// </code>
/// </example>
/// </summary>
public sealed class PicoSimulation : RP2040TestSimulation
{
    /// <summary>Probe for UART0 (GP0/GP1).</summary>
    public UartProbe Uart0 { get; }

    /// <summary>Probe for UART1 (GP4/GP5).</summary>
    public UartProbe Uart1 { get; }

    /// <summary>Auto-enumerated USB CDC-ACM channel (TinyUSB-compatible).</summary>
    public UsbCdcProbe UsbCdc { get; }

    /// <summary>All 30 GPIO pins.</summary>
    public IReadOnlyList<GpioPin> Gpio => Machine.Gpio;

    public PicoSimulation(bool withUsbCdc = true)
    {
        WithFrequency(125_000_000);
        AddUart(0, out var u0);
        AddUart(1, out var u1);
        Uart0 = u0;
        Uart1 = u1;

        if (withUsbCdc)
        {
            AddUsbCdc(out var cdc);
            UsbCdc = cdc;
        }
        else
        {
            // Leave USB unattached so the device sees no USB host.
            UsbCdc = new UsbCdcProbe();
        }
    }

    /// <summary>Load firmware into Flash and reset.</summary>
    public PicoSimulation LoadFlash(ReadOnlySpan<byte> bytes)
    {
        WithBinary(bytes);
        return this;
    }
}
