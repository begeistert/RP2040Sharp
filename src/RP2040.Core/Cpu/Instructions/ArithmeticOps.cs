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

        // Escritura optimizada: Obtenemos el puntero al destino.
        ref var ptrRd = ref cpu.Registers[rd];
        var valRn = cpu.Registers[rn]; // Rn suele ser distinto, leemos por valor
        
        ptrRd = AddWithFlags(cpu, valRn, imm3, carryIn: 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        // ADDS Rd, #imm8 (Rd es Source y Destino)
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        
        // OPTIMIZACIÓN CLAVE: Read-Modify-Write
        // Capturamos la dirección de Rd una sola vez.
        ref var ptrRd = ref cpu.Registers[rd];
        
        // Pasamos 'ptrRd' (se lee implícitamente) y asignamos el resultado a 'ptrRd'
        ptrRd = AddWithFlags(cpu, ptrRd, imm8, carryIn: 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsRegister(ushort opcode, CortexM0Plus cpu)
    {
        // ADDS Rd, Rn, Rm
        var rd = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var rm = (opcode >> 6) & 0x7;

        // Preparamos puntero de escritura
        ref var ptrRd = ref cpu.Registers[rd];
        var valRn = cpu.Registers[rn];
        var valRm = cpu.Registers[rm];

        ptrRd = AddWithFlags(cpu, valRn, valRm, carryIn: 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adcs(ushort opcode, CortexM0Plus cpu)
    {
        // ADCS Rd, Rm (Rd es Source y Destino)
        var rd = opcode & 0x7;
        var rm = (opcode >> 3) & 0x7;

        // Read-Modify-Write optimizado
        ref var ptrRd = ref cpu.Registers[rd];
        var valRm = cpu.Registers[rm];

        var carryIn = cpu.Registers.GetC();

        ptrRd = AddWithFlags(cpu, ptrRd, valRm, carryIn);
    }

    // --- ADD SP (Sin Flags) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSpImmediate7(ushort opcode, CortexM0Plus cpu)
    {
        // ADD SP, SP, #imm7
        // Aquí no necesitamos ref porque SP es un campo directo, no un array indexado.
        // El acceso a cpu.Registers.SP ya es directísimo.
        var imm7 = (uint)((opcode & 0x7F) << 2);
        cpu.Registers.SP += imm7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSpImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        // ADD Rd, SP, #imm8
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)((opcode & 0xFF) << 2);

        // Escritura en Rd
        cpu.Registers[rd] = cpu.Registers.SP + imm8;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddHighRegisters(ushort opcode, CortexM0Plus cpu)
    {
        // ADD (High Registers)
        var rm = (opcode >> 3) & 0xF;
        var dn = ((opcode >> 4) & 0x8) | (opcode & 0x7);
        
        // Aquí usamos acceso normal porque 'dn' puede ser PC(15) o SP(13)
        // y la lógica especial del switch hace difícil usar ref genérico.
        var valDn = (dn == 15) ? cpu.Registers.PC + 2 : cpu.Registers[dn];
        var valRm = cpu.Registers[rm];

        var result = valDn + valRm;

        switch (dn)
        {
            case 15: cpu.Registers.PC = result & 0xFFFFFFFE; break;
            case 13: cpu.Registers.SP = result & 0xFFFFFFFC; break;
            default: cpu.Registers[dn] = result; break; // Aquí se usa el indexador (set) optimizado
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adr(ushort opcode, CortexM0Plus cpu)
    {
        // ADR Rd, label
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        
        var basePc = (cpu.Registers.PC + 2) & 0xFFFFFFFC;
        
        // Escritura directa
        cpu.Registers[rd] = basePc + (imm8 << 2);
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public static void Cmn (ushort opcode, CortexM0Plus cpu)
    {
        var rm = (opcode >> 3) & 0x7;
        var rn = opcode & 0x7;
    
        var valRm = cpu.Registers[rm];
        var valRn = cpu.Registers[rn];
        
        AddWithFlags(cpu, valRn, valRm, carryIn: 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CmpImmediate(ushort opcode, CortexM0Plus cpu)
    {
        var rn = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        
        var val1 = cpu.Registers[rn];
        
        SubWithFlags(cpu, val1, imm8);
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public static void CmpRegister (ushort opcode, CortexM0Plus cpu)
    {
        var rm = (opcode >> 3) & 0x7; 
        var rn = opcode & 0x7;
        
        var val1 = cpu.Registers[rn];
        var val2 = cpu.Registers[rm];
        
        SubWithFlags(cpu, val1, val2);
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public static void CmpHighRegister (ushort opcode, CortexM0Plus cpu)
    {
        var rm = (opcode >> 3) & 0xF;
        var rn = ((opcode >> 4) & 0x8) | (opcode & 0x7);
    
        var valRn = cpu.Registers[rn];
        var valRm = cpu.Registers[rm];
        
        valRn += (uint)((rn + 1) >> 4) << 1;
        valRm += (uint)((rm + 1) >> 4) << 1;

        SubWithFlags(cpu, valRn, valRm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubsImmediate3(ushort opcode, CortexM0Plus cpu)
    {
        // SUBS Rd, Rn, #imm3
        var rd = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var imm3 = (uint)((opcode >> 6) & 0x7);
        
        ref var ptrRd = ref cpu.Registers[rd];
        var valRn = cpu.Registers[rn];
        
        ptrRd = SubWithFlags(cpu, valRn, imm3);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubsImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        // SUBS Rd, #imm8 (Rd es Source y Destino)
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);
        
        // OPTIMIZACIÓN: Read-Modify-Write
        ref var ptrRd = ref cpu.Registers[rd];
        
        ptrRd = SubWithFlags(cpu, ptrRd, imm8);
    }
    
    // =============================================================
    // MATH HELPERS (Sin cambios, reciben valores)
    // =============================================================
    
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