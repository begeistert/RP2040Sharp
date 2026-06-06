using FluentAssertions;
using RP2040.Peripherals.Usb;

namespace RP2040.Peripherals.Tests.Usb;

/// <summary>
/// Unit tests for <see cref="UsbCdcHost.ExtractEndpointNumbers"/>: locating the CDC data
/// interface's bulk IN/OUT endpoints, including within composite descriptors.
/// </summary>
public sealed class UsbDescriptorParsingTests
{
    // Minimal CDC-only configuration descriptor (CDC ACM control + data interfaces),
    // mirroring what TinyUSB emits for a CDC-only device.
    private static byte[] BuildCdcOnlyDescriptor()
    {
        return new byte[]
        {
            // Config descriptor (9 bytes, type 0x02)
            9, 0x02, 67, 0, 2, 1, 0, 0xC0, 50,
            // Interface 0: CDC Control (class 0x02, 1 endpoint)
            9, 0x04, 0, 0, 1, 0x02, 0x02, 0x01, 0,
            // Class-specific CDC headers (skipped)
            5, 0x24, 0x00, 0x10, 0x01,
            4, 0x24, 0x02, 0x02,
            5, 0x24, 0x06, 0x00, 0x01,
            // Endpoint 3 IN (interrupt, type=3)
            7, 0x05, 0x83, 0x03, 8, 0, 10,
            // Interface 1: CDC Data (class 0x0A, 2 bulk endpoints)
            9, 0x04, 1, 0, 2, 0x0A, 0x00, 0x00, 0,
            // Endpoint 1 OUT (bulk, type=2)
            7, 0x05, 0x01, 0x02, 64, 0, 0,
            // Endpoint 1 IN  (bulk, type=2)
            7, 0x05, 0x81, 0x02, 64, 0, 0,
        };
    }

    // CDC Data (class 0x0A, ep 1) + an extra MSC interface (class 0x08, ep 2): the parser
    // must still pick out the CDC endpoints and ignore the other interface.
    private static byte[] BuildCompositeCdcMscDescriptor()
    {
        return new byte[]
        {
            9, 0x02, 0, 0, 3, 1, 0, 0xC0, 50,
            9, 0x04, 0, 0, 1, 0x02, 0x02, 0x01, 0,  // CDC Control
            5, 0x24, 0x00, 0x10, 0x01,
            4, 0x24, 0x02, 0x02,
            5, 0x24, 0x06, 0x00, 0x01,
            7, 0x05, 0x83, 0x03, 8, 0, 10,          // ep 3 IN interrupt
            9, 0x04, 1, 0, 2, 0x0A, 0x00, 0x00, 0,  // CDC Data
            7, 0x05, 0x01, 0x02, 64, 0, 0,          // ep 1 OUT bulk
            7, 0x05, 0x81, 0x02, 64, 0, 0,          // ep 1 IN  bulk
            9, 0x04, 2, 0, 2, 0x08, 0x06, 0x50, 0,  // MSC interface (ignored)
            7, 0x05, 0x02, 0x02, 64, 0, 0,          // ep 2 OUT bulk
            7, 0x05, 0x82, 0x02, 64, 0, 0,          // ep 2 IN  bulk
        };
    }

    [Fact]
    public void ExtractEndpointNumbers_CdcOnly_FindsCdcEndpoints()
    {
        UsbCdcHost.ExtractEndpointNumbers(BuildCdcOnlyDescriptor(), out var inEp, out var outEp);
        inEp.Should().Be(1);
        outEp.Should().Be(1);
    }

    [Fact]
    public void ExtractEndpointNumbers_CompositeDescriptor_FindsCdcEndpointsOnly()
    {
        UsbCdcHost.ExtractEndpointNumbers(BuildCompositeCdcMscDescriptor(), out var inEp, out var outEp);
        inEp.Should().Be(1);
        outEp.Should().Be(1);
    }

    [Fact]
    public void ExtractEndpointNumbers_EmptyDescriptor_ReturnsMinusOne()
    {
        UsbCdcHost.ExtractEndpointNumbers(Array.Empty<byte>(), out var inEp, out var outEp);
        inEp.Should().Be(-1);
        outEp.Should().Be(-1);
    }
}
