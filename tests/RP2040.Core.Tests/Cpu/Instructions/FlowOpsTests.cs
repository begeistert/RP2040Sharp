using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
using RP2040.tests.Fixtures;
namespace RP2040.tests.Cpu.Instructions;

public abstract class FlowOpsTests
{
	public class Bl : CpuTestBase
	{
		[Fact]
		public void Should_BranchForward_And_SetLinkRegister_WithThumbBit ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bl (0x34);
			Bus.WriteWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000038);
			Cpu.Registers.LR.Should ().Be (0x20000005);
			Cpu.Cycles.Should ().Be (3);
		}

		[Fact]
		public void Should_BranchBackward_And_SetLinkRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bl (-0x10);
			Bus.WriteWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000004 - 0x10);
			Cpu.Registers.LR.Should ().Be (0x20000005);
			Cpu.Cycles.Should ().Be (3);
		}

		[Fact]
		public void Should_BranchBackward_WithLargeOffset_And_SignExtendCorrectly ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bl (-3242);
			Bus.WriteWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000004 - 3242);
			Cpu.Registers.LR.Should ().Be (0x20000005);
			Cpu.Cycles.Should ().Be (3);
		}
	}

	public class Blx : CpuTestBase
	{
		[Fact]
		public void Should_BranchToRegisterAddress_And_UpdateLinkRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.Blx (R3);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R3] = 0x20000201;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000200);
			Cpu.Registers.LR.Should ().Be (0x20000003);
			Cpu.Cycles.Should ().Be (2);
		}
	}

	public abstract class Branch : CpuTestBase
	{
		public class Unconditional : CpuTestBase
		{
			[Fact]
			public void Should_BranchBackward_Unconditionally ()
			{
				// Arrange
				var opcode = InstructionEmiter.Branch (0xfec);
				Bus.WriteHalfWord (0x20000000 + 9 * 2, opcode);

				Cpu.Registers.PC = 0x20000000 + 9 * 2;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers.PC.Should ().Be (0x20000002);
			}
		}
		
		public class Conditional : CpuTestBase
		{
			[Fact]
			public void Should_Branch_When_ConditionIsMet ()
			{
				// Arrange
				var opcode = InstructionEmiter.BranchConditional (1, 0x1f8);
				Bus.WriteHalfWord (0x20000000 + 9 * 2, opcode);

				Cpu.Registers.PC = 0x20000000 + 9 * 2;
				Cpu.Registers.Z = false;

				// Act
				Cpu.Step ();

				// Assert
				Cpu.Registers.PC.Should ().Be (0x2000000e);
			}
			
			[Fact]
			public void Should_ContinueToNextInstruction_When_ConditionIsNotMet()
			{
				// Arrange
				const uint jumpOffset = 0x1f8u;
				var opcode = InstructionEmiter.BranchConditional(1, jumpOffset);
   
				const uint startPc = 0x20000010u;
				Bus.WriteHalfWord(startPc, opcode);

				Cpu.Registers.PC = startPc;
				Cpu.Registers.Z = true; 

				// Act
				Cpu.Step();

				// Assert
				Cpu.Registers.PC.Should().Be(startPc + 2);
			}
		}
	}

	public class Bx : CpuTestBase
	{
		[Fact]
		public void Should_BranchToAddress_StoredInRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bx (LR);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers.LR = 0x10000200;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x10000200);
		}
	}
}
