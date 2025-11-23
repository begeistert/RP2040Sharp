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
            new OpcodeRule(0xFFC0, 0x4000, &BitOps.Ands)
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