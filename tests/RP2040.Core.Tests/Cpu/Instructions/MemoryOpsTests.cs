using FluentAssertions;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
using RP2040.tests.Fixtures;
namespace RP2040.tests.Cpu.Instructions;

public abstract class MemoryOpsTests
{
	public class Pop : CpuTestBase
	{
		const uint STACK_BASE = 0x20004000;

		public Pop ()
		{
			Cpu.Registers.SP = STACK_BASE;
		}

		[Fact]
		public void Should_PopStandardRegisters_And_UpdateStackPointer ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (false, (1 << R0) | (1 << R1) | (1 << R2));
			Bus.WriteHalfWord (0x20000000, opcode);

			Bus.WriteWord (STACK_BASE, 0x10101010);
			Bus.WriteWord (STACK_BASE + 4, 0x20202020);
			Bus.WriteWord (STACK_BASE + 8, 0x30303030);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[SP].Should ().Be (STACK_BASE + 12);
			Cpu.Registers[R0].Should ().Be (0x10101010);
			Cpu.Registers[R1].Should ().Be (0x20202020);
			Cpu.Registers[R2].Should ().Be (0x30303030);
		}

		[Fact]
		public void Should_PopRegistersAndProgramCounter_HandlingThumbBit ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (true, 1 << R4);
			Bus.WriteHalfWord (0x20000000, opcode);

			Bus.WriteWord (STACK_BASE, 0x44444444);
			const uint returnAddressRaw = 0x20000101; // Not aligned
			Bus.WriteWord (STACK_BASE + 4, returnAddressRaw);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[SP].Should ().Be (STACK_BASE + 8);
			Cpu.Registers[R4].Should ().Be (0x44444444);
			Cpu.Registers[PC].Should ().Be (0x20000100); // Aligned
		}

		[Fact]
		public void Should_PopRegisters_InAscendingIndexOrder ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (false, (1 << R7) | (1 << R0));
			Bus.WriteHalfWord (0x20000000, opcode);

			Bus.WriteWord (STACK_BASE, 0xAAAAAAAA);
			Bus.WriteWord (STACK_BASE + 4, 0xBBBBBBBB);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R0].Should ().Be (0xAAAAAAAA);
			Cpu.Registers[R7].Should ().Be (0xBBBBBBBB);
		}

		[Fact]
		public void Should_ConsumeExtraCycles_When_PoppingProgramCounter ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (true, 0);
			Bus.WriteHalfWord (0x20000000, opcode);

			var cyclesBefore = Cpu.Cycles;

			// Act
			Cpu.Step ();

			// Assert
			var cyclesTaken = Cpu.Cycles - cyclesBefore;
			cyclesTaken.Should ().BeGreaterThan (2);
		}

		[Fact]
		public void Should_HandleStackWrapAround_WithoutThrowing ()
		{
			// Arrange
			const uint nearlyEnd = BusInterconnect.SRAM_START_ADDRESS + BusInterconnect.MASK_SRAM - 2;
			Cpu.Registers.SP = nearlyEnd;

			var opcode = InstructionEmiter.Pop (false, 1 << R0);
			Bus.WriteHalfWord (0x20000000, opcode);

			// Act
			var act = () => Cpu.Step ();

			// Assert
			act.Should ().NotThrow ();
			Cpu.Registers[SP].Should ().Be (nearlyEnd + 4);
		}
	}

	public class Push : CpuTestBase
	{
		const uint STACK_INITIAL = 0x20001000;

		public Push ()
		{
			Cpu.Registers.SP = STACK_INITIAL;
		}

		[Fact]
		public void Should_PushSingleRegister_ToStack ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (false, 1 << R0);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0xCAFEBABE;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[SP].Should ().Be (STACK_INITIAL - 4);
			Bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0xCAFEBABE);
		}

		[Fact]
		public void Should_PushMultipleRegisters_InAscendingOrder ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (false, (1 << R1) | (1 << R2));
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R1] = 0x11111111;
			Cpu.Registers[R2] = 0x22222222;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[SP].Should ().Be (STACK_INITIAL - 8);
			Bus.ReadWord (STACK_INITIAL - 8).Should ().Be (0x11111111);
			Bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0x22222222);
		}

		[Fact]
		public void Should_PushLinkRegister_And_OtherRegisters ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (true, 1 << R3);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[R3] = 0x33333333;
			Cpu.Registers[LR] = 0xFFFFFFFF;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[SP].Should ().Be (STACK_INITIAL - 8);
			Bus.ReadWord (STACK_INITIAL - 8).Should ().Be (0x33333333);
			Bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0xFFFFFFFF);
		}

		[Fact]
		public void Should_PushOnlyLinkRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (true, 0);
			Bus.WriteHalfWord (0x20000000, opcode);

			Cpu.Registers[LR] = 0xABCDEF00;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[SP].Should ().Be (STACK_INITIAL - 4);
			Bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0xABCDEF00);
		}

		[Fact]
		public void Should_ConsumeCycles_ProportionalToRegisterCount ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (true, (1 << R0) | (1 << R1) | (1 << R2));
			Bus.WriteHalfWord (0x20000000, opcode);

			var initialCycles = Cpu.Cycles;

			// Act
			Cpu.Step ();

			// Assert
			(Cpu.Cycles - initialCycles).Should ().Be (5);
		}
	}

	public class Ldmia : CpuTestBase
	{
		[Fact]
		public void Should_LoadMultiple_And_WriteBackBaseAddress ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ldmia (R0, 1 << R1 | 1 << R2);
			Bus.WriteHalfWord (0x20000000, opcode);
			const uint baseAddr = 0x20000010;
			Cpu.Registers[R0] = baseAddr;

			Bus.WriteWord (baseAddr, 0xF00DF00D);
			Bus.WriteWord (baseAddr + 4, 0x4242);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000002);
			Cpu.Registers[R0].Should ().Be (baseAddr + 8);
			Cpu.Registers[R1].Should ().Be (0xF00DF00D);
			Cpu.Registers[R2].Should ().Be (0x4242);
		}

		[Fact]
		public void Should_LoadMultiple_WithoutWriteBack_When_BaseIsLoaded ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ldmia (R5, 1 << R5);
			Bus.WriteHalfWord (0x20000000, opcode);
			const uint baseAddr = 0x20000010;
			Cpu.Registers[R5] = baseAddr;
			Bus.WriteWord (baseAddr, 0xF00DF00D);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000002);
			Cpu.Registers[R5].Should ().Be (0xF00DF00D);
		}

		[Fact]
		public void Should_ConsumeCorrectCycles_ForMultipleLoad ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ldmia (R0, 1 << R1 | 1 << R2);
			Bus.WriteHalfWord (0x20000000, opcode);
			Cpu.Registers[R0] = 0x20000010;

			var initialCycles = Cpu.Cycles;

			// Act
			Cpu.Step ();

			// Assert
			(Cpu.Cycles - initialCycles).Should ().Be (3);
		}
	}
}
