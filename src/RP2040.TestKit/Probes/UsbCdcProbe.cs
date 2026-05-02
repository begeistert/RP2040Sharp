using System.Text;
using RP2040.Peripherals.Usb;

namespace RP2040.TestKit.Probes;

/// <summary>
/// Captures bytes that the device transmits over USB-CDC and exposes a writer
/// that pushes data into the host-to-device direction.  Attach to a
/// <see cref="UsbCdcHost"/> via <see cref="Attach"/>.
/// </summary>
public sealed class UsbCdcProbe
{
    private readonly List<byte> _bytes = [];
    private string? _textCache;
    private string[]? _linesCache;

    public IReadOnlyList<byte> Bytes => _bytes;
    public int ByteCount => _bytes.Count;
    public string Text => _textCache ??= Encoding.Latin1.GetString(_bytes.ToArray());
    public IReadOnlyList<string> Lines
        => _linesCache ??= Text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    /// <summary>True after the host has completed enumeration and SET_CONTROL_LINE_STATE.</summary>
    public bool IsConnected => _cdc?.IsConnected ?? false;

    private UsbCdcHost? _cdc;

    public UsbCdcProbe Attach(UsbCdcHost cdc)
    {
        if (_cdc != null) _cdc.OnSerialData -= Capture;
        _cdc = cdc;
        _cdc.OnSerialData += Capture;
        return this;
    }

    public void InjectByte(byte value) => _cdc?.SendSerialByte(value);

    public void InjectString(string text)
    {
        if (_cdc == null) return;
        foreach (var b in Encoding.Latin1.GetBytes(text)) _cdc.SendSerialByte(b);
    }

    public void Clear()
    {
        _bytes.Clear();
        _textCache = null;
        _linesCache = null;
    }

    private void Capture(byte[] data)
    {
        _bytes.AddRange(data);
        _textCache = null;
        _linesCache = null;
    }
}
