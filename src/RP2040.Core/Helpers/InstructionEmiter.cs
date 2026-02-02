using System.Diagnostics.CodeAnalysis;

namespace RP2040.Core.Helpers;

[ExcludeFromCodeCoverage]
public static class InstructionEmiter
{
    const string LowRegisterIndexOutOfRange = "Register index out of range (0-7)";
    const string HighRegisterIndexOutOfRange = "Register index out of range (0-15)";

    public static ushort Adcs(int rd, int rm)
    {
        if (rd > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4140 | (rm << 3) | rd);
    }

    public static ushort AddSpImm7(uint imm7)
    {
        imm7 >>= 2;
        if (imm7 > 0x7F)
            throw new ArgumentException("Immediate too large for ADD SP, immediate");
        return (ushort)(0xB000 | imm7);
    }

    public static ushort AddSpImm8(int rd, uint imm8)
    {
        imm8 >>= 2;
        if (imm8 > 0xFF)
            throw new ArgumentException("Immediate too large for ADD SP, immediate");
        if (rd > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)((uint)(0xA800 | rd << 8) | imm8);
    }

    public static ushort AddHighRegisters(uint rdn, uint rm)
    {
        if (rdn > 0xf || rm > 0xf)
            throw new ArgumentException("Register index out of range (0-15)");
        return (ushort)(0x4400 | (rdn & 0x8) << 4 | (rm & 0xf) << 3 | rdn & 0x7);
    }

    public static ushort AddsImm3(uint rd, uint rn, uint imm3)
    {
        if (rd > 7 || rn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm3 > 7)
            throw new ArgumentException("Immediate too large for ADDS");
        return (ushort)(0x1C00 | (imm3 & 0x7) << 6 | ((rn & 0x07) << 3) | (rd & 0x07));
    }

    public static ushort AddsImm8(uint rdn, uint imm8)
    {
        if (rdn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm8 > 255)
            throw new ArgumentException("Immediate too large for ADDS");
        return (ushort)(0x3000 | (rdn & 0x07) << 8 | (imm8 & 0xFF));
    }

    public static ushort AddsRegister(uint rd, uint rn, uint rm)
    {
        if (rd > 7 || rn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x1800 | ((rm & 0x07) << 6) | (rn & 0x07) << 3 | (rd & 0x07));
    }

    public static ushort Adr(uint rd, uint imm8)
    {
        if (rd > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm8 >> 2 > 0xFF)
            throw new ArgumentException("Immediate too large");
        return (ushort)(0xA000 | (rd & 7) << 8 | imm8 >> 2 & 0xFF);
    }

    public static ushort Ands(uint rn, uint rm)
    {
        if (rn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4000 | (rm & 7) << 3 | rn & 7);
    }

    public static ushort AsrsImm5(uint rd, uint rm, uint imm5)
    {
        if (rd > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm5 > 31)
            throw new ArgumentException("Immediate too large for ASRS");
        return (ushort)(0x1000 | (imm5 & 0x1F) << 6 | ((rm & 7) << 3) | (rd & 7));
    }

    public static ushort AsrsRegister(uint rdn, uint rm)
    {
        if (rdn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4100 | ((rm & 7) << 3) | rdn);
    }

    public static ushort Bics(uint rdn, uint rm)
    {
        if (rdn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4380 | ((rm & 7) << 3) | rdn);
    }

    public static uint Bl(int offset)
    {
        if (offset < -16777216 || offset > 16777214)
            throw new ArgumentException("Offset out of range for BL (+/- 16MB)");
        var s = (uint)((offset >> 24) & 1);
        var i1 = (uint)((offset >> 23) & 1);
        var i2 = (uint)((offset >> 22) & 1);
        var imm10 = (uint)((offset >> 12) & 0x3FF);
        var imm11 = (uint)((offset >> 1) & 0x7FF);
        var j1 = (~i1 ^ s) & 1;
        var j2 = (~i2 ^ s) & 1;
        var h1 = 0xF000 | (s << 10) | imm10;
        var h2 = 0xD000 | (j1 << 13) | (j2 << 11) | imm11;
        return (h2 << 16) | h1;
    }

    public static ushort Blx(int rm)
    {
        if (rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4780 | rm << 3);
    }

    public static ushort BranchConditional(uint cond, uint offset)
    {
        if (cond > 15)
            throw new ArgumentException("Condition code out of range (0-15)");
        if (offset > 0x3FF)
            throw new ArgumentException(
                "Offset out of range for Conditional Branch (-256 to +254)"
            );
        if (offset % 2 != 0)
            throw new ArgumentException("Offset must be aligned to 2 bytes");
        return (ushort)(0xD000 | (cond & 0xF) << 8 | offset >> 1 & 0x1FF);
    }

    public static ushort Branch(uint offset)
    {
        if (offset > 0xFFF)
            throw new ArgumentException(
                "Offset out of range for Unconditional Branch (-2048 to +2046)"
            );
        if (offset % 2 != 0)
            throw new ArgumentException("Offset must be aligned to 2 bytes");
        return (ushort)(0xE000 | ((offset >> 1) & 0x7FF));
    }

    public static ushort Bx(uint rm)
    {
        if (rm > 15)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4700 | rm << 3);
    }

    public static ushort Cmn(uint rn, uint rm)
    {
        if (rn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x42c0 | ((rn & 7) << 3) | rm);
    }

    public static ushort CmpImm(uint rn, uint imm8)
    {
        if (rn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm8 > 255)
            throw new ArgumentException("Immediate too large for CMP");
        return (ushort)(0x2800 | (rn & 7) << 3 | imm8 & 0xFF);
    }

    public static ushort CmpRegister(uint rn, uint rm)
    {
        if (rn > 7 || rm > 7)
            throw new ArgumentException("CMP T1 only supports Low Registers (0-7)");
        return (ushort)(0x4280 | ((rm & 0x7) << 3) | (rn & 0x7));
    }

    public static ushort CmpHighRegister(uint rn, uint rm)
    {
        if (rn > 15 || rm > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        var n = (rn >> 3) & 1;
        return (ushort)(0x4500 | (n << 7) | ((rm & 0xF) << 3) | (rn & 0x7));
    }

    public const uint Dmb = 0x8f4ff3bfu;

    public const uint Dsb = 0x8f4ff3bf;

    public static ushort Eors(uint rdn, uint rm)
    {
        if (rdn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4040 | ((rm & 7) << 3) | rdn & 0x7);
    }

    public const uint Isb = 0x8f6ff3bf;

    public static ushort Mov(uint rd, uint rm)
    {
        if (rd > 15 || rm > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        return (ushort)(0x4600 | ((rd & 8) << 4) | ((rm & 0xF) << 3) | (rd & 7));
    }

    public static ushort Movs(uint rd, uint imm8)
    {
        if (rd > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        if (imm8 > 255)
            throw new ArgumentException("Immediate too large for MOVS");
        return (ushort)(0x2000 | (rd & 7) << 8 | imm8 & 0xFF);
    }

    public static ushort Muls(uint rn, uint rdm)
    {
        if (rn > 7 || rdm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4340 | ((rn & 7) << 3) | (rdm & 7));
    }

    public static ushort Mvns(uint rd, uint rm)
    {
        if (rd > 15 || rm > 15)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x43c0 | ((rm & 7) << 3) | (rd & 7));
    }

    public const ushort Nop = 0xBF00;

    public static ushort Orrs(uint rn, uint rm)
    {
        if (rn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4300 | ((rm & 7) << 3) | rn & 7);
    }

    public static ushort Pop(bool p, uint registerList)
    {
        if (registerList > 255)
            throw new ArgumentException("Register list too large (0-15)");
        return (ushort)(0xBC00 | (p ? 0x100u : 0u) | registerList);
    }

    public static ushort Push(bool m, uint registerList)
    {
        if (registerList > 255)
            throw new ArgumentException("Register list too large (0-15)");
        return (ushort)(0xB400 | (m ? 0x100u : 0u) | registerList);
    }

    public static ushort Rev(uint rd, uint rn)
    {
        if (rd > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        if (rn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0xBA00 | ((rn & 7) << 3) | (rd & 7));
    }

    public static uint Mrs(uint rd, uint specialRegister)
    {
        if (rd > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        if (specialRegister > 255)
            throw new ArgumentException("Special register index out of range (0-15)");
        return 0x80000000 | (rd & 0xf) << 24 | (specialRegister & 0xFF) << 16 | 0xf3ef;
    }

    public static uint Msr(uint specialRegister, uint rd)
    {
        if (rd > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        if (specialRegister > 255)
            throw new ArgumentException("Special register index out of range (0-15)");
        return 0x88000000 | (specialRegister & 0xFF) << 16 | 0xf380 | rd & 0xf;
    }

    public static ushort Ldmia(uint rn, uint registerList)
    {
        if (registerList > 255)
            throw new ArgumentException("Register list too large (0-15)");
        if (rn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0xC800 | (rn & 0x7) << 8 | registerList & 0xFF);
    }

    public static ushort LslsImm5(uint rd, uint rm, uint imm5)
    {
        if (rd > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm5 > 31)
            throw new ArgumentException("Immediate too large for LSLS");
        return (ushort)((imm5 & 0x1F) << 6 | ((rm & 7) << 3) | (rd & 7));
    }

    public static ushort LslsRegister(uint rdn, uint rm)
    {
        if (rdn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x4080 | ((rm & 7) << 3) | rdn);
    }

    public static ushort Revsh(uint rd, uint rm)
    {
        if (rd > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0xBAC0 | ((rm & 7) << 3) | (rd & 7));
    }

    public static ushort Rev16(uint rd, uint rn)
    {
        if (rd > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        if (rn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0xBA40 | ((rn & 0x7) << 3) | (rd & 0x7));
    }

    public static ushort Rsbs(uint rd, uint rn)
    {
        if (rd > 15 || rn > 15)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        return (ushort)(0x4240 | (rn & 0x7) << 3 | (rd & 0x7));
    }

    public static ushort Sbcs(uint rn, uint rm)
    {
        if (rm > 7 || rn > 7)
            throw new ArgumentException(HighRegisterIndexOutOfRange);
        return (ushort)(0x4180 | ((rm & 0x7) << 3) | (rn & 0x7));
    }

    public static ushort SubSp(uint imm)
    {
        if (imm > 508)
            throw new ArgumentOutOfRangeException(
                nameof(imm),
                "Immediate value must be between 0 and 508."
            );
        return (ushort)(0xB080 | ((imm >> 2) & 0x7f));
    }

    public static ushort SubsImm3(uint rd, uint rn, uint imm3)
    {
        if (rd > 7 || rn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm3 > 7)
            throw new ArgumentException("Immediate too large for SUBS");

        return (ushort)(0x1E00 | ((imm3 & 0x7) << 6) | ((rn & 7) << 3) | (rd & 7));
    }

    public static ushort SubsImm8(uint rdn, uint imm8)
    {
        if (rdn > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        if (imm8 > 255)
            throw new ArgumentException("Immediate too large for SUBS");
        return (ushort)(0x3800 | ((rdn & 7) << 8) | (imm8 & 0xff));
    }

    public static ushort SubsReg(uint rd, uint rn, uint rm)
    {
        if (rd > 7 || rn > 7 || rm > 7)
            throw new ArgumentException(LowRegisterIndexOutOfRange);
        return (ushort)(0x1A00 | ((rm & 0x7) << 6) | ((rn & 7) << 3) | (rd & 7));
    }
}
