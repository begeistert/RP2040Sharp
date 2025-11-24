namespace RP2040.Core.Helpers;

public static class InstructionEmiter
{
	// ADCS Rd, Rm
	// Encoding: 0100 0001 01mm mddd (0x4140 base)
	public static ushort Adcs(int rd, int rm)
	{
		// Validaciones de "Eminencia": Fail fast si el test está mal escrito
		if (rd > 7 || rm > 7) throw new ArgumentException("Register index out of range (0-7)");
        
		return (ushort)(0x4140 | (rm << 3) | rd);
	}

	public static ushort AddSpImm7 (uint imm7)
	{
		imm7 >>= 2;
		if (imm7 > 0x7F) throw new ArgumentException("Immediate too large for ADD SP, immediate");
		return (ushort)(0xB000 | imm7);
	}
	
	public static ushort AddSpImm8 (int rd, uint imm8)
	{
		imm8 >>= 2;
		if (imm8 > 0xFF) throw new ArgumentException("Immediate too large for ADD SP, immediate");
		if (rd > 7) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)((uint)(0xA800 | rd << 8) | imm8);
	}

	public static ushort AddHighRegisters (uint rdn, uint rm)
	{
		if (rdn > 0xf || rm > 0xf) throw new ArgumentException("Register index out of range (0-15)");
		return (ushort)(0x4400 | (rdn & 0x8) << 4 | (rm & 0xf) << 3 | rdn & 0x7);
	}

	public static ushort AddsImm3 (uint rd, uint rn, uint imm3)
	{
		if (rd > 7 || rn > 7) throw new ArgumentException("Register index out of range (0-7)");
		if (imm3 > 7) throw new ArgumentException("Immediate too large for ADDS");
		return (ushort)(0x1C00 | (imm3 & 0x7) << 6 | ((rn & 0x07) << 3) | (rd & 0x07));
	}

	public static ushort AddsImm8 (uint rdn, uint imm8)
	{
		if (rdn > 7) throw new ArgumentException("Register index out of range (0-7)");
		if (imm8 > 255) throw new ArgumentException("Immediate too large for ADDS");
		return (ushort)(0x3000 | (rdn & 0x07) << 8 | (imm8 & 0xFF));
	}

	public static ushort AddsRegister (uint rd, uint rn, uint rm)
	{
		if (rd > 7 || rn > 7 || rm > 7) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)(0x1800 | ((rm & 0x07) << 6) | (rn & 0x07) << 3 | (rd & 0x07));
	}

	public static ushort Adr (uint rd, uint imm8)
	{
		if (rd > 7) throw new ArgumentException("Register index out of range (0-7)");
		if (imm8 >> 2 > 0xFF) throw new ArgumentException("Immediate too large");
		return (ushort)(0xA000 | (rd & 7) << 8 | imm8 >> 2 & 0xFF);
	}

	public static ushort Ands (uint rn, uint rm)
	{
		if (rn > 7 || rm > 7) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)(0x4000 | (rm & 7) << 3 | rn & 7);
	}

	public static ushort AsrsImm5 (uint rd, uint rm, uint imm5)
	{
		if (rd > 7 || rm > 7) throw new ArgumentException("Register index out of range (0-7)");
		if (imm5 > 31) throw new ArgumentException("Immediate too large for ASRS");
		return (ushort)(0x1000 | (imm5 & 0x1F) << 6 | ((rm & 7) << 3) | (rd & 7));
	}

	public static ushort AsrsRegister (uint rdn, uint rm)
	{
		if (rdn > 7 || rm > 7) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)(0x4100 | ((rm & 7) << 3) | rdn);
	}

	public static ushort Bics (uint rdn, uint rm)
	{
		if (rdn > 7 || rm > 7) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)(0x4380 | ((rm & 7) << 3) | rdn);
	}

	public static uint Bl (int offset)
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

	public static ushort Blx (int rm)
	{
		if (rm > 7) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)(0x4780 | rm << 3);
	}
	
	public static ushort BranchConditional(uint cond, uint offset)
	{
		if (cond > 15) throw new ArgumentException("Condition code out of range (0-15)");
		if (offset > 0x3FF) throw new ArgumentException("Offset out of range for Conditional Branch (-256 to +254)");
		if (offset % 2 != 0) throw new ArgumentException("Offset must be aligned to 2 bytes");
		return (ushort)(0xD000 | (cond & 0xF) << 8 | (uint)(offset >> 1 & 0x1FF));
	}
	
	public static ushort Branch(uint offset)
	{
		if (offset > 0xFFF) throw new ArgumentException("Offset out of range for Unconditional Branch (-2048 to +2046)");
		if (offset % 2 != 0) throw new ArgumentException("Offset must be aligned to 2 bytes");
		return (ushort)(0xE000 | ((offset >> 1) & 0x7FF));
	}

	public static ushort Bx (uint rm)
	{
		if (rm > 15) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)(0x4700 | rm << 3);
	}

	public static ushort Cmn (uint rn, uint rm)
	{
		if (rn > 7 || rm > 7) throw new ArgumentException("Register index out of range (0-7)");
		return (ushort)(0x42c0 | ((rn & 7) << 3) | rm);
	}
}
