using FluentAssertions;
using RP2040.Peripherals.Usb;

namespace RP2040.Peripherals.Tests.Usb;

/// <summary>
/// Unit tests for <see cref="UsbCdcHost.ExtractAllInterfaces"/> and the backward-compatible
/// <see cref="UsbCdcHost.ExtractEndpointNumbers"/> wrapper.
/// </summary>
public sealed class UsbDescriptorParsingTests
{
    // Minimal CDC-only configuration descriptor for the 9-byte CDC ACM + Data interfaces
    // (this mirrors what TinyUSB emits for a CDC-only device).
    private static byte[] BuildCdcOnlyDescriptor()
    {
        // Interface 0 (CDC Control, class 0x02) — 1 interrupt endpoint (ignored for CDC data)
        // Interface 1 (CDC Data, class 0x0A) — 2 bulk endpoints
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

    private static byte[] BuildCompositeCdcMscDescriptor()
    {
        // CDC Data (class 0x0A, ep 1 OUT + ep 1 IN) + MSC (class 0x08, ep 2 OUT + ep 2 IN)
        return new byte[]
        {
            // Config descriptor
            9, 0x02, 0, 0, 3, 1, 0, 0xC0, 50,
            // Interface 0: CDC Control
            9, 0x04, 0, 0, 1, 0x02, 0x02, 0x01, 0,
            5, 0x24, 0x00, 0x10, 0x01,
            4, 0x24, 0x02, 0x02,
            5, 0x24, 0x06, 0x00, 0x01,
            7, 0x05, 0x83, 0x03, 8, 0, 10,  // ep 3 IN interrupt
            // Interface 1: CDC Data (class 0x0A)
            9, 0x04, 1, 0, 2, 0x0A, 0x00, 0x00, 0,
            7, 0x05, 0x01, 0x02, 64, 0, 0,  // ep 1 OUT bulk
            7, 0x05, 0x81, 0x02, 64, 0, 0,  // ep 1 IN  bulk
            // Interface 2: MSC (class 0x08)
            9, 0x04, 2, 0, 2, 0x08, 0x06, 0x50, 0,
            7, 0x05, 0x02, 0x02, 64, 0, 0,  // ep 2 OUT bulk
            7, 0x05, 0x82, 0x02, 64, 0, 0,  // ep 2 IN  bulk
        };
    }

    private static byte[] BuildCompositeWithHidDescriptor()
    {
        // CDC Data + MSC + HID (class 0x03, ep 3 IN interrupt)
        var cdcMsc = BuildCompositeCdcMscDescriptor().ToList();
        // Append HID interface + endpoint
        cdcMsc.AddRange(new byte[]
        {
            9, 0x04, 3, 0, 1, 0x03, 0x00, 0x00, 0,  // HID interface
            7, 0x05, 0x83, 0x03, 8, 0, 1,            // ep 3 IN interrupt
        });
        return cdcMsc.ToArray();
    }

    [Fact]
    public void ExtractAllInterfaces_CdcOnly_FindsCdcEndpoints()
    {
        var desc = BuildCdcOnlyDescriptor();
        UsbCdcHost.ExtractAllInterfaces(desc,
            out var cdcIn, out var cdcOut,
            out var mscIn, out var mscOut,
            out var hidIn, out var hidOut);

        cdcIn.Should().Be(1);
        cdcOut.Should().Be(1);
        mscIn.Should().Be(-1);
        mscOut.Should().Be(-1);
        hidIn.Should().Be(-1);
        hidOut.Should().Be(-1);
    }

    [Fact]
    public void ExtractAllInterfaces_CdcMsc_FindsBothInterfaces()
    {
        var desc = BuildCompositeCdcMscDescriptor();
        UsbCdcHost.ExtractAllInterfaces(desc,
            out var cdcIn, out var cdcOut,
            out var mscIn, out var mscOut,
            out var hidIn, out var hidOut);

        cdcIn.Should().Be(1);
        cdcOut.Should().Be(1);
        mscIn.Should().Be(2);
        mscOut.Should().Be(2);
        hidIn.Should().Be(-1);
        hidOut.Should().Be(-1);
    }

    [Fact]
    public void ExtractAllInterfaces_CdcMscHid_FindsAllInterfaces()
    {
        var desc = BuildCompositeWithHidDescriptor();
        UsbCdcHost.ExtractAllInterfaces(desc,
            out var cdcIn, out var cdcOut,
            out var mscIn, out var mscOut,
            out var hidIn, out var hidOut);

        cdcIn.Should().Be(1);
        cdcOut.Should().Be(1);
        mscIn.Should().Be(2);
        mscOut.Should().Be(2);
        hidIn.Should().Be(3);
        hidOut.Should().Be(-1, "the HID interface only has an IN endpoint");
    }

    [Fact]
    public void ExtractEndpointNumbers_BackwardCompatWrapper_ReturnsCdcEndpoints()
    {
        var desc = BuildCompositeCdcMscDescriptor();
        UsbCdcHost.ExtractEndpointNumbers(desc, out var inEp, out var outEp);
        inEp.Should().Be(1);
        outEp.Should().Be(1);
    }

    [Fact]
    public void ExtractAllInterfaces_EmptyDescriptor_ReturnsAllMinusOne()
    {
        UsbCdcHost.ExtractAllInterfaces(Array.Empty<byte>(),
            out var cdcIn, out var cdcOut,
            out var mscIn, out var mscOut,
            out var hidIn, out var hidOut);

        cdcIn.Should().Be(-1);
        cdcOut.Should().Be(-1);
        mscIn.Should().Be(-1);
        mscOut.Should().Be(-1);
        hidIn.Should().Be(-1);
        hidOut.Should().Be(-1);
    }
}
