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

	// MOVS Rd, #imm8
	// Encoding: 0010 0ddd iiii iiii (0x2000 base)
	public static ushort Movs(int rd, uint imm8)
	{
		if (imm8 > 255) throw new ArgumentException("Immediate too large for MOVS");
		return (ushort)(0x2000 | (rd << 8) | imm8);
	}

	// B <offset> (Calculado automáticamente)
	public static ushort B(int currentPc, int targetAddress)
	{
		// Cálculo inverso del salto relativo
		int offset = targetAddress - (currentPc + 4); 
		offset /= 2; // Instrucciones alineadas
        
		// Máscara de 11 bits
		int imm11 = offset & 0x7FF; 
		return (ushort)(0xE000 | imm11);
	}
    
	// Agrega aquí ADDS, SUB, etc. a medida que los necesites
}
