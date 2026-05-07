using RP2040.Peripherals.Usb;

namespace RP2040.TestKit.Probes;

/// <summary>
/// Test-kit probe for the USB Mass Storage Class host driver.
/// Wraps <see cref="UsbMscHost"/> and exposes synchronous sector read/write helpers
/// that run the simulation internally.
///
/// Attach via <see cref="Attach"/> and then call
/// <see cref="WaitForReady"/> before issuing read/write operations.
/// </summary>
public sealed class UsbMscProbe
{
    private UsbMscHost? _msc;

    /// <summary>true after MSC initialisation (TEST_UNIT_READY + READ_CAPACITY) is complete.</summary>
    public bool IsConnected => _msc?.IsConnected ?? false;

    /// <summary>Total logical blocks exposed by the device (valid after <see cref="IsConnected"/>).</summary>
    public uint BlockCount => _msc?.BlockCount ?? 0;

    /// <summary>Logical block size in bytes, typically 512.</summary>
    public uint BlockSize  => _msc?.BlockSize  ?? 512;

    public UsbMscProbe Attach(UsbMscHost msc)
    {
        _msc = msc;
        return this;
    }

    /// <summary>
    /// Enqueue a sector read from logical block <paramref name="lba"/>.
    /// The caller must advance the simulation to process the transfer.
    /// <paramref name="callback"/> is invoked with the 512-byte sector data when complete.
    /// </summary>
    public void RequestRead(uint lba, Action<byte[]> callback)
        => _msc?.RequestRead(lba, callback);

    /// <summary>
    /// Enqueue a sector write to logical block <paramref name="lba"/>.
    /// The caller must advance the simulation.
    /// <paramref name="callback"/> is invoked (with an empty array) after the CSW confirms success.
    /// </summary>
    public void RequestWrite(uint lba, byte[] data, Action<byte[]>? callback = null)
        => _msc?.RequestWrite(lba, data, callback);
}
