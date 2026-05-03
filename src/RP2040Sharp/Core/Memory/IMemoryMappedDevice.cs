namespace RP2040.Core.Memory;

public interface IMemoryMappedDevice
{
    uint Size { get; }

    byte ReadByte(uint address);
    ushort ReadHalfWord(uint address);
    uint ReadWord(uint address);

    void WriteByte(uint address, byte value);
    void WriteHalfWord(uint address, ushort value);
    void WriteWord(uint address, uint value);
}

/// <summary>
/// Marker interface: the device processes RP2040 atomic-alias addresses
/// (bits 12–13 = XOR/SET/CLR) internally. The AHB bridge will pass the
/// full, unmodified address and the raw firmware write value so the device
/// can apply the correct per-register semantics (e.g. W1C vs R/W).
/// </summary>
public interface IHandlesAtomicAliases { }
