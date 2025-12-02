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
		var regCount = (uint)BitOperations.PopCount (mask);

		var sp = cpu.Registers.SP;
		var finalSp = sp + (regCount * 4);

		if ((sp >> 28) == BusInterconnect.REGION_SRAM) {
			var rawPtr = cpu.Bus.PtrSram + (sp & BusInterconnect.MASK_SRAM);

			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = Unsafe.ReadUnaligned<uint> (rawPtr);

				rawPtr += 4;
				mask &= (mask - 1);
			}
		}
		else {
			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = cpu.Bus.ReadWord (sp);
				sp += 4;
				mask &= (mask - 1);
			}
		}

		cpu.Registers.SP = finalSp;
		cpu.Cycles += 1 + regCount;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static void PopPc (ushort opcode, CortexM0Plus cpu)
	{
		var mask = (uint)(opcode & 0xFF);
		var regCount = (uint)BitOperations.PopCount (mask);

		var sp = cpu.Registers.SP;
		var finalSp = sp + ((regCount + 1) * 4);

		if ((sp >> 28) == BusInterconnect.REGION_SRAM) {
			var rawPtr = cpu.Bus.PtrSram + (sp & BusInterconnect.MASK_SRAM);

			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = Unsafe.ReadUnaligned<uint> (rawPtr);
				rawPtr += 4;
				mask &= (mask - 1);
			}
			var newPc = Unsafe.ReadUnaligned<uint> (rawPtr);

			cpu.Registers.PC = newPc & 0xFFFFFFFE;
		}
		else {
			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				cpu.Registers[regIdx] = cpu.Bus.ReadWord (sp);
				sp += 4;
				mask &= (mask - 1);
			}
			var newPc = cpu.Bus.ReadWord (sp);
			cpu.Registers.PC = newPc & 0xFFFFFFFE;
		}

		cpu.Registers.SP = finalSp;
		cpu.Cycles += 4 + regCount;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static void Push (ushort opcode, CortexM0Plus cpu)
	{
		var mask = (uint)(opcode & 0xFF);
		var regCount = (uint)BitOperations.PopCount (mask);
		var totalBytes = regCount * 4;

		var oldSp = cpu.Registers.SP;
		var newSp = oldSp - totalBytes;

		if ((newSp >> 28) == BusInterconnect.REGION_SRAM) {
			var rawPtr = cpu.Bus.PtrSram + (newSp & BusInterconnect.MASK_SRAM);

			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);

				Unsafe.WriteUnaligned<uint> (rawPtr, cpu.Registers[regIdx]);

				rawPtr += 4;
				mask &= (mask - 1);
			}
		}
		else {
			var writePtr = newSp;
			while (mask != 0) {
				var regIdx = BitOperations.TrailingZeroCount (mask);
				var val = cpu.Registers[regIdx];
				cpu.Bus.WriteWord (writePtr, val);

				writePtr += 4;
				mask &= (mask - 1);
			}
		}

		cpu.Registers.SP = newSp;
		cpu.Cycles += regCount;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void PushLr(ushort opcode, CortexM0Plus cpu)
	{
		var mask = (uint)(opcode & 0xFF);
		var regCount = (uint)BitOperations.PopCount(mask);
		var totalBytes = (regCount + 1) * 4; // +1 because of LR

		var oldSp = cpu.Registers.SP;
		var newSp = oldSp - totalBytes;

		if ((newSp >> 28) == BusInterconnect.REGION_SRAM) 
		{
			var rawPtr = (uint*)(cpu.Bus.PtrSram + (newSp & BusInterconnect.MASK_SRAM));

			while (mask != 0) 
			{
				var regIdx = BitOperations.TrailingZeroCount(mask);
				*rawPtr = cpu.Registers[regIdx];
				rawPtr++;
				mask &= (mask - 1);
			}
       
			*rawPtr = cpu.Registers.LR;
		}
		else 
		{
			var writePtr = newSp;
			while (mask != 0) 
			{
				var regIdx = BitOperations.TrailingZeroCount(mask);
				cpu.Bus.WriteWord(writePtr, cpu.Registers[regIdx]);
				writePtr += 4;
				mask &= (mask - 1);
			}
			cpu.Bus.WriteWord(writePtr, cpu.Registers.LR);
		}

		cpu.Registers.SP = newSp;
		cpu.Cycles += regCount + 1; // +1 por LR
	}
}
