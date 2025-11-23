using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Cpu.Instructions;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;

using unsafe InstructionHandler = delegate* managed<ushort, RP2040.Core.Cpu.CortexM0Plus, void>;

namespace RP2040.tests.Cpu;

public unsafe class InstructionDecoderTests
{
	const int R0 = 0;
	const int R1 = 1;
	const int R2 = 2;
	const int R3 = 3;
	const int R4 = 4;
	const int R5 = 5;

	const int IP = 12;
	
	static nuint AddressOf(InstructionHandler handler) => (nuint)handler;
	static readonly InstructionDecoder Decoder = new InstructionDecoder ();

    [Fact]
	public void Adcs ()
	{
		// Arrange
		var opcode = Assembler.Adcs (R4, R4);
		var expectedPointer = AddressOf(&ArithmeticOps.Adcs);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddSpImm7 ()
	{
		// Arrange
		var opcode = Assembler.AddSpImm7 (0x10);
		var expectedPointer = AddressOf(&ArithmeticOps.AddSpImm7);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddSpImm8 ()
	{
		// Arrange
		var opcode = Assembler.AddSpImm8 (R1, 0x10);
		var expectedPointer = AddressOf(&ArithmeticOps.AddSpImm8);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddHighRegisters ()
	{
		// Arrange
		var opcode = Assembler.AddHighRegisters (R1, IP);
		var expectedPointer = AddressOf(&ArithmeticOps.AddHighRegisters);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddsImmediate3 ()
	{
		// Arrange
		var opcode = Assembler.AddsImmediate3 (R1, R2, 3);
		var expectedPointer = AddressOf (&ArithmeticOps.AddsImmediate3);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
}
