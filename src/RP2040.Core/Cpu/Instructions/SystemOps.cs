using System.Runtime.CompilerServices;
namespace RP2040.Core.Cpu.Instructions;

public class SystemOps
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Barrier(ushort opcodeH1, CortexM0Plus cpu)
	{
		cpu.Registers.PC += 2;
		cpu.Cycles += 2;
	}
}
