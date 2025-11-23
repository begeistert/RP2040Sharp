using System.Runtime.CompilerServices;
using RP2040.Core.Memory;

namespace RP2040.Core.Cpu;

public unsafe class CortexM0Plus
{
	public readonly BusInterconnect Bus;
	public Registers Registers; 
	
	readonly InstructionDecoder _decoder;
	
	private byte* _fetchPtr;
	private uint _fetchMask;
	private uint _currentRegionId; // 0x0, 0x1, o 0x2

	public CortexM0Plus(BusInterconnect bus)
	{
		Bus = bus;
		_decoder = new InstructionDecoder();
		Reset();
	}

	public void Reset()
	{
		// RP2040 Boot sequence:
		// SP @ 0x00000000
		Registers.SP = Bus.ReadWord(0x00000000);
		// PC @ 0x00000004
		Registers.PC = Bus.ReadWord(0x00000004);
        
		// Limpiar flags
		Registers.N = false;
		Registers.Z = false;
		Registers.C = false;
		Registers.V = false;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Pide al JIT máxima prioridad
	public void Run(int instructions)
	{
		var decoder = _decoder; 
        
		while (instructions-- > 0)
		{
			// 1. FETCH
			// TODO: Optimización futura -> "Fast Fetch" usando punteros directos si PC está en Flash/RAM para evitar la llamada virtual a Bus.ReadHalfWord.
			var pc = Registers.PC;
			var opcode = Bus.ReadHalfWord(pc);

			// 2. UPDATE PC
			Registers.PC = pc + 2;

			// 3. DECODE & EXECUTE (Inlined)
			decoder.Dispatch(opcode, this);
		}
	}

	// Este método será el corazón del bucle
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Step()
	{
		var pc = Registers.PC;
		var opcode = Bus.ReadHalfWord(pc);
		Registers.PC = pc + 2;
		_decoder.Dispatch(opcode, this);
	}
}
