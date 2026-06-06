using System.Text;
using RP2040.Peripherals.Uart;

namespace RP2040.TestKit.Probes;

/// <summary>
/// Captures bytes transmitted by a UART peripheral and allows injecting bytes into the RX FIFO.
/// Attach to a <see cref="UartPeripheral"/> via <see cref="Attach"/>.
/// </summary>
public sealed class UartProbe
{
    private readonly List<byte> _bytes = [];
    private string? _textCache;
    private string[]? _linesCache;

    /// <summary>All bytes transmitted so far (Latin-1 encoded).</summary>
    public IReadOnlyList<byte> Bytes => _bytes;

    /// <summary>Number of bytes captured.</summary>
    public int ByteCount => _bytes.Count;

    /// <summary>Transmitted bytes decoded as Latin-1 text.</summary>
    public string Text => _textCache ??= Encoding.Latin1.GetString(_bytes.ToArray());

    /// <summary>Lines split on LF (CR stripped), cached until next byte arrives.</summary>
    public IReadOnlyList<string> Lines
        => _linesCache ??= Text.Split('\n')
                               .Select(l => l.TrimEnd('\r'))
                               .ToArray();

    private UartPeripheral? _uart;

    /// <summary>Attach this probe to a UART peripheral.</summary>
    public UartProbe Attach(UartPeripheral uart)
    {
        if (_uart != null)
            _uart.OnByteTransmit -= Capture;
        _uart = uart;
        _uart.OnByteTransmit += Capture;
        return this;
    }

    /// <summary>Inject a byte as if received from a remote device.</summary>
    public void InjectByte(byte value) => _uart?.InjectByte(value);

    /// <summary>Inject a string as Latin-1 bytes.</summary>
    public void InjectString(string text)
    {
        foreach (var b in Encoding.Latin1.GetBytes(text))
            _uart?.InjectByte(b);
    }

    /// <summary>Clear captured data.</summary>
    public void Clear()
    {
        _bytes.Clear();
        _textCache = null;
        _linesCache = null;
    }

    private void Capture(byte b)
    {
        _bytes.Add(b);
        _textCache = null;
        _linesCache = null;
    }
}
