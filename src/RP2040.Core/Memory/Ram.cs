using System.Buffers.Binary;
namespace RP2040.Core.Memory;

public class RandomAccessMemory (int size) : IMemoryBus
{
	private readonly byte[] _memory = new byte[size];

	public byte ReadByte(uint address)
	{
		// Verificación de límites omitida por brevedad, pero necesaria
		return _memory[address];
	}

	public ushort ReadHalfWord(uint address)
	{
		// TRUCO DE EXPERTO: Span + BinaryPrimitives
		// Creamos un Span de solo lectura sobre los 2 bytes que nos interesan.
		// El JIT elimina el costo de creación del Span en Release.
		ReadOnlySpan<byte> slice = _memory.AsSpan((int)address, 2);
        
		// El RP2040 es Little Endian
		return BinaryPrimitives.ReadUInt16LittleEndian(slice);
	}

	public uint ReadWord(uint address)
	{
		// Lectura de 32 bits de golpe
		ReadOnlySpan<byte> slice = _memory.AsSpan((int)address, 4);
		return BinaryPrimitives.ReadUInt32LittleEndian(slice);
	}
	public void WriteByte (uint address, byte value)
	{
		_memory[address] = value;
	}
	public void WriteHalfWord (uint address, ushort value)
	{
		Span<byte> slice = _memory.AsSpan((int)address, 2);
		BinaryPrimitives.WriteUInt16LittleEndian(slice, value);
	}

	public void WriteWord(uint address, uint value)
	{
		Span<byte> slice = _memory.AsSpan((int)address, 4);
		BinaryPrimitives.WriteUInt32LittleEndian(slice, value);
	}
}
