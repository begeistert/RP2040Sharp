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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AsrsImm5(ushort opcode, CortexM0Plus cpu)
	{
		var rd = opcode & 0x7;
		var rm = (opcode >> 3) & 0x7;
		var imm5 = (opcode >> 6) & 0x1F;

		ref var ptrRd = ref cpu.Registers[rd];
		var valRm = cpu.Registers[rm];

		uint result;
		bool carry;

		if (imm5 == 0)
		{
			result = (uint)((int)valRm >> 31);
			carry = (int)valRm < 0;
		}
		else
		{
			result = (uint)((int)valRm >> imm5);
			carry = ((valRm >> (imm5 - 1)) & 1) != 0;
		}

		ptrRd = result;

		cpu.Registers.N = (int)result < 0; // Bit 31
		cpu.Registers.Z = (result == 0);
		cpu.Registers.C = carry;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AsrsRegister(ushort opcode, CortexM0Plus cpu)
	{
		var rdn = opcode & 0x7;
		var rm = (opcode >> 3) & 0x7;

		ref var ptrRdn = ref cpu.Registers[rdn];
		
		var valRdn = ptrRdn; 
		var valRm = cpu.Registers[rm];

		var shift = (int)(valRm & 0xFF);

		if (shift == 0)
		{
			cpu.Registers.N = (int)valRdn < 0;
			cpu.Registers.Z = (valRdn == 0);
			return;
		}

		uint result;
		bool carry;

		if (shift < 32)
		{
			result = (uint)((int)valRdn >> shift);
			carry = ((valRdn >> (shift - 1)) & 1) != 0;
		}
		else
		{
			result = (uint)((int)valRdn >> 31);
			carry = (int)valRdn < 0;
		}

		ptrRdn = result;

		cpu.Registers.N = (int)result < 0;
		cpu.Registers.Z = (result == 0);
		cpu.Registers.C = carry;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static void Bics (ushort opcode, CortexM0Plus cpu)
	{
		var rdn = opcode & 0x7;
		var rm = (opcode >> 3) & 0x7;
		
		ref var ptrRdn = ref cpu.Registers[rdn];
		var valRm = cpu.Registers[rm];
		
		var result = ptrRdn & ~valRm;
		ptrRdn = result;
		
		cpu.Registers.N = (int)result < 0; 
		cpu.Registers.Z = (result == 0);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static void Eors (ushort opcode, CortexM0Plus cpu)
	{
		var rm = (opcode >> 3) & 0x7;
		var rdn = opcode & 0x7;
		ref var ptrRdn = ref cpu.Registers[rdn];
		var valRm = cpu.Registers[rm];
		
		var result = ptrRdn ^ valRm;
		ptrRdn = result;
		
		cpu.Registers.N = (int)result < 0; 
		cpu.Registers.Z = (result == 0);
	}
}
