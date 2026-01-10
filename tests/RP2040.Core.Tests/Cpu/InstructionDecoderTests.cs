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

	static nuint Addr (InstructionHandler handler) => (nuint)handler;
	static ushort FirstHalf(uint opcode32) => (ushort)(opcode32 & 0xFFFF);
	static readonly InstructionDecoder Decoder = InstructionDecoder.Instance;

	[Theory]
	[MemberData(nameof(ArithmeticCases))]
	[MemberData(nameof(BitOpsCases))]
	[MemberData(nameof(FlowCases))]
	[MemberData(nameof(SystemCases))]
	[MemberData(nameof(MemoryCases))]
	public void ShouldMapCorrectly (string name, ushort opcode, nuint expectedHandlerAddress)
	{
		// Act
		var actualHandler = Decoder.GetHandler (opcode);

		// Assert
		actualHandler.Should ().Be (expectedHandlerAddress, $"The instruction '{name}' should decode correctly");
	}
	
	public static TheoryData<string, ushort, nuint> ArithmeticCases => new TheoryData<string, ushort, UIntPtr>
	{
		{ "Adcs", InstructionEmiter.Adcs(R4, R4), Addr(&ArithmeticOps.Adcs) },
		{ "AddSpImm7", InstructionEmiter.AddSpImm7(0x10), Addr(&ArithmeticOps.AddSpImmediate7) },
		{ "AddSpImm8", InstructionEmiter.AddSpImm8(R1, 0x10), Addr(&ArithmeticOps.AddSpImmediate8) },
		{ "AddsImm3", InstructionEmiter.AddsImm3(R1, R2, 3), Addr(&ArithmeticOps.AddsImmediate3) },
		{ "AddsImm8", InstructionEmiter.AddsImm8(R1, 1), Addr(&ArithmeticOps.AddsImmediate8) },
		{ "AddsRegister", InstructionEmiter.AddsRegister(R1, R2, R7), Addr(&ArithmeticOps.AddsRegister) },
		{ "Adr", InstructionEmiter.Adr(R4, 0x50), Addr(&ArithmeticOps.Adr) },
        
		// Special Cases (High Register)
		{ "AddHighReg (Reg)", InstructionEmiter.AddHighRegisters(R1, R2), Addr(&ArithmeticOps.AddHighToReg) },
		{ "AddHighReg (Sp)", InstructionEmiter.AddHighRegisters(SP, R2), Addr(&ArithmeticOps.AddHighToSp) },
		{ "AddHighReg (Pc)", InstructionEmiter.AddHighRegisters(PC, R2), Addr(&ArithmeticOps.AddHighToPc) },

		{ "Cmn", InstructionEmiter.Cmn(R7, R2), Addr(&ArithmeticOps.Cmn) },
		{ "CmpImm", InstructionEmiter.CmpImm(R5, 66), Addr(&ArithmeticOps.CmpImmediate) },
		{ "CmpRegister", InstructionEmiter.CmpRegister(R5, R0), Addr(&ArithmeticOps.CmpRegister) },
		{ "CmpHighRegister", InstructionEmiter.CmpHighRegister(R11, R3), Addr(&ArithmeticOps.CmpHighRegister) },
		{ "Muls", InstructionEmiter.Muls(R0, R2), Addr(&ArithmeticOps.Muls) },
		{ "Orrs", InstructionEmiter.Orrs(R5, R0), Addr(&ArithmeticOps.Orrs) }
	};
	
	public static TheoryData<string, ushort, nuint> BitOpsCases => new TheoryData<string, ushort, UIntPtr>
	{
        { "Ands", InstructionEmiter.Ands(R5, R0), Addr(&BitOps.Ands) },
        { "AsrsImm5", InstructionEmiter.AsrsImm5(R3, R2, 31), Addr(&BitOps.AsrsImm5) },
        { "AsrsRegister", InstructionEmiter.AsrsRegister(R3, R4), Addr(&BitOps.AsrsRegister) },
        { "Bics", InstructionEmiter.Bics(R0, R3), Addr(&BitOps.Bics) },
        { "Eors", InstructionEmiter.Eors(R1, R3), Addr(&BitOps.Eors) },
        { "LslsImm", InstructionEmiter.LslsImm5(R5, R5, 18), Addr(&BitOps.LslsImm5) },
        { "LslsImmZero", InstructionEmiter.LslsImm5(R5, R5, 0), Addr(&BitOps.LslsZero) },
        { "LslsRegister", InstructionEmiter.LslsRegister(R5, R0), Addr(&BitOps.LslsRegister) },
        { "Mvns", InstructionEmiter.Mvns(R0, R2), Addr(&BitOps.Mvns) },
        
        // Mov Variations
        { "Mov (Reg)", InstructionEmiter.Mov(R3, R8), Addr(&BitOps.MovRegister) },
        { "Mov (Pc)", InstructionEmiter.Mov(PC, R8), Addr(&BitOps.MovToPc) },
        { "Mov (Sp)", InstructionEmiter.Mov(SP, R8), Addr(&BitOps.MovToSp) }
    };

    public static TheoryData<string, ushort, nuint> FlowCases
    {
        get
        {
            var data = new TheoryData<string, ushort, nuint>();
            
            // BranchConditional Generation
            for (uint cond = 0; cond <= 13; cond++)
            {
                InstructionHandler expected = cond switch {
                    0x0 => &FlowOps.Beq, 0x1 => &FlowOps.Bne, 0x2 => &FlowOps.Bcs, 0x3 => &FlowOps.Bcc,
                    0x4 => &FlowOps.Bmi, 0x5 => &FlowOps.Bpl, 0x6 => &FlowOps.Bvs, 0x7 => &FlowOps.Bvc,
                    0x8 => &FlowOps.Bhi, 0x9 => &FlowOps.Bls, 0xA => &FlowOps.Bge, 0xB => &FlowOps.Blt,
                    0xC => &FlowOps.Bgt, 0xD => &FlowOps.Ble,
                    _ => throw new Exception("Invalid condition")
                };
                data.Add($"BranchConditional ({cond})", InstructionEmiter.BranchConditional(cond, 0), Addr(expected));
            }

            // Other Flow Ops (Using FirstHalf for 32-bit instructions)
            data.Add("Bl", FirstHalf(InstructionEmiter.Bl(0x34)), Addr(&FlowOps.Bl));
            data.Add("Blx", InstructionEmiter.Blx(R3), Addr(&FlowOps.Blx));
            data.Add("Branch", InstructionEmiter.Branch(0xfec), Addr(&FlowOps.Branch));
            data.Add("Bx", InstructionEmiter.Bx(LR), Addr(&FlowOps.Bx));

            return data;
        }
    }

    public static TheoryData<string, ushort, nuint> SystemCases => new TheoryData<string, ushort, UIntPtr>
    {
        { "Dmb", FirstHalf(InstructionEmiter.Dmb), Addr(&SystemOps.Barrier) },
        { "Dsb", FirstHalf(InstructionEmiter.Dsb), Addr(&SystemOps.Barrier) },
        { "Isb", FirstHalf(InstructionEmiter.Isb), Addr(&SystemOps.Barrier) },
        { "Nop", InstructionEmiter.Nop, Addr(&SystemOps.Nop) },
        { "Mrs", FirstHalf(InstructionEmiter.Mrs(R0, 5)), Addr(&SystemOps.Mrs) },
        { "Msr", FirstHalf(InstructionEmiter.Msr(8, R0)), Addr(&SystemOps.Msr) }
    };

    public static TheoryData<string, ushort, nuint> MemoryCases => new()
    {
        { "Ldmia", InstructionEmiter.Ldmia(R0, (1 << R1) | (1 << R2)), Addr(&MemoryOps.Ldmia) },
        
        // Push / Pop Variants
        { "Pop", InstructionEmiter.Pop(false, (1 << R4)), Addr(&MemoryOps.Pop) },
        { "Pop (PC)", InstructionEmiter.Pop(true, (1 << R4)), Addr(&MemoryOps.PopPc) },
        { "Push", InstructionEmiter.Push(false, (1 << R4)), Addr(&MemoryOps.Push) },
        { "Push (LR)", InstructionEmiter.Push(true, (1 << R4)), Addr(&MemoryOps.PushLr) }
    };
}
