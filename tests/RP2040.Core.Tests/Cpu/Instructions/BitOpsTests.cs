using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
using RP2040.tests.Fixtures;

namespace RP2040.tests.Cpu.Instructions;

public abstract class BitOpsTests
{
	public class Ands : CpuTestBase
	{
		[Fact]
		public void Should_CalculateBitwiseAnd_And_UpdateNegativeFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ands (R5, R0);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R5] = 0xffff0000;
			Cpu.Registers[R0] = 0xf00fffff;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R5].Should ().Be (0xf00f0000);
			Cpu.Registers.N.Should ().BeTrue ();
			Cpu.Registers.Z.Should ().BeFalse ();
		}
	}

	public abstract class Asrs
	{
		public class Immediate : CpuTestBase
		{
			[Fact]
			public void Should_SignExtend_When_ShiftingMaxNegative_By31 ()
			{
				// Arrange
				var opcode = InstructionEmiter.AsrsImm5 (R3, R2, 31);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R2] = 0x80000000;
				Cpu.Registers.C = true;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0xffffffff); // -1 (Sign extension)
				Cpu.Registers.PC.Should ().Be (0x20000002);

				Cpu.Registers.N.Should ().BeTrue ();
				Cpu.Registers.Z.Should ().BeFalse ();
				Cpu.Registers.C.Should ().BeFalse ();
			}

			[Fact]
			public void Should_PerformArithmeticShiftBy32_When_ImmediateIsZero ()
			{
				// Arrange
				var opcode = InstructionEmiter.AsrsImm5 (R3, R2, 0);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R2] = 0x80000000;
				Cpu.Registers.C = false;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0xffffffff);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.N.Should ().BeTrue ();
				Cpu.Registers.Z.Should ().BeFalse ();
				Cpu.Registers.C.Should ().BeTrue ();
			}
		}

		public class Register : CpuTestBase
		{
			[Fact]
			public void Should_UseOnlyBottomByteOfShiftRegister ()
			{
				// Arrange
				var opcode = InstructionEmiter.AsrsRegister (R3, R4);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R3] = 0x80000040;
				Cpu.Registers[R4] = 0xff500007;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0xff000000);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.N.Should ().BeTrue ();
				Cpu.Registers.Z.Should ().BeFalse ();
				Cpu.Registers.C.Should ().BeTrue ();
			}

			[Fact]
			public void Should_ResultInZero_When_ShiftingPositiveNumber_ByMoreThan31 ()
			{
				// Arrange
				var opcode = InstructionEmiter.AsrsRegister (R3, R4);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R3] = 0x40000040;
				Cpu.Registers[R4] = 50;
				Cpu.Registers.C = true;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.N.Should ().BeFalse ();
				Cpu.Registers.Z.Should ().BeTrue ();
				Cpu.Registers.C.Should ().BeFalse ();
			}

			[Fact]
			public void Should_ShiftBy31_And_UpdateCarry_WithBit30 ()
			{
				// Arrange
				var opcode = InstructionEmiter.AsrsRegister (R3, R4);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R3] = 0x40000040;
				Cpu.Registers[R4] = 31;
				Cpu.Registers.C = true;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.N.Should ().BeFalse ();
				Cpu.Registers.Z.Should ().BeTrue ();
				Cpu.Registers.C.Should ().BeTrue ();
			}

			[Fact]
			public void Should_ResultInMinusOne_When_ShiftingNegativeNumber_ByMoreThan31 ()
			{
				// Arrange
				var opcode = InstructionEmiter.AsrsRegister (R3, R4);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R3] = 0x80000040;
				Cpu.Registers[R4] = 50;
				Cpu.Registers.C = true;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0xffffffff);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.N.Should ().BeTrue ();
				Cpu.Registers.Z.Should ().BeFalse ();
				Cpu.Registers.C.Should ().BeTrue ();
			}

			[Fact]
			public void Should_PreserveValue_And_PreserveCarry_When_ShiftIsZero ()
			{
				// Arrange
				var opcode = InstructionEmiter.AsrsRegister (R3, R4);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R3] = 0x80000040;
				Cpu.Registers[R4] = 0;
				Cpu.Registers.C = true;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0x80000040);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.N.Should ().BeTrue ();
				Cpu.Registers.Z.Should ().BeFalse ();
				Cpu.Registers.C.Should ().BeTrue ();
			}
		}
	}

	public class Bics : CpuTestBase
	{
		[Fact]
		public void Should_ClearBits_WhereMaskIsSet ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bics (R0, R3);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0xff;
			Cpu.Registers[R3] = 0x0f;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R0].Should ().Be (0xf0);
			Cpu.Registers.N.Should ().BeFalse ();
			Cpu.Registers.Z.Should ().BeFalse ();
		}

		[Fact]
		public void Should_SetNegativeFlag_When_ResultHasSignBitSet ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bics (R0, R3);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0xffffffff;
			Cpu.Registers[R3] = 0x0000ffff;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R0].Should ().Be (0xffff0000);
			Cpu.Registers.N.Should ().BeTrue ();
			Cpu.Registers.Z.Should ().BeFalse ();
		}
	}

	public class Eors : CpuTestBase
	{
		[Fact]
		public void Should_CalculateExclusiveOr_And_UpdateNegativeFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.Eors (R1, R3);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R1] = 0xf0f0f0f0;
			Cpu.Registers[R3] = 0x08ff3007;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R1].Should ().Be (0xf80fc0f7);
			Cpu.Registers.N.Should ().BeTrue ();
			Cpu.Registers.Z.Should ().BeFalse ();
		}
	}

	public class Mov : CpuTestBase
	{
		[Fact]
		public void Should_CopyValue_BetweenRegisters ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mov (R3, R8);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R8] = 55;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R3].Should ().Be (55);
		}

		[Fact]
		public void Should_ReadProgramCounter_WithPipelineOffset ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mov (R3, PC);
			Bus.WriteHalfWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R3].Should ().Be (0x20000004);
		}

		[Fact]
		public void Should_MoveRegisterToStackPointer_And_EnforceAlignment ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mov (SP, R8);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R8] = 55;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[SP].Should ().Be (52);
		}

		[Fact]
		public void Should_ClearLowerTwoBits_When_WritingToStackPointer ()
		{
			// Arrange
			Cpu.Registers.PC = 0x20000000;
			var opcode = InstructionEmiter.Mov (SP, R5);
			Bus.WriteHalfWord (0x20000000, opcode);
			Cpu.Registers.R5 = 0x53;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.SP.Should ().Be (0x50);
		}

		[Fact]
		public void Should_ClearLeastSignificantBit_When_WritingToProgramCounter ()
		{
			// Arrange
			Cpu.Registers.PC = 0x20000000;
			var opcode = InstructionEmiter.Mov (PC, R5);
			Bus.WriteHalfWord (0x20000000, opcode); // mov pc, r5
			Cpu.Registers.R5 = 0x53;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x52);
		}
	}

	public class Movs : CpuTestBase
	{
		[Fact]
		public void Should_LoadImmediateValue_And_UpdateFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.Movs (R5, 128);
			Bus.WriteHalfWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R5].Should ().Be (128);
			Cpu.Registers.PC.Should ().Be (0x20000002);
		}
	}

	public class Mvns : CpuTestBase
	{
		[Fact]
		public void Should_BitwiseInvert_Value_And_UpdateNegativeFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mvns (R4, R3);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R3] = 0x11115555;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R4].Should ().Be (0xeeeeaaaa);
			Cpu.Registers.N.Should ().BeTrue ();
			Cpu.Registers.Z.Should ().BeFalse ();
		}
	}

	public class Lsls
	{
		public class Immediate : CpuTestBase
		{
			[Fact]
			public void Should_ShiftLeft_ByImmediateValue ()
			{
				// Arrange
				var opcode = InstructionEmiter.LslsImm5 (R5, R5, 18);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R5] = 0b00000000000000000011; // 0b11
				Cpu.Registers.C = false;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R5].Should ().Be (0b11000000000000000000);
				Cpu.Registers.C.Should ().BeFalse ();
				Cpu.Registers.PC.Should ().Be (0x20000002);
			}

			[Fact]
			public void Should_SetCarryFlag_When_BitIsShiftedOut ()
			{
				// Arrange
				var opcode = InstructionEmiter.LslsImm5 (R5, R5, 18);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R5] = 0x4001;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R5].Should ().Be (0x40000);
				Cpu.Registers.C.Should ().BeTrue ();
			}

			[Fact]
			public void Should_PreserveCarry_When_ShiftIsZero ()
			{
				// Arrange
				var opcode = InstructionEmiter.LslsImm5 (R5, R5, 0); // LSLS R5, R5, #0
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R5] = 0xFFFF;
				Cpu.Registers.C = true;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R5].Should ().Be (0xFFFF);
				Cpu.Registers.C.Should ().BeTrue ("LSL #0 should preserve Carry flag (MOVS behavior)");
				Cpu.Registers.Z.Should ().BeFalse ();
				Cpu.Registers.N.Should ().BeFalse ();
			}
		}

		public class Register : CpuTestBase
		{
			[Fact]
			public void Should_UseOnlyBottomByte_Of_ShiftRegister ()
			{
				// Arrange
				var opcode = InstructionEmiter.LslsRegister (R5, R0);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R5] = 0b00000000000000000011;
				Cpu.Registers[R0] = 0xFF003302;
				Cpu.Registers.C = false;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R5].Should ().Be (0b00000000000000001100);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.C.Should ().BeFalse ();
			}

			[Fact]
			public void Should_ResultInZero_And_SetCarry_When_ShiftingBy32 ()
			{
				// Arrange
				var opcode = InstructionEmiter.LslsRegister (R3, R4);
				Bus.WriteHalfWord (0x20000000, opcode);

				Cpu.Registers[R3] = 1;
				Cpu.Registers[R4] = 0x20;
				Cpu.Registers.C = false;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers[R3].Should ().Be (0);
				Cpu.Registers.PC.Should ().Be (0x20000002);
				Cpu.Registers.N.Should ().BeFalse ();
				Cpu.Registers.Z.Should ().BeTrue (); // Result is 0
				Cpu.Registers.C.Should ().BeTrue (); // Bit 0 shifted out
			}
		}
	}
	
	public class Rev : CpuTestBase
	{
		[Fact]
		public void Should_ReverseByteOrder_Of_32BitWord ()
		{
			// Arrange
			var opcode = InstructionEmiter.Rev (R2, R3);
			Bus.WriteHalfWord (0x20000000, opcode);
			Cpu.Registers[R3] = 0x11223344;
			
			// Act
			Cpu.Step();
			
			// Assert
			Cpu.Registers.R2.Should ().Be (0x44332211);
		}
	}

	public class Revsh : CpuTestBase
	{
		[Fact]
		public void Should_ReverseBytesInLowHalfword_And_SignExtend ()
		{
			// Arrange
			var opcode = InstructionEmiter.Revsh (R1, R2);
			Bus.WriteHalfWord (0x20000000, opcode);
			
			Cpu.Registers[R2] = 0xeeaa55f0;
			
			// Act
			Cpu.Step ();
			
			// Assert
			Cpu.Registers[R1].Should ().Be (0xfffff055);
		}
	}

	public class Rev16
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;
		public Rev16()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecuteRev16R0R5Instruction()
		{
			// Arrange
			var opcode = InstructionEmiter.Rev16 (R0, R5);
			_bus.WriteHalfWord (0x20000000, opcode);
			_cpu.Registers[R5] = 0x11223344;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R0].Should ().Be (0x22114433);
		}
	}
}
