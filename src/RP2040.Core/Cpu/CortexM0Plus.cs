using System.Runtime.CompilerServices;
using RP2040.Core.Memory;

namespace RP2040.Core.Cpu;

public class CortexM0Plus
{
	public readonly IMemoryBus Bus;
    
	// El estado de los registros.
	// Al ser un struct, 'Registers' aquí es un CAMPO que contiene los datos directamente.
	public Registers Registers; 
	private readonly InstructionDecoder _decoder;

	public CortexM0Plus(IMemoryBus bus)
	{
		Bus = bus;
		_decoder = new InstructionDecoder();
		Reset();
	}

	public void Reset()
	{
		// Lógica de Reset del ARM Cortex-M0+
		// 1. Leer el Stack Pointer inicial desde la dirección 0x00000000
		Registers.SP = Bus.ReadWord(0x00000000);
        
		// 2. Leer el Reset Vector (PC inicial) desde 0x00000004
		Registers.PC = Bus.ReadWord(0x00000004);
        
		// Limpiar flags
		Registers.N = false;
		Registers.Z = false;
		Registers.C = false;
		Registers.V = false;
	}

	// Este método será el corazón del bucle
	public void Step()
	{
		// 1. FETCH
		uint pc = Registers.PC;
		ushort opcode = Bus.ReadHalfWord(pc);
        
		// Avanzamos PC (Thumb instructions son 2 bytes, algunas 4, M0+ usa mayormente 2)
		Registers.PC += 2;

		// 2. DECODE & EXECUTE
		ExecuteInstruction(opcode);
	}

	private void ExecuteInstruction(ushort opcode)
	{
		// Buscamos el handler en la tabla (Array lookup = Muy rápido)
		_decoder.Dispatch(opcode, this);
	}
	
	/// <summary>
	/// Actualiza los flags N y Z basados en el resultado.
	/// Usado por casi todas las instrucciones de movimiento y lógicas.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void SetZN(uint result)
	{
		Registers.Z = (result == 0);
		Registers.N = (result & 0x80000000) != 0; // Bit 31 set
	}
}
