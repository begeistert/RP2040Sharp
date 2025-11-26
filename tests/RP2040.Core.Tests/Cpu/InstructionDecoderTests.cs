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
	const int R6 = 6;
	const int R7 = 7;
	const int R8 = 8;
	const int R9 = 9;
	const int R10 = 10;
	const int R11 = 11;

	const int IP = 12;
	const int LR = 14;
	
	public static nuint AddressOf(InstructionHandler handler) => (nuint)handler;
	static readonly InstructionDecoder Decoder = new InstructionDecoder ();

    [Fact]
	public void Adcs ()
	{
		// Arrange
		var opcode = InstructionEmiter.Adcs (R4, R4);
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
		var opcode = InstructionEmiter.AddSpImm7 (0x10);
		var expectedPointer = AddressOf(&ArithmeticOps.AddSpImmediate7);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddSpImm8 ()
	{
		// Arrange
		var opcode = InstructionEmiter.AddSpImm8 (R1, 0x10);
		var expectedPointer = AddressOf(&ArithmeticOps.AddSpImmediate8);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddHighRegisters ()
	{
		// Arrange
		var opcode = InstructionEmiter.AddHighRegisters (R1, IP);
		var expectedPointer = AddressOf(&ArithmeticOps.AddHighRegisters);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddsImm3 ()
	{
		// Arrange
		var opcode = InstructionEmiter.AddsImm3 (R1, R2, 3);
		var expectedPointer = AddressOf (&ArithmeticOps.AddsImmediate3);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddsImm8 ()
	{
		// Arrange
		var opcode = InstructionEmiter.AddsImm8 (R1, 1);
		var expectedPointer = AddressOf (&ArithmeticOps.AddsImmediate8);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddsRegister ()
	{
		// Arrange
		var opcode = InstructionEmiter.AddsRegister (R1, R2, R7);
		var expectedPointer = AddressOf (&ArithmeticOps.AddsRegister);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Adr ()
	{
		// Arrange
		var opcode = InstructionEmiter.Adr (R4, 0x50);
		var expectedPointer = AddressOf (&ArithmeticOps.Adr);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Ands ()
	{
		// Arrange
		var opcode = InstructionEmiter.Ands (R5, R0);
		var expectedPointer = AddressOf (&BitOps.Ands);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AsrsImm5 ()
	{
		// Arrange
		var opcode = InstructionEmiter.AsrsImm5 (R3, R2, 31);
		var expectedPointer = AddressOf (&BitOps.AsrsImm5);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AsrsRegister ()
	{
		// Arrange
		var opcode = InstructionEmiter.AsrsRegister (R3, R4);
		var expectedPointer = AddressOf (&BitOps.AsrsRegister);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Bics ()
	{
		// Arrange
		var opcode = InstructionEmiter.Bics (R0, R3);
		var expectedPointer = AddressOf (&BitOps.Bics);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Bl ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Bl  (0x34) & 0xFFFF);
		var expectedPointer = AddressOf (&FlowOps.Bl);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Blx ()
	{
		// Arrange
		var opcode = InstructionEmiter.Blx  (R3);
		var expectedPointer = AddressOf (&FlowOps.Blx);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void BranchConditional ()
	{
		// Arrange
		var opcode = InstructionEmiter.BranchConditional  (1, 0x1f8);
		var expectedPointer = AddressOf (&FlowOps.BranchConditional);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void Branch ()
	{
		// Arrange
		var opcode = InstructionEmiter.Branch  (0xfec);
		var expectedPointer = AddressOf (&FlowOps.Branch);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void Bx ()
	{
		// Arrange
		var opcode = InstructionEmiter.Bx  (LR);
		var expectedPointer = AddressOf (&FlowOps.Bx);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void Cmn ()
	{
		// Arrange
		var opcode = InstructionEmiter.Cmn  (R7, R2);
		var expectedPointer = AddressOf (&ArithmeticOps.Cmn);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void CmpImm ()
	{
		// Arrange
		var opcode = InstructionEmiter.CmpImm  (R5, 66);
		var expectedPointer = AddressOf (&ArithmeticOps.CmpImmediate);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void CmpRegister ()
	{
		// Arrange
		var opcode = InstructionEmiter.CmpRegister  (R5, R0);
		var expectedPointer = AddressOf (&ArithmeticOps.CmpRegister);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void CmpHighRegister ()
	{
		// Arrange
		var opcode = InstructionEmiter.CmpHighRegister  (R11, R3);
		var expectedPointer = AddressOf (&ArithmeticOps.CmpHighRegister);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Dmb ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Dmb  () & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Barrier);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void Dsb ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Dsb  () & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Barrier);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void Eors ()
	{
		// Arrange
		var opcode = InstructionEmiter.Eors (R1, R3) ;
		var expectedPointer = AddressOf (&BitOps.Eors);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void Isb ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Isb  () & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Barrier);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
	
	[Fact]
	public void Mov ()
	{
		// Arrange
		var opcode = InstructionEmiter.Mov (R3, R8);
		var expectedPointer = AddressOf (&BitOps.Mov);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Muls ()
	{
		// Arrange
		var opcode = InstructionEmiter.Muls (R0, R2);
		var expectedPointer = AddressOf (&ArithmeticOps.Muls);
		
		// Act
		var handlerAddress = Decoder.GetHandler(opcode);
		
		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
}
