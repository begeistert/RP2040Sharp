using System.Runtime.CompilerServices;

namespace RP2040.Core.Cpu.Instructions;

public static class FlowOps
{
    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public static void Bl (ushort opcodeH1, CortexM0Plus cpu)
    {
        ref var pc = ref cpu.Registers.PC;
        var opcodeH2 = cpu.Bus.ReadHalfWord(pc);

        pc += 2;
        var nextPc = pc;
        
        var s = (opcodeH1 >> 10) & 1;
        var j1 = (opcodeH2 >> 13) & 1;
        var j2 = (opcodeH2 >> 11) & 1;
        
        var imm10 = opcodeH1 & 0x3FF;
        var imm11 = opcodeH2 & 0x7FF;

        var i1 = ~(j1 ^ s) & 1;
        var i2 = ~(j2 ^ s) & 1;

        var offset = (s << 24) | (i1 << 23) | (i2 << 22) | (imm10 << 12) | (imm11 << 1);

        offset = (offset << 7) >> 7;

        cpu.Registers.LR = nextPc | 1;
        pc = (uint)(nextPc + offset);

        cpu.Cycles += 2; // Takes total 3 cycles
        // BLTaken Action is Missing
    }

    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public static void Blx (ushort opcode, CortexM0Plus cpu)
    {
        var rm = (opcode >> 3) & 0xF;
        ref var pc = ref cpu.Registers.PC;
        
        cpu.Registers.LR = pc | 0x1;
        
        var targetAddress = cpu.Registers[rm];
        pc = targetAddress & 0xFFFFFFFE;

        cpu.Cycles ++;
        // BLTaken Action is Missing
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Branch(ushort opcode, CortexM0Plus cpu)
    {
        var offset = (int)(opcode << 21) >> 20;

        ref var pc = ref cpu.Registers.PC;
        
        pc += (uint)(offset + 2);
        
        cpu.Cycles ++;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Beq(ushort opcode, CortexM0Plus cpu) // Z == 1
    {
        if (cpu.Registers.Z) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bne(ushort opcode, CortexM0Plus cpu) // Z == 0
    {
        if (!cpu.Registers.Z) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bcs(ushort opcode, CortexM0Plus cpu) // C == 1
    {
        if (cpu.Registers.C) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bcc(ushort opcode, CortexM0Plus cpu) // C == 0
    {
        if (!cpu.Registers.C) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bmi(ushort opcode, CortexM0Plus cpu) // N == 1
    {
        if (cpu.Registers.N) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bpl(ushort opcode, CortexM0Plus cpu) // N == 0
    {
        if (!cpu.Registers.N) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bvs(ushort opcode, CortexM0Plus cpu) // V == 1
    {
        if (cpu.Registers.V) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bvc(ushort opcode, CortexM0Plus cpu) // V == 0
    {
        if (!cpu.Registers.V) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bhi(ushort opcode, CortexM0Plus cpu) // C==1 && Z==0
    {
        if (cpu.Registers.C && !cpu.Registers.Z) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bls(ushort opcode, CortexM0Plus cpu) // C==0 || Z==1
    {
        if (!cpu.Registers.C || cpu.Registers.Z) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bge(ushort opcode, CortexM0Plus cpu) // N == V
    {
        if (cpu.Registers.N == cpu.Registers.V) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Blt(ushort opcode, CortexM0Plus cpu) // N != V
    {
        if (cpu.Registers.N != cpu.Registers.V) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Bgt(ushort opcode, CortexM0Plus cpu) // Z==0 && N==V
    {
        if (!cpu.Registers.Z && (cpu.Registers.N == cpu.Registers.V)) TakeBranch(opcode, cpu);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ble(ushort opcode, CortexM0Plus cpu) // Z==1 || N!=V
    {
        if (cpu.Registers.Z || (cpu.Registers.N != cpu.Registers.V)) TakeBranch(opcode, cpu);
    }
    
    [MethodImpl (MethodImplOptions.AggressiveInlining)]
    public static void Bx (ushort opcode, CortexM0Plus cpu)
    {
        var rm = (opcode >> 3) & 0xf;
        var target = cpu.Registers[rm];
        if (target >= 0xFFFFFFF0 && cpu.Registers.IPSR != 0)
        {
            cpu.ExceptionReturn(target);
            return; 
        }
        cpu.Registers.PC = target & 0xFFFFFFFE;
        cpu.Cycles ++;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TakeBranch(ushort opcode, CortexM0Plus cpu)
    {
        var offset = (sbyte)(opcode & 0xFF) << 1;
    
        ref var pc = ref cpu.Registers.PC;
        pc += (uint)(offset + 2);
    
        cpu.Cycles ++;
    }
}