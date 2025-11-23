using System.Runtime.CompilerServices;
namespace RP2040.Core.Cpu.Instructions;

public class ArithmeticOps
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsImmediate3(ushort opcode, CortexM0Plus cpu)
    {
        // ADDS Rd, Rn, #imm3
        var rd = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var imm3 = (uint)((opcode >> 6) & 0x7);

        // Lectura optimizada con Unsafe (via indexer)
        var valRn = cpu.Registers[rn];
        
        // Inlined Math: Evitamos overhead de llamada para la instrucción más común
        var res = AddWithFlags(cpu, valRn, imm3, carryIn: 0);
        cpu.Registers[rd] = res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        var valRd = cpu.Registers[rd];
        
        cpu.Registers[rd] = AddWithFlags(cpu, valRd, imm8, carryIn: 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsRegister(ushort opcode, CortexM0Plus cpu)
    {
        var rd = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var rm = (opcode >> 6) & 0x7;

        var valRn = cpu.Registers[rn];
        var valRm = cpu.Registers[rm];

        cpu.Registers[rd] = AddWithFlags(cpu, valRn, valRm, carryIn: 0);
    }

    // --- ADCS (Suma con Acarreo y flags) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adcs(ushort opcode, CortexM0Plus cpu)
    {
        var rd = opcode & 0x7;
        var rm = (opcode >> 3) & 0x7;

        var valRd = cpu.Registers[rd];
        var valRm = cpu.Registers[rm];

        // Conversión branchless de bool a int (0 o 1)
        // Unsafe.As<bool, byte> es una opción, pero el ternario suele optimizarse bien.
        var carryIn = cpu.Registers.GetC ();

        cpu.Registers[rd] = AddWithFlags(cpu, valRd, valRm, carryIn);
    }

    // --- ADD (Sin flags - Usualmente con SP o High Registers) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSpImmediate7(ushort opcode, CortexM0Plus cpu)
    {
        // ADD SP, SP, #imm7 (Encoding T2)
        // Encoding: 1011 0000 0iii iiii
        // Nota: Esta instrucción multiplica el inmediato por 4 (alineación de palabras)
        var imm7 = (uint)((opcode & 0x7F) << 2);

        // NO actualiza flags
        cpu.Registers.SP += imm7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSpImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        // ADD Rd, SP, #imm8
        // Encoding: 1010 1ddd iiii iiii
        // También multiplica por 4
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)((opcode & 0xFF) << 2);

        // 3. Ejecución
        cpu.Registers[rd] = cpu.Registers.SP + imm8;
    }
    
    // (Falta ADD High Register, pero con estos tienes el 90% cubierto)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddHighRegisters(ushort opcode, CortexM0Plus cpu)
    {
        // Encoding: 0100 0100 DNmm mddd (0x4400)
        var rm = (opcode >> 3) & 0xF;
        var dn = ((opcode >> 4) & 0x8) | (opcode & 0x7);
        
        var valDn = (dn == 15) ? cpu.Registers.PC + 2 : cpu.Registers[dn];
        var valRm = cpu.Registers[rm];

        var result = valDn + valRm;

        switch (dn)
        {
            case 15: cpu.Registers.PC = result & 0xFFFFFFFE; break; // Align Halfword
            case 13: cpu.Registers.SP = result & 0xFFFFFFFC; break; // Align Word
            default: cpu.Registers[dn] = result; break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adr(ushort opcode, CortexM0Plus cpu)
    {
        // ADR Rd, label
        // Encoding: 1010 0ddd iiii iiii
    
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        
        var basePc = (cpu.Registers.PC + 2) & 0xFFFFFFFC;
        cpu.Registers[rd] = basePc + (imm8 << 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CmpImmediate(ushort opcode, CortexM0Plus cpu)
    {
        // CMP Rn, #imm8
        // Encoding: 0010 1rrr iiii iiii (0x2800)
        var rn = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        
        var val1 = cpu.Registers[rn];
        
        SubWithFlags(cpu, val1, imm8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubsImmediate3(ushort opcode, CortexM0Plus cpu)
    {
        // SUBS Rd, Rn, #imm3
        var rd = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var imm3 = (uint)((opcode >> 6) & 0x7);
        
        cpu.Registers[rd] = SubWithFlags(cpu, cpu.Registers[rn], imm3);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubsImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        // SUBS Rd, #imm8 (Rd es también Rn)
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        
        cpu.Registers[rd] = SubWithFlags(cpu, cpu.Registers[rd], imm8);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AddWithFlags(CortexM0Plus cpu, uint op1, uint op2, uint carryIn)
    {
        var result64 = (ulong)op1 + op2 + carryIn;
        var result32 = (uint)result64;
        
        cpu.Registers.N = (int)result32 < 0; 
        cpu.Registers.Z = (result32 == 0);
        cpu.Registers.C = result64 > uint.MaxValue;
        cpu.Registers.V = ((~(op1 ^ op2) & (op1 ^ result32)) & 0x80000000) != 0;

        return result32;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint SubWithFlags(CortexM0Plus cpu, uint op1, uint op2)
    {
        var result = op1 - op2;
        cpu.Registers.N = (int)result < 0;
        cpu.Registers.Z = (result == 0);
        cpu.Registers.C = op1 >= op2;
        cpu.Registers.V = (((op1 ^ op2) & (op1 ^ result)) & 0x80000000) != 0;

        return result;
    }
}
