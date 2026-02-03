using System.Numerics;
using System.Runtime.CompilerServices;
using RP2040.Core.Memory;

namespace RP2040.Core.Cpu.Instructions;

public static unsafe class MemoryOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LdrImmediate(ushort opcode, CortexM0Plus cpu)
    {
        var rt = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var imm5 = (uint)((opcode >> 6) & 0x1F) << 2;

        cpu.Registers[rt] = ReadWordWithCycles(cpu, cpu.Registers[rn] + imm5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LdrLiteral(ushort opcode, CortexM0Plus cpu)
    {
        var rt = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF) << 2;
        var nextPc = cpu.Registers.PC + 2;
        var addr = (nextPc & 0xFFFFFFFC) + imm8;
        cpu.Registers[rt] = ReadWordWithCycles(cpu, addr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LdrRegister(ushort opcode, CortexM0Plus cpu)
    {
        var rt = opcode & 0x7;
        var rn = (opcode >> 3) & 0x7;
        var rm = (opcode >> 6) & 0x7;
        cpu.Registers[rt] = ReadWordWithCycles(cpu, cpu.Registers[rn] + cpu.Registers[rm]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LdrSpRelative(ushort opcode, CortexM0Plus cpu)
    {
        var rt = (opcode >> 8) & 0x7;
        var imm8 = (uint)(opcode & 0xFF) << 2;
        cpu.Registers[rt] = ReadWordWithCycles(cpu, cpu.Registers.SP + imm8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pop(ushort opcode, CortexM0Plus cpu)
    {
        var mask = (uint)(opcode & 0xFF);
        var regCount = (uint)BitOperations.PopCount(mask);

        var sp = cpu.Registers.SP;
        var finalSp = sp + (regCount * 4);

        if ((sp >> 28) == BusInterconnect.REGION_SRAM)
        {
            var rawPtr = cpu.Bus.PtrSram + (sp & BusInterconnect.MASK_SRAM);

            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                cpu.Registers[regIdx] = Unsafe.ReadUnaligned<uint>(rawPtr);

                rawPtr += 4;
                mask &= (mask - 1);
            }
        }
        else
        {
            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                cpu.Registers[regIdx] = cpu.Bus.ReadWord(sp);
                sp += 4;
                mask &= (mask - 1);
            }
        }

        cpu.Registers.SP = finalSp;
        cpu.Cycles += 1 + regCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PopPc(ushort opcode, CortexM0Plus cpu)
    {
        var mask = (uint)(opcode & 0xFF);
        var regCount = (uint)BitOperations.PopCount(mask);

        var sp = cpu.Registers.SP;
        var finalSp = sp + ((regCount + 1) * 4);

        if ((sp >> 28) == BusInterconnect.REGION_SRAM)
        {
            var rawPtr = cpu.Bus.PtrSram + (sp & BusInterconnect.MASK_SRAM);

            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                cpu.Registers[regIdx] = Unsafe.ReadUnaligned<uint>(rawPtr);
                rawPtr += 4;
                mask &= (mask - 1);
            }
            var newPc = Unsafe.ReadUnaligned<uint>(rawPtr);

            cpu.Registers.PC = newPc & 0xFFFFFFFE;
        }
        else
        {
            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                cpu.Registers[regIdx] = cpu.Bus.ReadWord(sp);
                sp += 4;
                mask &= (mask - 1);
            }
            var newPc = cpu.Bus.ReadWord(sp);
            cpu.Registers.PC = newPc & 0xFFFFFFFE;
        }

        cpu.Registers.SP = finalSp;
        cpu.Cycles += 4 + regCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Push(ushort opcode, CortexM0Plus cpu)
    {
        var mask = (uint)(opcode & 0xFF);
        var regCount = (uint)BitOperations.PopCount(mask);
        var totalBytes = regCount * 4;

        var oldSp = cpu.Registers.SP;
        var newSp = oldSp - totalBytes;

        if ((newSp >> 28) == BusInterconnect.REGION_SRAM)
        {
            var rawPtr = cpu.Bus.PtrSram + (newSp & BusInterconnect.MASK_SRAM);

            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);

                Unsafe.WriteUnaligned<uint>(rawPtr, cpu.Registers[regIdx]);

                rawPtr += 4;
                mask &= (mask - 1);
            }
        }
        else
        {
            var writePtr = newSp;
            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                var val = cpu.Registers[regIdx];
                cpu.Bus.WriteWord(writePtr, val);

                writePtr += 4;
                mask &= (mask - 1);
            }
        }

        cpu.Registers.SP = newSp;
        cpu.Cycles += regCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PushLr(ushort opcode, CortexM0Plus cpu)
    {
        var mask = (uint)(opcode & 0xFF);
        var regCount = (uint)BitOperations.PopCount(mask);
        var totalBytes = (regCount + 1) * 4; // +1 because of LR

        var oldSp = cpu.Registers.SP;
        var newSp = oldSp - totalBytes;

        if ((newSp >> 28) == BusInterconnect.REGION_SRAM)
        {
            var rawPtr = cpu.Bus.PtrSram + (newSp & BusInterconnect.MASK_SRAM);

            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                Unsafe.WriteUnaligned<uint>(rawPtr, cpu.Registers[regIdx]);
                rawPtr += 4;
                mask &= (mask - 1);
            }
            Unsafe.WriteUnaligned<uint>(rawPtr, cpu.Registers.LR);
        }
        else
        {
            var writePtr = newSp;
            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                cpu.Bus.WriteWord(writePtr, cpu.Registers[regIdx]);
                writePtr += 4;
                mask &= (mask - 1);
            }
            cpu.Bus.WriteWord(writePtr, cpu.Registers.LR);
        }

        cpu.Registers.SP = newSp;
        cpu.Cycles += regCount + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ldmia(ushort opcode, CortexM0Plus cpu)
    {
        var rn = (opcode >> 8) & 0x7;
        var mask = (uint)(opcode & 0xFF);

        var regCount = (uint)BitOperations.PopCount(mask);
        var baseAddr = cpu.Registers[rn];

        var isRnInList = (mask >> rn) & 1;
        var writeBackOffset = (regCount * 4) * (isRnInList ^ 1);

        if ((baseAddr >> 28) == BusInterconnect.REGION_SRAM)
        {
            var ptr = cpu.Bus.PtrSram + (baseAddr & BusInterconnect.MASK_SRAM);

            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                cpu.Registers[regIdx] = Unsafe.ReadUnaligned<uint>(ptr);

                ptr += 4;
                mask &= (mask - 1);
            }
        }
        else // SLOW PATH
        {
            var readPtr = baseAddr;
            while (mask != 0)
            {
                var regIdx = BitOperations.TrailingZeroCount(mask);
                cpu.Registers[regIdx] = cpu.Bus.ReadWord(readPtr);

                readPtr += 4;
                mask &= (mask - 1);
            }
        }

        cpu.Registers[rn] += writeBackOffset;
        cpu.Cycles += (int)regCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadWordWithCycles(CortexM0Plus cpu, uint address)
    {
        var region = address >> 28;

        switch (region)
        {
            case <= BusInterconnect.REGION_SRAM:
                cpu.Cycles += 1;
                break;
            case 0x4: // APB/AHB
            case 0x5:
                cpu.Cycles += 2;
                break;
            // SIO (Single-cycle IO)
            case 0xD:
                break;
            default:
                cpu.Cycles += 1; // Fallback
                break;
        }

        return cpu.Bus.ReadWord(address);
    }
}
