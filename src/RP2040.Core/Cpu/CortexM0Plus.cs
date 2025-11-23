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

	/// <summary>
	/// Actualiza N, Z, C, V para operaciones de SUMA (ADD, CMN).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void SetFlagsAdd(uint op1, uint op2, uint result)
	{
		Registers.Z = (result == 0);
		Registers.N = (result & 0x80000000) != 0;
        
		// Carry: Si el resultado es menor que uno de los operandos (overflow de unsigned)
		Registers.C = result < op1;
        
		// Overflow (Signed): Si operando 1 y 2 tienen el mismo signo, 
		// y el resultado tiene signo distinto.
		// Fórmula mágica: (~(op1 ^ op2) & (op1 ^ result)) & 0x80000000
		Registers.V = ((~(op1 ^ op2) & (op1 ^ result)) & 0x80000000) != 0;
	}

	/// <summary>
	/// Actualiza N, Z, C, V para operaciones de RESTA (SUB, CMP).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void SetFlagsSub(uint op1, uint op2, uint result)
	{
		Registers.Z = (result == 0);
		Registers.N = (result & 0x80000000) != 0;
        
		// Carry en ARM para resta es "Not Borrow". 
		// C = 1 si no hubo préstamo (op1 >= op2).
		Registers.C = op1 >= op2;
        
		// Overflow: (op1 ^ op2) & (op1 ^ result) & 0x80000000
		Registers.V = (((op1 ^ op2) & (op1 ^ result)) & 0x80000000) != 0;
	}
}
