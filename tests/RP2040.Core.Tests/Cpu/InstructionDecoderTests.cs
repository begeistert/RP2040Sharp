using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Cpu.Instructions;
using RP2040.Core.Helpers;
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

	static ulong AddressOf (InstructionHandler handler) => (ulong)handler;
	static readonly InstructionDecoder Decoder = InstructionDecoder.Instance;

	[Theory]
	[MemberData (nameof (GetInstructionTestCases))]
	public void ShouldMapCorrectly (string name, ushort opcode, ulong expectedHandlerAddress)
	{
		// Act
		var actualHandler = (ulong)Decoder.GetHandler (opcode);

		// Assert
		actualHandler.Should ().Be (expectedHandlerAddress, $"The instruction '{name}' should decode correctly");
	}

	public static TheoryData<string, ushort, ulong> GetInstructionTestCases ()
	{
		var cases = new TheoryData<string, ushort, ulong> ();

		// --- Arithmetic Operations ---
		Add ("Adcs", InstructionEmiter.Adcs (R4, R4), &ArithmeticOps.Adcs);
		Add ("AddSpImm7", InstructionEmiter.AddSpImm7 (0x10), &ArithmeticOps.AddSpImmediate7);
		Add ("AddSpImm8", InstructionEmiter.AddSpImm8 (R1, 0x10), &ArithmeticOps.AddSpImmediate8);
		Add ("AddsImm3", InstructionEmiter.AddsImm3 (R1, R2, 3), &ArithmeticOps.AddsImmediate3);
		Add ("AddsImm8", InstructionEmiter.AddsImm8 (R1, 1), &ArithmeticOps.AddsImmediate8);
		Add ("AddsRegister", InstructionEmiter.AddsRegister (R1, R2, R7), &ArithmeticOps.AddsRegister);
		Add ("Adr", InstructionEmiter.Adr (R4, 0x50), &ArithmeticOps.Adr);
		Add("SubsRegister", InstructionEmiter.SubsReg(R1, R2, R4), &ArithmeticOps.SubsRegister);
		Add("SubsImm3", InstructionEmiter.SubsImm3(R1, R2, 3), &ArithmeticOps.SubsImmediate3);
		Add("SubsImm8", InstructionEmiter.SubsImm8(R1, 0x10), &ArithmeticOps.SubsImmediate8);
		Add("SubSp", InstructionEmiter.SubSp(0x10), &ArithmeticOps.SubSp);
 
		// Special Cases for AddHighRegister
		Add ("AddHighReg (Reg)", InstructionEmiter.AddHighRegisters (R1, R2), &ArithmeticOps.AddHighToReg);
		Add ("AddHighReg (Sp)", InstructionEmiter.AddHighRegisters (SP, R2), &ArithmeticOps.AddHighToSp);
		Add ("AddHighReg (Pc)", InstructionEmiter.AddHighRegisters (PC, R2), &ArithmeticOps.AddHighToPc);

		Add ("Cmn", InstructionEmiter.Cmn (R7, R2), &ArithmeticOps.Cmn);
		Add ("CmpImm", InstructionEmiter.CmpImm (R5, 66), &ArithmeticOps.CmpImmediate);
		Add ("CmpRegister", InstructionEmiter.CmpRegister (R5, R0), &ArithmeticOps.CmpRegister);
		Add ("CmpHighRegister", InstructionEmiter.CmpHighRegister (R11, R3), &ArithmeticOps.CmpHighRegister);
		Add ("Muls", InstructionEmiter.Muls (R0, R2), &ArithmeticOps.Muls);

		// --- Bit Operations ---
		Add ("Ands", InstructionEmiter.Ands (R5, R0), &BitOps.Ands);
		Add ("AsrsImm5", InstructionEmiter.AsrsImm5 (R3, R2, 31), &BitOps.AsrsImm5);
		Add ("AsrsRegister", InstructionEmiter.AsrsRegister (R3, R4), &BitOps.AsrsRegister);
		Add ("Bics", InstructionEmiter.Bics (R0, R3), &BitOps.Bics);
		Add ("Eors", InstructionEmiter.Eors (R1, R3), &BitOps.Eors);
		Add ("LslsImm", InstructionEmiter.LslsImm5 (R5, R5, 18), &BitOps.LslsImm5);
		Add ("LslsImmZero", InstructionEmiter.LslsImm5 (R5, R5, 0), &BitOps.LslsZero);
		Add ("LslsRegister", InstructionEmiter.LslsRegister (R5, R0), &BitOps.LslsRegister);
		Add ("Mvns", InstructionEmiter.Mvns (R0, R2), &BitOps.Mvns);
		Add ("Orrs", InstructionEmiter.Orrs (R5, R0), &BitOps.Orrs);
		Add ("Rev", InstructionEmiter.Rev (R0, R1), &BitOps.Rev);
		Add ("Revsh", InstructionEmiter.Revsh (R0, R1), &BitOps.Revsh);

		// Mov Variations
		Add ("Mov (Reg)", InstructionEmiter.Mov (R3, R8), &BitOps.MovRegister);
		Add ("Mov (Pc)", InstructionEmiter.Mov (PC, R8), &BitOps.MovToPc);
		Add ("Mov (Sp)", InstructionEmiter.Mov (SP, R8), &BitOps.MovToSp);

		// --- Flow Control ---
		for (uint cond = 0; cond <= 13; cond++) {
			InstructionHandler expected = cond switch {
				0x0 => &FlowOps.Beq, 0x1 => &FlowOps.Bne, 0x2 => &FlowOps.Bcs, 0x3 => &FlowOps.Bcc,
				0x4 => &FlowOps.Bmi, 0x5 => &FlowOps.Bpl, 0x6 => &FlowOps.Bvs, 0x7 => &FlowOps.Bvc,
				0x8 => &FlowOps.Bhi, 0x9 => &FlowOps.Bls, 0xA => &FlowOps.Bge, 0xB => &FlowOps.Blt,
				0xC => &FlowOps.Bgt, 0xD => &FlowOps.Ble,
				_ => throw new System.Exception ()
			};
			Add ($"BranchConditional ({cond})", InstructionEmiter.BranchConditional (cond, 0), expected);
		}

		Add ("Bl", (ushort)(InstructionEmiter.Bl (0x34) & 0xFFFF), &FlowOps.Bl);
		Add ("Blx", InstructionEmiter.Blx (R3), &FlowOps.Blx);
		Add ("Branch", InstructionEmiter.Branch (0xfec), &FlowOps.Branch);
		Add ("Bx", InstructionEmiter.Bx (LR), &FlowOps.Bx);

		// --- System & Memory ---
		Add ("Dmb", (ushort)(InstructionEmiter.Dmb & 0xFFFF), &SystemOps.Barrier);
		Add ("Dsb", (ushort)(InstructionEmiter.Dsb & 0xFFFF), &SystemOps.Barrier);
		Add ("Isb", (ushort)(InstructionEmiter.Isb & 0xFFFF), &SystemOps.Barrier);
		Add ("Nop", InstructionEmiter.Nop, &SystemOps.Nop);
		Add ("Mrs", (ushort)(InstructionEmiter.Mrs (R0, 5) & 0xFFFF), &SystemOps.Mrs);
		Add ("Msr", (ushort)(InstructionEmiter.Msr (8, R0) & 0xFFFF), &SystemOps.Msr);

		Add ("Ldmia", InstructionEmiter.Ldmia (R0, (1 << R1) | (1 << R2)), &MemoryOps.Ldmia);

		// Push / Pop
		Add ("Pop", InstructionEmiter.Pop (false, (1 << R4)), &MemoryOps.Pop);
		Add ("Pop (PC)", InstructionEmiter.Pop (true, (1 << R4)), &MemoryOps.PopPc);
		Add ("Push", InstructionEmiter.Push (false, (1 << R4)), &MemoryOps.Push);
		Add ("Push (LR)", InstructionEmiter.Push (true, (1 << R4)), &MemoryOps.PushLr);

		return cases;

		void Add (string name, ushort opcode, InstructionHandler handler)
		{
			cases.Add (name, opcode, AddressOf (handler));
		}
	}
}
