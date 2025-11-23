using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Cpu.Instructions;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
namespace RP2040.tests.Cpu;

public class InstructionDecoderTests
{
	const int R0 = 0;
	const int R1 = 1;
	const int R2 = 2;
	const int R3 = 3;
	const int R4 = 4;
	const int R5 = 5;

	const int IP = 12;

    [Fact]
	public void Adcs ()
	{
		// Arrange
		var decoder = new InstructionDecoder ();
		var opcode = Assembler.Adcs (R4, R4);
		
		// Act
		var handler = decoder.GetHandler(opcode);
		
		// Assert
		handler.Should ().NotBeNull ();
		Assert.Equal (ArithmeticOps.Adcs, handler);
	}

	[Fact]
	public void AddSpImm7 ()
	{
		// Arrange
		var decoder = new InstructionDecoder ();
		var opcode = Assembler.AddSpImm7 (0x10);
		
		// Act
		var handler = decoder.GetHandler(opcode);
		
		// Assert
		handler.Should ().NotBeNull ();
		Assert.Equal (ArithmeticOps.AddSpImm7, handler);
	}

	[Fact]
	public void AddSpImm8 ()
	{
		// Arrange
		var decoder = new InstructionDecoder ();
		var opcode = Assembler.AddSpImm8 (R1, 0x10);
		
		// Act
		var handler = decoder.GetHandler(opcode);
		
		// Assert
		handler.Should ().NotBeNull ();
		Assert.Equal (ArithmeticOps.AddSpImm8, handler);
	}

	[Fact]
	public void AddHighRegisters ()
	{
		// Arrange
		var decoder = new InstructionDecoder ();
		var opcode = Assembler.AddHighRegisters (R1, IP);
		
		// Act
		var handler = decoder.GetHandler(opcode);
		
		// Assert
		handler.Should ().NotBeNull ();
		Assert.Equal (ArithmeticOps.AddHighRegisters, handler);
	}
}
