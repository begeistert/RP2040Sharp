namespace RP2040.Core.Memory;

public interface IMemoryBus
{
	// Usamos uint para direcciones (32-bit address space)
    
	// Read Operations
	byte ReadByte(uint address);
	ushort ReadHalfWord(uint address); // 16-bit
	uint ReadWord(uint address);       // 32-bit

	// Write Operations
	void WriteByte(uint address, byte value);
	void WriteHalfWord(uint address, ushort value);
	void WriteWord(uint address, uint value);
}
