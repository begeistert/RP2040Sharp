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
        // 1. Llenar con Undefined
        _pinnedHandle = GCHandle.Alloc(_lookupTable, GCHandleType.Pinned);
        _fastTablePtr = (InstructionHandler*)_pinnedHandle.AddrOfPinnedObject();
        
        InstructionHandler undefinedPtr = &HandleUndefined;
        fixed (InstructionHandler* ptrToArr = _lookupTable)
        {
            new Span<nuint>(ptrToArr, _lookupTable.Length).Fill((nuint)undefinedPtr);
        }

        // 2. Registrar instrucciones (Aquí conectamos el decoder con la implementación)
        ReadOnlySpan<OpcodeRule> rules = [
            // --- ARITMÉTICA ESPECIAL ---
            // ADCS (Rd, Rm)
            // Mask: 1111 1111 1100 0000 (FFC0) -> Pattern: 0100 0001 0100 0000 (4140)
            new OpcodeRule (0xFFC0, 0x4140, &ArithmeticOps.Adcs),

            // --- ADD CON SP (STACK POINTER) ---
            // ADD (SP + imm7)
            // Mask: 1111 1111 1000 0000 (FF80) -> Pattern: 1011 0000 0000 0000 (B000)
            new OpcodeRule(0xFF80, 0xB000, &ArithmeticOps.AddSpImm7),

            // ADD (Rd = SP + imm8)
            // Mask: 1111 1000 0000 0000 (F800) -> Pattern: 1010 1000 0000 0000 (A800)
            new OpcodeRule(0xF800, 0xA800, &ArithmeticOps.AddSpImm8),
            
            // ADD (High Registers) - Encoding T2
            // Cubre: ADD Rd, Rm (donde alguno es > R7)
            new OpcodeRule(0xFF00, 0x4400, &ArithmeticOps.AddHighRegisters),

            // --- ARITMÉTICA COMÚN ---
            // ADDS (Rd, Rn, Rm) - Encoding T1 Register
            // Mask: 1111 1110 0000 0000 (FE00) -> Pattern: 0001 1000 0000 0000 (1800)
            // OJO: Esta máscara es similar a AddsImmediate3. El bit 10 es la clave.
            // AddsImm3: 0001 11... (Bit 10 es 1)
            // AddsReg:  0001 10... (Bit 10 es 0)
            new OpcodeRule(0xFE00, 0x1800, &ArithmeticOps.AddsRegister),

            // ADDS (Rd, Rn, imm3)
            new OpcodeRule(0xFE00, 0x1C00, &ArithmeticOps.AddsImmediate3),

            // ADDS (Rd, imm8)
            new OpcodeRule(0xF800, 0x3000, &ArithmeticOps.AddsImmediate8),
            // ADCS (Rd, Rm) - JS: opcode >> 6 === 0b0100000101
            // Mask: 1111 1111 1100 0000 (FFC0) -> Pattern: 0100 0001 0100 0000 (4140)
            // new OpcodeRule(0xFFC0, 0x4140, AluOps.Adcs), 

            // --- ADD CON SP (STACK POINTER) ---
            // ADD (SP + imm7) - JS: opcode >> 7 === 0b101100000
            // Mask: 0xFF80, Pattern: 0xB000
            // new OpcodeRule(0xFF80, 0xB000, AluOps.AddSpImm7),

            // ADD (Rd = SP + imm8) - JS: opcode >> 11 === 0b10101
            // Mask: 0xF800, Pattern: 0xA800
            // new OpcodeRule(0xF800, 0xA800, AluOps.AddSpImm8),

            // --- ARITMÉTICA COMÚN (ADDS/SUBS/MOVS) ---

            // MOVS (Rd, imm8) - JS: opcode >> 11 === 0b00100
            // Mask: 0xF800, Pattern: 0x2000
            new OpcodeRule(0xF800, 0x2000, &BitOps.MovsImmediate),

            // --- CONTROL DE FLUJO (BRANCH) ---
            // B (Conditional) - JS: opcode >> 12 === 0b1101
            // Mask: 1111 0000 0000 0000 (F000) -> Pattern: 1101 0000 0000 0000 (D000)
            // new OpcodeRule(0xF000, 0xD000, FlowOps.ConditionalBranch),

            // B (Unconditional) - JS: opcode >> 11 === 0b11100
            // Mask: 1111 1000 0000 0000 (F800) -> Pattern: 1110 0000 0000 0000 (E000)
            new OpcodeRule(0xF800, 0xE000, &FlowOps.Branch),
            
            // BL (Branch with Link - 32 bit) 
            // Aquí solo detectamos la PRIMERA mitad (Prefijo 11110...)
            // JS: opcode >> 11 === 0b11110
            // new OpcodeRule(0xF800, 0xF000, FlowOps.BranchLink)
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