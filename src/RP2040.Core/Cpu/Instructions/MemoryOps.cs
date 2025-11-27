using System.Numerics;
using System.Runtime.CompilerServices;
using RP2040.Core.Memory;

namespace RP2040.Core.Cpu.Instructions;

public unsafe static class MemoryOps
{
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static void Pop (ushort opcode, CortexM0Plus cpu)
	{
		var mask = (uint)(opcode & 0xFF);
		var sp = cpu.Registers.SP; 

		if (sp >> 28 == BusInterconnect.REGION_SRAM) {
			var ptr = cpu.Bus.PtrSram;
			const uint memMask = BusInterconnect.MASK_SRAM;

			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = Unsafe.ReadUnaligned<uint> (ptr + (sp & memMask));
				sp += 4;
				cpu.Cycles++;
				mask &= (mask - 1);
			}
		}
		else {
			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = cpu.Bus.ReadWord (sp);
				sp += 4;
				cpu.Cycles++;
				mask &= (mask - 1);
			}
		}
		cpu.Registers.SP = sp;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static void PopPc (ushort opcode, CortexM0Plus cpu)
	{
		var mask = (uint)(opcode & 0xFF);
		var sp = cpu.Registers.SP;
		
		if (sp >> 28 == BusInterconnect.REGION_SRAM) {
			var ptr = cpu.Bus.PtrSram;
			const uint memMask = BusInterconnect.MASK_SRAM;

			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = Unsafe.ReadUnaligned<uint> (ptr + (sp & memMask));
				sp += 4;
				cpu.Cycles++;
				mask &= (mask - 1);
			}

			var newPc = Unsafe.ReadUnaligned<uint> (ptr + (sp & memMask));
			sp += 4;
			cpu.Registers.PC = newPc & 0xFFFFFFFE;
		}
		else {
			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = cpu.Bus.ReadWord (sp);
				sp += 4;
				cpu.Cycles++;
				mask &= (mask - 1);
			}

			var newPc = cpu.Bus.ReadWord (sp);
			sp += 4;
			cpu.Registers.PC = newPc & 0xFFFFFFFE;
		}
		
		cpu.Cycles += 2;
		cpu.Registers.SP = sp;
	}
}
