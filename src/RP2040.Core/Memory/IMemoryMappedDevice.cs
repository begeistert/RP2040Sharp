namespace RP2040.Core.Memory;

public interface IMemoryMappedDevice
{
	uint Size { get; }

	byte ReadByte (uint address);
	ushort ReadHalfWord (uint address);
	uint ReadWord (uint address);

	void WriteByte (uint address, byte value);
	void WriteHalfWord (uint address, ushort value);
	void WriteWord (uint address, uint value);
}
