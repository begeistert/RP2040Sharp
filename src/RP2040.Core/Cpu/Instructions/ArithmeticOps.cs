using System.Runtime.CompilerServices;
using RP2040.Core.Internals;
namespace RP2040.Core.Cpu.Instructions;

public class ArithmeticOps
{
	// =============================================================
    // IMPLEMENTACIONES PÚBLICAS (Handlers para el Decoder)
    // =============================================================
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsImmediate3(ushort opcode, CortexM0Plus cpu)
    {
        // ADDS Rd, Rn, #imm3
        // Encoding: 0001 110i iinn nddd
        var rd = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var imm3 = (uint)((opcode >> 6) & 0x7);

        var valRn = cpu.Registers[rn];
        // ADDS siempre actualiza flags (useCarry: false, updateFlags: true)
        cpu.Registers[rd] = AddImpl(cpu, valRn, imm3, useCarry: false, updateFlags: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        // ADDS Rd, #imm8 (Rd también es Rn)
        // Encoding: 0011 0ddd iiii iiii
        var rd = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF);

        var valRd = cpu.Registers[rd];
        cpu.Registers[rd] = AddImpl(cpu, valRd, imm8, useCarry: false, updateFlags: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddsRegister(ushort opcode, CortexM0Plus cpu)
    {
        // ADDS Rd, Rn, Rm
        // Encoding: 0001 100m mmnn nddd
        var rd = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var rm = (opcode >> 6) & 0x7;

        var valRn = cpu.Registers[rn];
        var valRm = cpu.Registers[rm];

        cpu.Registers[rd] = AddImpl(cpu, valRn, valRm, useCarry: false, updateFlags: true);
    }

    // --- ADCS (Suma con Acarreo y flags) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adcs(ushort opcode, CortexM0Plus cpu)
    {
        // ADCS Rd, Rm (Rd es también Rn)
        // Encoding: 0100 0001 01mm mddd
        var rd = opcode & 0x7;
        var rm = (opcode >> 3) & 0x7;

        var valRd = cpu.Registers[rd];
        var valRm = cpu.Registers[rm];

        // Aquí SI usamos el carry actual (useCarry: true)
        cpu.Registers[rd] = AddUpdateFlags(cpu, valRd, valRm, cpu.Registers.C);
    }

    // --- ADD (Sin flags - Usualmente con SP o High Registers) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSpImm7(ushort opcode, CortexM0Plus cpu)
    {
        // ADD SP, SP, #imm7 (Encoding T2)
        // Encoding: 1011 0000 0iii iiii
        // Nota: Esta instrucción multiplica el inmediato por 4 (alineación de palabras)
        var imm7 = (uint)((opcode & 0x7F) << 2);

        // NO actualiza flags
        cpu.Registers.SP += imm7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSpImm8(ushort opcode, CortexM0Plus cpu)
    {
        // ADD Rd, SP, #imm8
        // Encoding: 1010 1ddd iiii iiii
        // También multiplica por 4
        var rd = (int)BitUtils.Extract(opcode, 8, 3);
        var imm8 = (uint)(byte)opcode << 2;

        // 3. Ejecución
        cpu.Registers[rd] = cpu.Registers.SP + imm8;
    }
    
    // (Falta ADD High Register, pero con estos tienes el 90% cubierto)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddHighRegisters(ushort opcode, CortexM0Plus cpu)
    {
        // Encoding: 0100 0100 DNmm mddd (0x4400)
        
        // 1. Extracción con BitUtils (Más limpio)
        // Rm está en los bits 3-6 (longitud 4)
        var rm = (int)BitUtils.Extract(opcode, 3, 4);

        // 2. Extracción de Rdn (El Trucazo)
        // Rdn tiene el bit alto en la posición 7 (D) y los bajos en 0-2 (ddd).
        // BitUtils no extrae rangos separados, así que usamos aritmética pura optimizada.
        // (opcode >> 4) & 0x8  -> Mueve bit 7 a pos 3 (vale 8 si está activo)
        // opcode & 0x7         -> Bits bajos
        var dn = ((opcode >> 4) & 0x8) | (opcode & 0x7);

        // 3. Lectura de Operandos (Branchless optimization where possible)
        // Si DN es 15 (PC), leemos PC + 2 (ajuste de pipeline). Si no, registro normal.
        var valDn = (dn == 15) ? cpu.Registers.PC + 2 : cpu.Registers[dn];
        var valRm = cpu.Registers[rm];

        // 4. Cálculo (ADD puro, sin flags)
        var result = valDn + valRm;

        // 5. Escritura (Switch expression para claridad y velocidad)
        // En C# moderno esto compila a una tabla de saltos muy eficiente.
        switch (dn)
        {
            case 15: // PC (R15)
                // Escritura en PC -> Branch -> Debe alinear a Halfword (bit 0 = 0)
                cpu.Registers.PC = result & 0xFFFFFFFE;
                break;

            case 13: // SP (R13)
                // Escritura en SP -> Debe alinear a Word (bits 0-1 = 0)
                cpu.Registers.SP = result & 0xFFFFFFFC;
                break;

            default: // Registros R0-R12, R14
                cpu.Registers[dn] = result;
                break;
        }
    }

    // =============================================================
    // LÓGICA COMPARTIDA (Privada)
    // =============================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AddUpdateFlags (CortexM0Plus cpu, uint op1, uint op2, bool carryIn = false)
    {
        var carryIntValue = carryIn ? 1u : 0u;
        var result64 = (ulong)op1 + op2 + carryIntValue;
        var result32 = (uint)result64;
        
        // Flags
        cpu.Registers.N = (result32 & 0x80000000) != 0;
        cpu.Registers.Z = (result32 == 0);
        
        // Carry: La magia de ulong. Si result64 > result32, nos pasamos.
        cpu.Registers.C = result64 != result32;

        // Overflow (V): Usamos la lógica de signos con long
        var signedSum = (long)(int)op1 + (int)op2 + carryIntValue;
        cpu.Registers.V = signedSum != (int)result32;

        return result32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AddImpl(CortexM0Plus cpu, uint op1, uint op2, bool useCarry, bool updateFlags)
    {
        // Si es ADCS, sumamos 1 si el flag C está activo
        var carryIn = (useCarry && cpu.Registers.C) ? 1u : 0u;
        
        // Usamos long para detectar el desbordamiento de 32 bits fácilmente
        var result64 = (ulong)op1 + op2 + carryIn;
        var result32 = (uint)result64;

        if (!updateFlags)
            return result32;
        cpu.Registers.N = (result32 & 0x80000000) != 0;
        cpu.Registers.Z = (result32 == 0);

        // Carry: Si el resultado no cabe en 32 bits
        cpu.Registers.C = result64 > 0xFFFFFFFF;

        // Overflow (Signed): 
        // Ocurre si sumamos dos positivos y da negativo, o dos negativos y da positivo.
        // Fórmula: (~(op1 ^ op2) & (op1 ^ result)) & 0x80000000
        // Nota: Para ADCS la fórmula es más compleja, pero esta aproximación estándar suele bastar para M0+.
        // Una forma precisa es verificar si los signos de las entradas son iguales 
        // y el signo de la salida es diferente.
        var op1Sign = (op1 & 0x80000000) != 0;
        var op2Sign = (op2 & 0x80000000) != 0;
        var resSign = (result32 & 0x80000000) != 0;
            
        cpu.Registers.V = (op1Sign == op2Sign) && (op1Sign != resSign);

        return result32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CmpImmediate(ushort opcode, CortexM0Plus cpu)
    {
        // CMP Rn, #imm8
        // Encoding: 0010 1rrr iiii iiii (0x2800)
        var rn = (int)BitUtils.Extract(opcode, 8, 3);
        var imm8 = BitUtils.Extract(opcode, 0, 8);
        
        var val1 = cpu.Registers[rn];
        
        // Llamamos a la lógica común SIN guardar resultado (discardResult: true)
        SubImpl(cpu, val1, imm8, discardResult: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubsImmediate3(ushort opcode, CortexM0Plus cpu)
    {
        // SUBS Rd, Rn, #imm3
        var rd = (int)BitUtils.Extract(opcode, 0, 3);
        var rn = (int)BitUtils.Extract(opcode, 3, 3);
        var imm3 = BitUtils.Extract(opcode, 6, 3);
        
        var result = SubImpl(cpu, cpu.Registers[rn], imm3, discardResult: false);
        cpu.Registers[rd] = result;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubsImmediate8(ushort opcode, CortexM0Plus cpu)
    {
        // SUBS Rd, #imm8 (Rd es también Rn)
        var rd = (int)BitUtils.Extract(opcode, 8, 3);
        var imm8 = BitUtils.Extract(opcode, 0, 8);
        
        var result = SubImpl(cpu, cpu.Registers[rd], imm8, discardResult: false);
        cpu.Registers[rd] = result;
    }

    // ... Aquí irían ADD, ADC, MUL ...

    // =============================================================
    // LÓGICA COMPARTIDA (Privada)
    // =============================================================

    /// <summary>
    /// Realiza la resta (Op1 - Op2), actualiza flags y devuelve el resultado.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint SubImpl(CortexM0Plus cpu, uint op1, uint op2, bool discardResult)
    {
        var result = op1 - op2;

        // Actualización de Flags (N, Z, C, V)
        // Esta lógica debe coincidir con SetFlagsSub que definimos en CortexM0Plus
        // O podemos llamar al método del CPU si lo hiciste público/internal.
        cpu.SetFlagsSub(op1, op2, result);

        return result;
    }
}
