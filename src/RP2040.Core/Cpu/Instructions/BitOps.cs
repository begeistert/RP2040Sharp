using System.Runtime.CompilerServices;

namespace RP2040.Core.Cpu.Instructions;

public static class BitOps
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Ands(ushort opcode, CortexM0Plus cpu)
	{
		var rdn = opcode & 0x7;
		var rm = (opcode >> 3) & 0x7;
		
		ref var ptrRdn = ref cpu.Registers[rdn];
		var valRm = cpu.Registers[rm];
		
		var result = ptrRdn & valRm;
		ptrRdn = result;

		cpu.Registers.N = (int)result < 0; 
		cpu.Registers.Z = (result == 0);
	}
}
