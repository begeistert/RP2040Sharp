using RP2040.Core.Memory;

namespace RP2040.Peripherals.Apb;

/// <summary>
/// APB bridge for the 0x40xxxxxx peripheral bus (region 0x4).
/// Routes using bits [21:14] of the local address (after &amp; 0x0FFFFFFF),
/// which groups each peripheral's four atomic-mirror windows (base, XOR, SET, CLR)
/// into the same 16 KiB slot.
/// The local address passed to each device is address &amp; 0xFFF (4 KiB window).
/// </summary>
public sealed class ApbBridge : IMemoryMappedDevice
{
    // 256 slots, each covering 16 KiB of the APB space
    private readonly IMemoryMappedDevice?[] _devices = new IMemoryMappedDevice?[256];

    public uint Size => 0x10000000;

    /// <summary>
    /// Register a device at its APB base address (full 32-bit address, e.g. 0x40034000).
    /// </summary>
    public void Register(uint baseAddress, IMemoryMappedDevice device)
    {
        // Mask off region nibble → local address, then extract 16 KiB slot index
        var localBase = baseAddress & 0x0FFFFFFF;
        _devices[(localBase >> 14) & 0xFF] = device;
    }

    public uint ReadWord(uint address)
    {
        var device = _devices[(address >> 14) & 0xFF];
        return device?.ReadWord(address & 0xFFF) ?? 0;
    }

    public ushort ReadHalfWord(uint address)
    {
        var device = _devices[(address >> 14) & 0xFF];
        return device?.ReadHalfWord(address & 0xFFF) ?? 0;
    }

    public byte ReadByte(uint address)
    {
        var device = _devices[(address >> 14) & 0xFF];
        return device?.ReadByte(address & 0xFFF) ?? 0;
    }

    public void WriteWord(uint address, uint value)
        => _devices[(address >> 14) & 0xFF]?.WriteWord(address & 0xFFF, value);

    public void WriteHalfWord(uint address, ushort value)
        => _devices[(address >> 14) & 0xFF]?.WriteHalfWord(address & 0xFFF, value);

    public void WriteByte(uint address, byte value)
        => _devices[(address >> 14) & 0xFF]?.WriteByte(address & 0xFFF, value);
}
