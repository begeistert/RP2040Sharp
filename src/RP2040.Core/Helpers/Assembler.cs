namespace RP2040.Core.Helpers;

public static class Assembler
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

	public static ushort AddHighRegisters (int rdn, int rm)
	{
		if (rdn > 0xf || rm > 0xf) throw new ArgumentException("Register index out of range (0-15)");
		return (ushort)(0x4400 | (rdn & 0x8) << 4 | (rm & 0xf) << 3 | rdn & 0x7);
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
