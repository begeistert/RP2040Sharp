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
		
		UpdateFetchCache(Registers.PC);
        
		// Limpiar flags
		Registers.N = false;
		Registers.Z = false;
		Registers.C = false;
		Registers.V = false;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void UpdateFetchCache(uint pc)
	{
		_currentRegionId = pc >> 28; // High Nibble

		switch (_currentRegionId)
		{
			case BusInterconnect.REGION_FLASH: // 0x1
				_fetchPtr = Bus.PtrFlash;
				_fetchMask = Bus.MaskFlash;
				break;
			case BusInterconnect.REGION_SRAM: // 0x2
				_fetchPtr = Bus.PtrSram;
				_fetchMask = Bus.MaskSram;
				break;
			case BusInterconnect.REGION_BOOTROM: // 0x0
				_fetchPtr = Bus.PtrBootRom;
				_fetchMask = Bus.MaskBootRom;
				break;
			default:
				// Ejecución fuera de memoria mapeada (Crash o Periféricos no ejecutables)
				_fetchPtr = null; 
				break;
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Pide al JIT máxima prioridad
	public void Run(int instructions)
	{
		var decoder = _decoder;
		
		byte* fetchPtr = _fetchPtr;
		uint fetchMask = _fetchMask;
		uint regionId = _currentRegionId;
       
		while (instructions-- > 0)
		{
			var pc = Registers.PC;

			if ((pc >> 28) != regionId) 
			{
				// FALLBACK: Cambio de región (Salto largo o retorno de interrupción)
				// Actualizamos la caché local
				UpdateFetchCache(pc);
				fetchPtr = _fetchPtr;
				fetchMask = _fetchMask;
				regionId = _currentRegionId;
             
				// Si saltamos a la nada, abortamos o manejamos error
				if (fetchPtr == null) break; 
			}

			// EJECUCIÓN OPTIMIZADA
			// Leemos directo del puntero. Sin 'if' de RAM vs Flash.
			// Solo un AND para la máscara.
			var opcode = Unsafe.ReadUnaligned<ushort>(fetchPtr + (pc & fetchMask));
			
			Registers.PC = pc + 2;
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
