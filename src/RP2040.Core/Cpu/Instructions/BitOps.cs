using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

		var shiftVal = imm5 == 0 ? 31 : imm5;
		var shiftCarry = (imm5 - 1) & 0x1F;

		var result = (uint)((int)valRm >> shiftVal);
		var carry = ((valRm >> shiftCarry) & 1) != 0;

		ptrRd = result;

		cpu.Registers.N = (int)result < 0; 
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
		
		var effShift = shift < 32 ? shift : 31;
		var effCarryShift = shift < 32 ? (shift - 1) : 31;

		var result = (uint)((int)valRdn >> effShift);
		var carry = ((valRdn >> effCarryShift) & 1) != 0;

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
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void MovToPc(ushort opcode, CortexM0Plus cpu)
	{
		var rm = (opcode >> 3) & 0xF;
        
		ref var pc = ref cpu.Registers.PC;
		var valRm = cpu.Registers[rm];
    
		valRm += (uint)((rm + 1) >> 4) << 1;
		pc = valRm & 0xFFFFFFFE;
		cpu.Cycles++;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void MovToSp(ushort opcode, CortexM0Plus cpu)
	{
		var rm = (opcode >> 3) & 0xF;
		ref var sp = ref cpu.Registers.SP;
    
		var valRm = cpu.Registers[rm];
		valRm += (uint)((rm + 1) >> 4) << 1;

		sp = valRm & 0xFFFFFFFC;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void MovRegister(ushort opcode, CortexM0Plus cpu)
	{
		var rm = (opcode >> 3) & 0xF;
		var rd = ((opcode >> 4) & 0x8) | (opcode & 0x7);

		ref var ptrRd = ref cpu.Registers[rd];
		var valRm = cpu.Registers[rm];
		
		valRm += (uint)((rm + 1) >> 4) << 1;
		ptrRd = valRm;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Movs(ushort opcode, CortexM0Plus cpu)
	{
		var value = (uint)(opcode & 0xFF);

		cpu.Registers[(opcode >> 8) & 7] = value;
		
		cpu.Registers.N = false;
		cpu.Registers.Z = value == 0;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Mvns(ushort opcode, CortexM0Plus cpu)
	{
		var rm = (opcode >> 3) & 7;
		var rd = opcode & 7;
		
		ref var ptrRd = ref cpu.Registers[rd];
		var valRm = cpu.Registers[rm];
		
		ptrRd = ~valRm;
		
		cpu.Registers.N = (int)ptrRd < 0; 
		cpu.Registers.Z = (ptrRd == 0);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LslsImm5(ushort opcode, CortexM0Plus cpu)
	{
		var imm5 = (opcode >> 6) & 0x1F;
		var rm = (opcode >> 3) & 0x7;
		var rd = opcode & 0x7;
    
		ref var ptrRd = ref cpu.Registers[rd];
		var valRm = cpu.Registers[rm];
    
		var extended = (ulong)valRm << imm5;
		var result = (uint)extended;
		var carry = (extended & 0x1_0000_0000) != 0;
    
		ptrRd = result;
    
		cpu.Registers.N = (int)result < 0;
		cpu.Registers.Z = (result == 0);
		cpu.Registers.C = carry;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LslsZero(ushort opcode, CortexM0Plus cpu)
	{
		var rm = (opcode >> 3) & 0x7;
		var rd = opcode & 0x7;

		ref var ptrRd = ref cpu.Registers[rd];
		var valRm = cpu.Registers[rm];
		ptrRd = valRm;

		cpu.Registers.N = (int)valRm < 0;
		cpu.Registers.Z = (valRm == 0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void LslsRegister(ushort opcode, CortexM0Plus cpu)
	{
		var rdn = opcode & 0x7;
		var rm = (opcode >> 3) & 0x7;

		ref var ptrRdn = ref cpu.Registers[rdn];
    
		var valRdn = ptrRdn;
		var shift = (int)(cpu.Registers[rm] & 0xFF); 

		var extended = (ulong)valRdn << shift;
		var result = shift >= 32 ? 0 : (uint)extended;

		var calcCarry = (extended & 0x1_0000_0000) != 0;
		var finalCarry = (shift == 0) ? (cpu.Registers.GetC() != 0) : calcCarry;

		ptrRdn = result;
    
		cpu.Registers.N = (int)result < 0;
		cpu.Registers.Z = (result == 0);
		cpu.Registers.C = finalCarry;
	}
}
