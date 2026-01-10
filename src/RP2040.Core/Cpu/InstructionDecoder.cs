using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RP2040.Core.Cpu.Instructions;
using unsafe InstructionHandler = delegate* managed<ushort, RP2040.Core.Cpu.CortexM0Plus, void>;

namespace RP2040.Core.Cpu;

public unsafe class InstructionDecoder : IDisposable
{
	public static InstructionDecoder Instance { get; } = new InstructionDecoder ();

	private readonly InstructionHandler[] _lookupTable = new InstructionHandler[65536];
	private GCHandle _pinnedHandle;
	private readonly InstructionHandler* _fastTablePtr;

	private readonly struct OpcodeRule (ushort mask, ushort pattern, InstructionHandler handler)
	{
		public readonly ushort Mask = mask;
		public readonly ushort Pattern = pattern;
		public readonly InstructionHandler Handler = handler;
	}

	public InstructionDecoder ()
	{
		_pinnedHandle = GCHandle.Alloc (_lookupTable, GCHandleType.Pinned);
		_fastTablePtr = (InstructionHandler*)_pinnedHandle.AddrOfPinnedObject ();

		InstructionHandler undefinedPtr = &HandleUndefined;
		fixed (InstructionHandler* ptrToArr = _lookupTable) {
			new Span<nuint> (ptrToArr, _lookupTable.Length).Fill ((nuint)undefinedPtr);
		}

		ReadOnlySpan<OpcodeRule> rules = [
			// ================================================================
			// GROUP 1: Mask 0xFFFF (Exact Match - Max Priority)
			// ================================================================
			// MRS Rd, spec_reg (F3EF)
			new OpcodeRule (0xFFFF, 0xF3EF, &SystemOps.Mrs),
			// DMB, DSB, ISB (F3BF)
			// Mask: 1111 1111 1111 1111
			new OpcodeRule (0xFFFF, 0xF3BF, &SystemOps.Barrier),

			// ================================================================
			// GROUP 2: Mask 0xFFF0
			// ================================================================
			// MSR spec_reg, Rn (F38x)
			new OpcodeRule (0xFFF0, 0xF380, &SystemOps.Msr),

			// ================================================================
			// GROUP 3: Mask 0xFFC0 (10 bits significant)
			// IMPORTANT: Must come before 0xFF00 to prevent generic instructions
			// (like ORRS or ADD generic) from shadowing these specific opcodes.
			// ================================================================
			// ADCS (Rd, Rm)
			new OpcodeRule (0xFFC0, 0x4140, &ArithmeticOps.Adcs),
			// ANDS (Rn, Rm)
			new OpcodeRule (0xFFC0, 0x4000, &BitOps.Ands),
			// ASRS (Register) - Encoding T2
			new OpcodeRule (0xFFC0, 0x4100, &BitOps.AsrsRegister),
			// BICS (Rdn, Rm)
			new OpcodeRule (0xFFC0, 0x4380, &BitOps.Bics),
			// CMN (Rn, Rm)
			new OpcodeRule (0xFFC0, 0x42C0, &ArithmeticOps.Cmn),
			// CMP Rn, Rm (Low Registers - Encoding T1)
			new OpcodeRule (0xFFC0, 0x4280, &ArithmeticOps.CmpRegister),
			// EORS Rdn, Rm
			new OpcodeRule (0xFFC0, 0x4040, &BitOps.Eors),
			// MULS Rn, Rdm - (Must be before ORRS 0x4300)
			new OpcodeRule (0xFFC0, 0x4340, &ArithmeticOps.Muls),
			// MVNS Rd, Rm
			new OpcodeRule (0xFFC0, 0x43C0, &BitOps.Mvns),
			// LSLS Rd, Rm, #0
			new OpcodeRule (0xFFC0, 0x0000, &BitOps.LslsZero),
			// LSLS (Register) - Encoding T2
			new OpcodeRule (0xFFC0, 0x4080, &BitOps.LslsRegister),
			// REV (Rd, Rn)
			new OpcodeRule (0xFFC0, 0xBA00, &BitOps.Rev),

			// ================================================================
			// GROUP 4: Mask 0xFF87 (High Register Special Cases)
			// CRITICAL: These are specific cases of the 0xFF00 generic group.
			// They verify Bit 7 (DN) and Bits 0-2 (Rd/Rm).
			// ================================================================
			// 1. High Priority: ADD PC, Rm (R15)
			new OpcodeRule (0xFF87, 0x4487, &ArithmeticOps.AddHighToPc),
			// 2. High Priority: ADD SP, Rm (R13)
			new OpcodeRule (0xFF87, 0x4485, &ArithmeticOps.AddHighToSp),
			// BLX Rm
			new OpcodeRule (0xFF87, 0x4780, &FlowOps.Blx),
			// BX Rm
			new OpcodeRule (0xFF87, 0x4700, &FlowOps.Bx),
			// 1. MOV PC, Rm (High Priority)
			new OpcodeRule (0xFF87, 0x4687, &BitOps.MovToPc),
			// 2. MOV SP, Rm (High Priority)
			new OpcodeRule (0xFF87, 0x4685, &BitOps.MovToSp),

			// ================================================================
			// GROUP 5: Mask 0xFF80
			// ================================================================
			// ADD (SP + imm7)
			new OpcodeRule (0xFF80, 0xB000, &ArithmeticOps.AddSpImmediate7),

			// ================================================================
			// GROUP 6: Mask 0xFF00 (8 bits significant - Broad Categories)
			// ================================================================
			// 3. Low Priority: ADD Generic (R0-R12, R14)
			new OpcodeRule (0xFF00, 0x4400, &ArithmeticOps.AddHighToReg),
			// CMP Rn, Rm (High Registers - Encoding T2)
			new OpcodeRule (0xFF00, 0x4500, &ArithmeticOps.CmpHighRegister),
			// 3. MOV Rd, Rm (Generic Fallback)
			new OpcodeRule (0xFF00, 0x4600, &BitOps.MovRegister),
			// ORRS (Rd, Rm) - NOTE: This covers 0x4300-0x43FF.
			// MULS (0x4340) and MVNS (0x43C0) are subsets and were handled above.
			new OpcodeRule (0xFF00, 0x4300, &ArithmeticOps.Orrs),

			// Stack Operations
			new OpcodeRule (0xFF00, 0xBC00, &MemoryOps.Pop),
			new OpcodeRule (0xFF00, 0xBD00, &MemoryOps.PopPc),
			new OpcodeRule (0xFF00, 0xB400, &MemoryOps.Push),
			new OpcodeRule (0xFF00, 0xB500, &MemoryOps.PushLr),

			// Conditional Branches (T1)
			// SVC (0xDF00) is technically caught here if not handled separately.
			// Ensure the handler filters 0xF (SVC) or add a specific SVC rule with higher priority.
			new OpcodeRule (0xFF00, 0xD000, &FlowOps.Beq),
			new OpcodeRule (0xFF00, 0xD100, &FlowOps.Bne),
			new OpcodeRule (0xFF00, 0xD200, &FlowOps.Bcs),
			new OpcodeRule (0xFF00, 0xD300, &FlowOps.Bcc),
			new OpcodeRule (0xFF00, 0xD400, &FlowOps.Bmi),
			new OpcodeRule (0xFF00, 0xD500, &FlowOps.Bpl),
			new OpcodeRule (0xFF00, 0xD600, &FlowOps.Bvs),
			new OpcodeRule (0xFF00, 0xD700, &FlowOps.Bvc),
			new OpcodeRule (0xFF00, 0xD800, &FlowOps.Bhi),
			new OpcodeRule (0xFF00, 0xD900, &FlowOps.Bls),
			new OpcodeRule (0xFF00, 0xDA00, &FlowOps.Bge),
			new OpcodeRule (0xFF00, 0xDB00, &FlowOps.Blt),
			new OpcodeRule (0xFF00, 0xDC00, &FlowOps.Bgt),
			new OpcodeRule (0xFF00, 0xDD00, &FlowOps.Ble),

			// ================================================================
			// GROUP 7: Mask 0xFE00
			// ================================================================
			// ADDS (Rd, Rn, Rm) - Encoding T1 Register
			new OpcodeRule (0xFE00, 0x1800, &ArithmeticOps.AddsRegister),
			// ADDS (Rd, Rn, imm3)
			new OpcodeRule (0xFE00, 0x1C00, &ArithmeticOps.AddsImmediate3),

			// ================================================================
			// GROUP 8: Mask 0xF800 (5 bits significant - Most Generic)
			// ================================================================
			// ADD (Rd = SP + imm8)
			new OpcodeRule (0xF800, 0xA800, &ArithmeticOps.AddSpImmediate8),
			// ADDS (Rd, imm8)
			new OpcodeRule (0xF800, 0x3000, &ArithmeticOps.AddsImmediate8),
			// ADR (Rd, imm8)
			new OpcodeRule (0xF800, 0xA000, &ArithmeticOps.Adr),
			// ASRS (Rd, Rm, imm5)
			new OpcodeRule (0xF800, 0x1000, &BitOps.AsrsImm5),
			// BL (Branch with Link)
			new OpcodeRule (0xF800, 0xF000, &FlowOps.Bl),
			// B (Unconditional) - T2
			new OpcodeRule (0xF800, 0xE000, &FlowOps.Branch),
			// CMP Rn, #imm8
			new OpcodeRule (0xF800, 0x2800, &ArithmeticOps.CmpImmediate),
			// MOVS (Rd, #imm8)
			new OpcodeRule (0xF800, 0x2000, &BitOps.Movs),
			// LDMIA (Load Multiple Increment After)
			new OpcodeRule (0xF800, 0xC800, &MemoryOps.Ldmia),
			// LSLS (Rd, Rm, imm5)
			new OpcodeRule (0xF800, 0x0000, &BitOps.LslsImm5),

			// ================================================================
			// GROUP 9: Mask 0xBF00
			// ================================================================
			// NOP (Hint)
			new OpcodeRule (0xBF00, 0xBF00, &SystemOps.Nop),
		];

		for (var i = 0; i < 65536; i++) {
			var opcode = (ushort)i;
			foreach (ref readonly var rule in rules) {
				if ((opcode & rule.Mask) != rule.Pattern)
					continue;
				_fastTablePtr[i] = rule.Handler;
				break;
			}
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void Dispatch (ushort opcode, CortexM0Plus cpu)
	{
		_fastTablePtr[opcode] (opcode, cpu);
	}

	public nuint GetHandler (ushort opcode)
	{
		return (nuint)_fastTablePtr[opcode];
	}

	private static void HandleUndefined (ushort opcode, CortexM0Plus cpu)
	{
		throw new Exception ($"Undefined Opcode: 0x{opcode:X4} PC={cpu.Registers.PC:X8}");
	}

	public void Dispose ()
	{
		if (_pinnedHandle.IsAllocated) _pinnedHandle.Free ();
	}
}
