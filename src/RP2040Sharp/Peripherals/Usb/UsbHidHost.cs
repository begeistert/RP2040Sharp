namespace RP2040.Peripherals.Usb;

/// <summary>
/// Host-side USB HID driver.  Works alongside <see cref="UsbCdcHost"/> when the device
/// exposes a composite configuration that includes a HID interface (e.g. CircuitPython
/// which exposes a keyboard/mouse HID device alongside its CDC REPL).
///
/// The driver listens on the HID interrupt-IN endpoint and surfaces received reports via
/// <see cref="OnReport"/>.  It optionally accepts outgoing (host→device) reports via
/// <see cref="SendReport"/> if the device exposes an interrupt-OUT endpoint.
///
/// No HID report descriptor parsing is performed; raw report bytes are surfaced
/// directly.
/// </summary>
public sealed class UsbHidHost
{
    private readonly UsbCdcHost    _cdc;
    private readonly UsbPeripheral _usb;

    private bool _connected;

    /// <summary>Fired whenever the device sends a HID report on the interrupt-IN endpoint.</summary>
    public Action<byte[]>? OnReport;
    /// <summary>Raised once the MSC configuration is complete and the HID interface is ready.</summary>
    public Action?         OnConnected;

    /// <summary>true once SET_CONFIGURATION is acknowledged and HID endpoints have been found.</summary>
    public bool IsConnected => _connected;

    public UsbHidHost(UsbCdcHost cdc)
    {
        _cdc = cdc;
        _usb = cdc.Usb;
        cdc.OnConfigurationComplete += HandleConfigurationComplete;
        _usb.OnEndpointWrite        += HandleEndpointWrite;
        _usb.OnEndpointRead         += HandleEndpointRead;
    }

    /// <summary>
    /// Send a HID output report to the device.  Requires the simulation to be running.
    /// Only valid when the device exposes a HID interrupt-OUT endpoint.
    /// </summary>
    public void SendReport(byte[] reportData)
    {
        var outEp = _cdc.HidOutEndpoint;
        if (outEp < 0) return;
        _usb.EndpointReadDone(outEp, reportData);
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void HandleConfigurationComplete()
    {
        _connected = _cdc.HidInEndpoint >= 0;
        if (_connected) OnConnected?.Invoke();
    }

    private void HandleEndpointWrite(int ep, byte[] data)
    {
        if (_cdc.HidInEndpoint < 0 || ep != _cdc.HidInEndpoint) return;
        if (data.Length > 0) OnReport?.Invoke(data);
    }

    private void HandleEndpointRead(int ep, int size)
    {
        // For HID OUT endpoints the device arms the endpoint when it wants to receive a report.
        // We do nothing here; the caller uses SendReport() explicitly.
        if (_cdc.HidOutEndpoint < 0 || ep != _cdc.HidOutEndpoint) return;
        // Acknowledge with empty data so TinyUSB doesn't stall.
        _usb.EndpointReadDone(ep, ReadOnlySpan<byte>.Empty);
    }
}
