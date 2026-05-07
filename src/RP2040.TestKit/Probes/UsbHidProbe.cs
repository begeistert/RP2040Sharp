using RP2040.Peripherals.Usb;

namespace RP2040.TestKit.Probes;

/// <summary>
/// Test-kit probe for the USB HID host driver.
/// Wraps <see cref="UsbHidHost"/> and captures incoming HID reports.
/// </summary>
public sealed class UsbHidProbe
{
    private UsbHidHost? _hid;
    private readonly List<byte[]> _reports = new();

    public UsbHidProbe Attach(UsbHidHost hid)
    {
        if (_hid != null) _hid.OnReport -= Capture;
        _hid = hid;
        _hid.OnReport += Capture;
        return this;
    }

    /// <summary>true once SET_CONFIGURATION is acknowledged and the HID endpoint has been found.</summary>
    public bool IsConnected => _hid?.IsConnected ?? false;

    /// <summary>All HID reports received so far.</summary>
    public IReadOnlyList<byte[]> Reports => _reports;

    /// <summary>Number of reports received.</summary>
    public int ReportCount => _reports.Count;

    /// <summary>The most recently received report, or an empty array if none.</summary>
    public byte[] LatestReport => _reports.Count > 0 ? _reports[^1] : Array.Empty<byte>();

    /// <summary>
    /// Send an output report to the device (host → device direction).
    /// Requires the simulation to be running.
    /// </summary>
    public void SendReport(byte[] reportData) => _hid?.SendReport(reportData);

    /// <summary>Remove all captured reports.</summary>
    public void Clear() => _reports.Clear();

    private void Capture(byte[] report)
        => _reports.Add(report);
}
