using System.Runtime.CompilerServices;
using RP2040.Core.Internals; // Para BitUtils

namespace RP2040.Core.Cpu.Instructions;

public static class BitOps
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void MovsImmediate(ushort opcode, CortexM0Plus cpu)
	{
		// MOVS Rd, #imm8
		// Encoding: 0010 0ddd iiii iiii
		var rd = (int)BitUtils.Extract(opcode, 8, 3);
		var imm8 = BitUtils.Extract(opcode, 0, 8);

		cpu.Registers[rd] = imm8;
		cpu.SetZN(imm8); // Método público/interno de CortexM0Plus
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AddsImmediate3(ushort opcode, CortexM0Plus cpu)
	{
		// ADDS Rd, Rn, #imm3
		// Encoding: 0001 110i iinn nddd
		var rd = (int)BitUtils.Extract(opcode, 0, 3);
		var rn = (int)BitUtils.Extract(opcode, 3, 3);
		var imm3 = BitUtils.Extract(opcode, 6, 3);

		var op1 = cpu.Registers[rn];
		var result = op1 + imm3;

		cpu.Registers[rd] = result;
		cpu.SetFlagsAdd(op1, imm3, result); // Método helper en CortexM0Plus
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AddsImmediate8(ushort opcode, CortexM0Plus cpu)
	{
		// ADDS Rd, #imm8 (Rd también es Rn)
		// Encoding: 0011 0ddd iiii iiii
		int rd = (int)BitUtils.Extract(opcode, 8, 3);
		uint imm8 = BitUtils.Extract(opcode, 0, 8);

		uint op1 = cpu.Registers[rd];
		uint result = op1 + imm8;

		cpu.Registers[rd] = result;
		cpu.SetFlagsAdd(op1, imm8, result);
	}
}
