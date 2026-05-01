using RP2040.Core.Memory;

namespace RP2040.Peripherals.Ahb;

/// <summary>
/// Sub-router for a 256 MB address region (e.g., region 0x5).
/// Dispatches by 1 MB blocks: index = (address >> 20) &amp; 0xFF.
/// Devices receive the address unchanged.
/// </summary>
public sealed class AhbBridge : IMemoryMappedDevice
{
    private readonly IMemoryMappedDevice?[] _devices = new IMemoryMappedDevice?[256];

    public uint Size => 0x1000_0000;  // full region

    /// <summary>Register a device. baseAddress bits [27:20] determine the slot.</summary>
    public void Register(uint baseAddress, IMemoryMappedDevice device)
    {
        var idx = (baseAddress >> 20) & 0xFF;
        _devices[idx] = device;
    }

    public uint ReadWord(uint address)
        => _devices[(address >> 20) & 0xFF]?.ReadWord(address) ?? 0;

    public ushort ReadHalfWord(uint address)
        => _devices[(address >> 20) & 0xFF]?.ReadHalfWord(address) ?? 0;

    public byte ReadByte(uint address)
        => _devices[(address >> 20) & 0xFF]?.ReadByte(address) ?? 0;

    public void WriteWord(uint address, uint value)
        => _devices[(address >> 20) & 0xFF]?.WriteWord(address, value);

    public void WriteHalfWord(uint address, ushort value)
        => _devices[(address >> 20) & 0xFF]?.WriteHalfWord(address, value);

    public void WriteByte(uint address, byte value)
        => _devices[(address >> 20) & 0xFF]?.WriteByte(address, value);
}
