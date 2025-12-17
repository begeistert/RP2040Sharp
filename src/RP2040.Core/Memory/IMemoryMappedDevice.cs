namespace RP2040.Core.Memory;

public interface IMemoryMappedDevice
{
	uint Size { get; }

	byte ReadByte (uint offset);
	ushort ReadHalfWord (uint offset);
	uint ReadWord (uint offset);

	void WriteByte (uint offset, byte value);
	void WriteHalfWord (uint offset, ushort value);
	void WriteWord (uint offset, uint value);
}
