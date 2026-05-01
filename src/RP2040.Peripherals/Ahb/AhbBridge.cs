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
    {
        var device = _devices[(address >> 20) & 0xFF];
        if (device == null) return;

        var atomicType = (address >> 12) & 0x3;
        if (atomicType == 0) { device.WriteWord(address, value); return; }

        var baseAddr = address & ~0x3000u;
        var current = device.ReadWord(baseAddr);
        device.WriteWord(baseAddr, atomicType switch
        {
            1 => current ^ value,
            2 => current | value,
            3 => current & ~value,
            _ => value,
        });
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        var device = _devices[(address >> 20) & 0xFF];
        if (device == null) return;

        var atomicType = (address >> 12) & 0x3;
        if (atomicType == 0) { device.WriteHalfWord(address, value); return; }

        var baseAddr = (address & ~0x3000u) & ~3u;
        var shift = (int)((address & 2) << 3);
        var current = device.ReadWord(baseAddr);
        uint expanded = (uint)value << shift;
        uint mask = 0xFFFFu << shift;
        device.WriteWord(baseAddr, atomicType switch
        {
            1 => (current & ~mask) | ((current ^ expanded) & mask),
            2 => current | expanded,
            3 => current & ~expanded,
            _ => (current & ~mask) | expanded,
        });
    }

    public void WriteByte(uint address, byte value)
    {
        var device = _devices[(address >> 20) & 0xFF];
        if (device == null) return;

        var atomicType = (address >> 12) & 0x3;
        if (atomicType == 0) { device.WriteByte(address, value); return; }

        var baseAddr = (address & ~0x3000u) & ~3u;
        var shift = (int)((address & 3) << 3);
        var current = device.ReadWord(baseAddr);
        uint expanded = (uint)value << shift;
        uint mask = 0xFFu << shift;
        device.WriteWord(baseAddr, atomicType switch
        {
            1 => (current & ~mask) | ((current ^ expanded) & mask),
            2 => current | expanded,
            3 => current & ~expanded,
            _ => (current & ~mask) | expanded,
        });
    }
}
