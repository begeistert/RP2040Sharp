using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RP2040.Core.Cpu.Instructions; 

using unsafe InstructionHandler = delegate* managed<ushort, RP2040.Core.Cpu.CortexM0Plus, void>;

namespace RP2040.Core.Cpu;

public unsafe class InstructionDecoder : IDisposable
{
    private readonly InstructionHandler[] _lookupTable = new InstructionHandler[65536];
    private GCHandle _pinnedHandle;
    private readonly InstructionHandler* _fastTablePtr;
    
    private readonly struct OpcodeRule (ushort mask, ushort pattern, InstructionHandler handler)
    {
        public readonly ushort Mask = mask;
        public readonly ushort Pattern = pattern;
        public readonly InstructionHandler Handler = handler;
    }

    public InstructionDecoder()
    {
        _pinnedHandle = GCHandle.Alloc(_lookupTable, GCHandleType.Pinned);
        _fastTablePtr = (InstructionHandler*)_pinnedHandle.AddrOfPinnedObject();
        
        InstructionHandler undefinedPtr = &HandleUndefined;
        fixed (InstructionHandler* ptrToArr = _lookupTable)
        {
            new Span<nuint>(ptrToArr, _lookupTable.Length).Fill((nuint)undefinedPtr);
        }

        ReadOnlySpan<OpcodeRule> rules = [
            // DMB, DSB, ISB
            // Mask: 1111 1111 1111 1111 (FFFF) -> Pattern: 1111 0011 1011 1111 (F3BF)
            new OpcodeRule(0xFFFF, 0xF3BF, &SystemOps.Barrier),
            // ADCS (Rd, Rm)
            // Mask: 1111 1111 1100 0000 (FFC0) -> Pattern: 0100 0001 0100 0000 (4140)
            new OpcodeRule (0xFFC0, 0x4140, &ArithmeticOps.Adcs),
            // ADD (SP + imm7)
            // Mask: 1111 1111 1000 0000 (FF80) -> Pattern: 1011 0000 0000 0000 (B000)
            new OpcodeRule(0xFF80, 0xB000, &ArithmeticOps.AddSpImmediate7),
            // ADD (Rd = SP + imm8)
            // Mask: 1111 1000 0000 0000 (F800) -> Pattern: 1010 1000 0000 0000 (A800)
            new OpcodeRule(0xF800, 0xA800, &ArithmeticOps.AddSpImmediate8),
            // ADD (High Registers) - Encoding T2
            // Cubre: ADD Rd, Rm (donde alguno es > R7)
            new OpcodeRule(0xFF00, 0x4400, &ArithmeticOps.AddHighRegisters),
            // ADDS (Rd, Rn, Rm) - Encoding T1 Register
            // Mask: 1111 1110 0000 0000 (FE00) -> Pattern: 0001 1000 0000 0000 (1800)
            new OpcodeRule(0xFE00, 0x1800, &ArithmeticOps.AddsRegister),
            // ADDS (Rd, Rn, imm3)
            new OpcodeRule(0xFE00, 0x1C00, &ArithmeticOps.AddsImmediate3),
            // ADDS (Rd, imm8)
            new OpcodeRule(0xF800, 0x3000, &ArithmeticOps.AddsImmediate8),
            // ADR (Rd, imm8)
            // Mask: 1111 1000 0000 0000 -> Pattern: 1010 0000 0000 0000 (0000)
            new OpcodeRule(0xF800, 0xA000, &ArithmeticOps.Adr),
            
            // ANDS (Rn, Rm)
            // Mask: 1111 1111 1100 0000 -> Pattern: 0100 0000 0000 0000 (4000)
            new OpcodeRule(0xFFC0, 0x4000, &BitOps.Ands),
            // ASRS (Rd, Rm, imm5)
            // Mask: 1111 1000 0000 0000 (F800) -> Pattern: 0001 0000 0000 0000 (1000)
            new OpcodeRule(0xF800, 0x1000, &BitOps.AsrsImm5),
            // ASRS (Register) - Encoding T2
            // Mask: 1111 1111 1100 0000 (0xFFC0) -> Pattern: 0100 0001 0000 0000 (0x4100)
            new OpcodeRule(0xFFC0, 0x4100, &BitOps.AsrsRegister),
            // BICS (Rdn, Rm)
            // Mask: 1111 1111 1100 0000 (0xFFC0) ->  Pattern: 0100 0011 1000 0000 (0x4380)
            new OpcodeRule(0xFFC0, 0x4380, &BitOps.Bics),
            // BL (Branch with Link)
            // H1 Mask: 1111 1000 0000 0000 (F800) -> Pattern: 1111 0000 0000 0000 (F000)
            new OpcodeRule(0xF800, 0xF000, &FlowOps.Bl),
            // BLX Rm
            // Mask: 1111 1111 1000 0111 (FF87) -> Pattern: 0100 0111 1000 0000 (4780)
            new OpcodeRule(0xFF87, 0x4780, &FlowOps.Blx),
            
            // B (Conditional) - T1
            // Mask: 1111 0000 0000 0000 (F000) -> Pattern: 1101 0000 0000 0000 (D000)
            // Nota: Esto captura SVC (cond=1111). Debemos asegurarnos de que SVC tenga prioridad 
            // o filtrar en el handler. 
            // MEJOR ESTRATEGIA: Filtrar cond != 1110 (UDF) y != 1111 (SVC) en la máscara es difícil.
            // Lo ideal es registrar SVC *antes* o usar una máscara más específica para SVC.

            // Opción Recomendada: Registrar B Conditional genérico, y dentro del switch el default maneja 0xE/0xF.
            // Y registrar SVC (0xDF) aparte con mayor prioridad en el array de reglas o asegurando que su patrón sea único.

            // B (Cond)
            new OpcodeRule(0xF000, 0xD000, &FlowOps.BranchConditional),

            // B (Unconditional) - T2
            // Mask: 1111 1000 0000 0000 (F800) -> Pattern: 1110 0000 0000 0000 (E000)
            new OpcodeRule(0xF800, 0xE000, &FlowOps.Branch),
            
            // BX Rm
            // Mask: 1111 1111 1000 0111 (FF87) -> Pattern: 0100 0111 0000 0000 (4700)
            new OpcodeRule(0xFF87, 0x4700, &FlowOps.Bx),
            
            // CMN (Rn, Rm)
            // Mask: 1111 1111 1100 0000 (FFC0) -> Pattern: 0100 0010 1100 0000 (42C0)
            new OpcodeRule(0xFFC0, 0x42C0, &ArithmeticOps.Cmn),
            
            // CMP Rn, #imm8
            // Encoding: 0010 1rrr iiii iiii (0x2800)
            // Mask: F800 -> Pattern: 2800
            new OpcodeRule(0xF800, 0x2800, &ArithmeticOps.CmpImmediate),
            
            // CMP Rn, Rm (Low Registers - Encoding T1)
            // Mask: 1111 1111 1100 0000 (FFC0) -> Pattern: 0100 0010 1000 0000 (4280)
            new OpcodeRule(0xFFC0, 0x4280, &ArithmeticOps.CmpRegister),

            // CMP Rn, Rm (High Registers - Encoding T2)
            // Mask: 1111 1111 0000 0000 (FF00) -> Pattern: 0100 0101 0000 0000 (4500)
            new OpcodeRule(0xFF00, 0x4500, &ArithmeticOps.CmpHighRegister),
            
            // EORS Rdn, Rm
            // Mask: 1111 1111 1100 0000 (FFC0) -> Pattern: 0100 0000 0100 0000 (4040)
            new OpcodeRule(0xFFC0, 0x4040, &BitOps.Eors),
            
            // MOV Rd, Rm (High Registers / No Flags)
            // Mask: FF00 -> Pattern: 4600
            new OpcodeRule(0xFF00, 0x4600, &BitOps.Mov),
            
            // MULS Rn, Rdm
            // Mask: 1111 1111 1100 0000 -> Pattern: 0100 0011 0100 0000
            new OpcodeRule(0xffc0, 0x4340, &ArithmeticOps.Muls),
            
            // MVNS Rd, Rm
            // Mask: 1111 1111 1100 0000 -> Pattern: 0100 0011 1100 0000
            new OpcodeRule(0xFFC0, 0x43C0, &BitOps.Mvns),
            
            // NOP
            // Mask: 1011 1111 0000 0000 -> Pattern: 1011 1111 0000 0000 (BF00)
            new OpcodeRule(0xBF00, 0xBF00, &SystemOps.Nop),
            
            // ORRS (Rd, Rm)
            // Mask: 1111 1111 0000 0000 -> Pattern: 0100 0011 0000 0000 (0x4300)
            new OpcodeRule(0xFF00, 0x4300, &ArithmeticOps.Orrs)
        ];
        
        for (var i = 0; i < 65536; i++)
        {
            var opcode = (ushort)i;
            foreach (ref readonly var rule in rules) {
                if ((opcode & rule.Mask) != rule.Pattern)
                    continue;
                _fastTablePtr[i] = rule.Handler;
                break;
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispatch(ushort opcode, CortexM0Plus cpu)
    {
        _fastTablePtr[opcode](opcode, cpu);
    }

    public nuint GetHandler(ushort opcode)
    {
        return (nuint)_fastTablePtr[opcode];
    }

    private static void HandleUndefined(ushort opcode, CortexM0Plus cpu)
    {
        throw new Exception($"Undefined Opcode: 0x{opcode:X4} PC={cpu.Registers.PC:X8}");
    }
    
    public void Dispose()
    {
        if (_pinnedHandle.IsAllocated) _pinnedHandle.Free();
    }
}