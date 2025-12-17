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
	const int SP = 13;
	const int LR = 14;
	const int PC = 15;

	public static nuint AddressOf (InstructionHandler handler) => (nuint)handler;
	static readonly InstructionDecoder Decoder = InstructionDecoder.Instance;

	[Fact]
	public void Adcs ()
	{
		// Arrange
		var opcode = InstructionEmiter.Adcs (R4, R4);
		var expectedPointer = AddressOf (&ArithmeticOps.Adcs);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddSpImm7 ()
	{
		// Arrange
		var opcode = InstructionEmiter.AddSpImm7 (0x10);
		var expectedPointer = AddressOf (&ArithmeticOps.AddSpImmediate7);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void AddSpImm8 ()
	{
		// Arrange
		var opcode = InstructionEmiter.AddSpImm8 (R1, 0x10);
		var expectedPointer = AddressOf (&ArithmeticOps.AddSpImmediate8);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Theory]
	[InlineData (R1, "Reg")] // 1
	[InlineData (SP, "Sp")] // 13
	[InlineData (PC, "Pc")] // 15
	public void AddHighRegisters (uint targetReg, string expectedType)
	{
		// Arrange
		var opcode = InstructionEmiter.AddHighRegisters (targetReg, R2);

		nuint expectedPointer = expectedType switch {
			"Reg" => AddressOf (&ArithmeticOps.AddHighToReg),
			"Sp" => AddressOf (&ArithmeticOps.AddHighToSp),
			"Pc" => AddressOf (&ArithmeticOps.AddHighToPc),
			_ => 0
		};

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Bl ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Bl (0x34) & 0xFFFF);
		var expectedPointer = AddressOf (&FlowOps.Bl);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Blx ()
	{
		// Arrange
		var opcode = InstructionEmiter.Blx (R3);
		var expectedPointer = AddressOf (&FlowOps.Blx);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Theory]
	[InlineData (0)] // EQ
	[InlineData (1)] // NE
	[InlineData (2)] // CS
	[InlineData (3)] // CC
	[InlineData (4)] // MI
	[InlineData (5)] // PL
	[InlineData (6)] // VS
	[InlineData (7)] // VC
	[InlineData (8)] // HI
	[InlineData (9)] // LS
	[InlineData (10)] // GE
	[InlineData (11)] // LT
	[InlineData (12)] // GT
	[InlineData (13)] // LE
	public void BranchConditional (uint cond)
	{
		// Arrange
		InstructionHandler expectedHandler = cond switch {
			0x0 => &FlowOps.Beq,
			0x1 => &FlowOps.Bne,
			0x2 => &FlowOps.Bcs,
			0x3 => &FlowOps.Bcc,
			0x4 => &FlowOps.Bmi,
			0x5 => &FlowOps.Bpl,
			0x6 => &FlowOps.Bvs,
			0x7 => &FlowOps.Bvc,
			0x8 => &FlowOps.Bhi,
			0x9 => &FlowOps.Bls,
			0xA => &FlowOps.Bge,
			0xB => &FlowOps.Blt,
			0xC => &FlowOps.Bgt,
			0xD => &FlowOps.Ble,
			_ => throw new ArgumentException ("Unexpected condition")
		};
		var opcode = InstructionEmiter.BranchConditional (cond, 0);

		// Act
		var actualAddress = Decoder.GetHandler (opcode);

		// Assert
		actualAddress.Should ().Be ((nuint)expectedHandler);
	}

	[Fact]
	public void Branch ()
	{
		// Arrange
		var opcode = InstructionEmiter.Branch (0xfec);
		var expectedPointer = AddressOf (&FlowOps.Branch);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Bx ()
	{
		// Arrange
		var opcode = InstructionEmiter.Bx (LR);
		var expectedPointer = AddressOf (&FlowOps.Bx);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Cmn ()
	{
		// Arrange
		var opcode = InstructionEmiter.Cmn (R7, R2);
		var expectedPointer = AddressOf (&ArithmeticOps.Cmn);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void CmpImm ()
	{
		// Arrange
		var opcode = InstructionEmiter.CmpImm (R5, 66);
		var expectedPointer = AddressOf (&ArithmeticOps.CmpImmediate);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void CmpRegister ()
	{
		// Arrange
		var opcode = InstructionEmiter.CmpRegister (R5, R0);
		var expectedPointer = AddressOf (&ArithmeticOps.CmpRegister);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void CmpHighRegister ()
	{
		// Arrange
		var opcode = InstructionEmiter.CmpHighRegister (R11, R3);
		var expectedPointer = AddressOf (&ArithmeticOps.CmpHighRegister);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Dmb ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Dmb () & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Barrier);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Dsb ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Dsb () & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Barrier);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Eors ()
	{
		// Arrange
		var opcode = InstructionEmiter.Eors (R1, R3);
		var expectedPointer = AddressOf (&BitOps.Eors);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Isb ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Isb () & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Barrier);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Theory]
	[InlineData (R3, "Reg")] // Registro normal
	[InlineData (PC, "Pc")] // Salto
	[InlineData (SP, "Sp")] // Stack
	public void Mov_Routing (uint targetReg, string expectedType)
	{
		// Arrange
		var opcode = InstructionEmiter.Mov (targetReg, R8);
		nuint expectedPointer = expectedType switch {
			"Reg" => AddressOf (&BitOps.MovRegister),
			"Pc" => AddressOf (&BitOps.MovToPc),
			"Sp" => AddressOf (&BitOps.MovToSp),
			_ => 0
		};

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

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
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Mvns ()
	{
		// Arrange
		var opcode = InstructionEmiter.Mvns (R0, R2);
		var expectedPointer = AddressOf (&BitOps.Mvns);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Nop ()
	{
		// Arrange
		var opcode = InstructionEmiter.Nop ();
		var expectedPointer = AddressOf (&SystemOps.Nop);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Orrs ()
	{
		// Arrange
		var opcode = InstructionEmiter.Orrs (R5, R0);
		var expectedPointer = AddressOf (&ArithmeticOps.Orrs);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Theory]
	[InlineData (false)]
	[InlineData (true)]
	public void Pop (bool pc)
	{
		// Arrange
		var opcode = InstructionEmiter.Pop (pc, (1 << R4) | (1 << R5) | (1 << R6));
		var expectedPointer = pc ? AddressOf (&MemoryOps.PopPc) : AddressOf (&MemoryOps.Pop);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Theory]
	[InlineData (false)]
	[InlineData (true)]
	public void Push (bool lr)
	{
		// Arrange
		var opcode = InstructionEmiter.Push (lr, (1 << R4) | (1 << R5) | (1 << R6));
		var expectedPointer = lr ? AddressOf (&MemoryOps.PushLr) : AddressOf (&MemoryOps.Push);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Mrs ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Mrs (R0, 5) & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Mrs);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}


	[Fact]
	public void Msr ()
	{
		// Arrange
		var opcode = (ushort)(InstructionEmiter.Msr (8, R0) & 0xFFFF);
		var expectedPointer = AddressOf (&SystemOps.Msr);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void Ldmia ()
	{
		// Arrange
		var opcode = InstructionEmiter.Ldmia (R0, (1 << R1) | (1 << R2));
		var expectedPointer = AddressOf (&MemoryOps.Ldmia);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void LslsImm ()
	{
		// Arrange
		var opcode = InstructionEmiter.LslsImm5 (R5, R5, 18);
		var expectedPointer = AddressOf (&BitOps.LslsImm5);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void LslsImmZero ()
	{
		// Arrange
		var opcode = InstructionEmiter.LslsImm5 (R5, R5, 0);
		var expectedPointer = AddressOf (&BitOps.LslsZero);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}

	[Fact]
	public void LslsRegister ()
	{
		// Arrange
		var opcode = InstructionEmiter.LslsRegister (R5, R0);
		var expectedPointer = AddressOf (&BitOps.LslsRegister);

		// Act
		var handlerAddress = Decoder.GetHandler (opcode);

		// Assert
		handlerAddress.Should ().Be (expectedPointer);
	}
}
