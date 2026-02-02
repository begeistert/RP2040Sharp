using System.Runtime.CompilerServices;

namespace RP2040.Core.Cpu.Instructions;

public static class SystemOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Barrier(ushort opcodeH1, CortexM0Plus cpu)
    {
        cpu.Registers.PC += 2;
        cpu.Cycles += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Nop(ushort opcodeH1, CortexM0Plus cpu)
    {
        // Do nothing
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Mrs(ushort opcodeH1, CortexM0Plus cpu)
    {
        ref var pc = ref cpu.Registers.PC;
        var opcodeH2 = cpu.Bus.ReadHalfWord(pc);
        pc += 2;

        var sysm = opcodeH2 & 0xFF;
        var rd = (opcodeH2 >> 8) & 0xF;

        uint result = 0;

        if ((sysm & 0xF8) == 0)
        {
            var fullPsr = cpu.Registers.GetxPsr();

            uint mask = 0;
            if ((sysm & 1) != 0)
                mask |= 0x1FF;
            if ((sysm & 4) == 0)
                mask |= 0xF0000000;

            result = fullPsr & mask;
        }
        else
        {
            var isPrivileged = (cpu.Registers.IPSR != 0) || ((cpu.Registers.CONTROL & 1) == 0);
            switch (sysm)
            {
                case 8: // MSP
                    var usePsp = (cpu.Registers.IPSR == 0) && ((cpu.Registers.CONTROL & 2) != 0);
                    result = usePsp ? cpu.Registers.MSP_Storage : cpu.Registers.SP;
                    break;

                case 9: // PSP
                    var usePsp2 = (cpu.Registers.IPSR == 0) && ((cpu.Registers.CONTROL & 2) != 0);
                    result = usePsp2 ? cpu.Registers.SP : cpu.Registers.PSP_Storage;
                    break;

                case 16: // PRIMASK
                    result = cpu.Registers.PRIMASK & 1;
                    break;

                case 20: // CONTROL
                    result = cpu.Registers.CONTROL & 3;
                    isPrivileged = true;
                    break;

                default:
                    result = 0;
                    break;
            }
            if (!isPrivileged)
                result = 0;
        }

        cpu.Registers[rd] = result;
        cpu.Cycles += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Msr(ushort opcodeH1, CortexM0Plus cpu)
    {
        ref var pc = ref cpu.Registers.PC;
        var opcodeH2 = cpu.Bus.ReadHalfWord(pc);
        pc += 2;

        var rn = opcodeH1 & 0xF;
        var sysm = opcodeH2 & 0xFF;
        var value = cpu.Registers[rn];

        if ((sysm & 0xF8) == 0)
        {
            if ((sysm & 4) == 0)
            {
                cpu.Registers.N = (int)value < 0; // Bit 31
                cpu.Registers.Z = (value & 0x40000000) != 0;
                cpu.Registers.C = (value & 0x20000000) != 0;
                cpu.Registers.V = (value & 0x10000000) != 0;
            }
        }
        else
        {
            var isPrivileged = (cpu.Registers.IPSR != 0) || ((cpu.Registers.CONTROL & 1) == 0);

            if (isPrivileged)
            {
                switch (sysm)
                {
                    case 8: // MSP
                        var alignedMsp = value & 0xFFFFFFFC;

                        var isMspActive =
                            (cpu.Registers.IPSR != 0) || ((cpu.Registers.CONTROL & 2) == 0);

                        if (isMspActive)
                            cpu.Registers.SP = alignedMsp;
                        else
                            cpu.Registers.MSP_Storage = alignedMsp;
                        break;

                    case 9: // PSP
                        var alignedPsp = value & 0xFFFFFFFC;

                        var isPspActive =
                            (cpu.Registers.IPSR == 0) && ((cpu.Registers.CONTROL & 2) != 0);

                        if (isPspActive)
                            cpu.Registers.SP = alignedPsp;
                        else
                            cpu.Registers.PSP_Storage = alignedPsp;
                        break;

                    case 16: // PRIMASK
                        cpu.Registers.PRIMASK = value & 1;
                        break;

                    case 20: // CONTROL
                        var oldControl = cpu.Registers.CONTROL;
                        var newNpriv = value & 1;
                        var newSpsel = (cpu.Registers.IPSR == 0) ? (value & 2) : (oldControl & 2);

                        var newControl = newNpriv | newSpsel;

                        if (((oldControl ^ newControl) & 2) != 0)
                        {
                            cpu.Registers.CONTROL = newControl;
                            cpu.UpdateStackPointerSource();
                        }
                        else
                        {
                            cpu.Registers.CONTROL = newControl;
                        }
                        break;
                }
            }
        }
        cpu.Cycles += 2;
    }
}
