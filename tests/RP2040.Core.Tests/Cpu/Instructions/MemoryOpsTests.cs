using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
namespace RP2040.tests.Cpu.Instructions;

public class MemoryOpsTests
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
	const int R12 = 12;

	const int IP = 12;
	const int SP = 13;
	const int LR = 14;
	const int PC = 15;

	public class Pop
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;

		const uint STACK_BASE = 0x20004000;

		public Pop ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);

			_cpu.Registers.PC = 0x20000000;
			_cpu.Registers.SP = STACK_BASE;
		}

		[Fact]
		public void ShouldPopStandardRegisters ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (false, (1 << R0) | (1 << R1) | (1 << R2));
			_bus.WriteHalfWord (0x20000000, opcode);

			_bus.WriteWord (STACK_BASE, 0x10101010);
			_bus.WriteWord (STACK_BASE + 4, 0x20202020);
			_bus.WriteWord (STACK_BASE + 8, 0x30303030);

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[SP].Should ().Be (STACK_BASE + 12);
			_cpu.Registers[R0].Should ().Be (0x10101010);
			_cpu.Registers[R1].Should ().Be (0x20202020);
			_cpu.Registers[R2].Should ().Be (0x30303030);
		}

		[Fact]
		public void ShouldPopRegistersAndPc_HandlingThumbBit ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (true, 1 << R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_bus.WriteWord (STACK_BASE, 0x44444444);
			const uint returnAddressRaw = 0x20000101; // Not aligned
			_bus.WriteWord (STACK_BASE + 4, returnAddressRaw);

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[SP].Should ().Be (STACK_BASE + 8);
			_cpu.Registers[R4].Should ().Be (0x44444444);
			_cpu.Registers[PC].Should ().Be (0x20000100); // Aligned
		}

		[Fact]
		public void ShouldPopInAscendingIndexOrder ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (false, (1 << R7) | (1 << R0));
			_bus.WriteHalfWord (0x20000000, opcode);

			_bus.WriteWord (STACK_BASE, 0xAAAAAAAA);
			_bus.WriteWord (STACK_BASE + 4, 0xBBBBBBBB);

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R0].Should ().Be (0xAAAAAAAA);
			_cpu.Registers[R7].Should ().Be (0xBBBBBBBB);
		}

		[Fact]
		public void ShouldConsumeExtraCyclesForPcPop ()
		{
			// Arrange
			var opcode = InstructionEmiter.Pop (true, 0);
			_bus.WriteHalfWord (0x20000000, opcode);

			var cyclesBefore = _cpu.Cycles;

			// Act
			_cpu.Step ();

			// Assert
			var cyclesTaken = _cpu.Cycles - cyclesBefore;
			cyclesTaken.Should ().BeGreaterThan (2);
		}

		[Fact]
		public void ShouldHandleStackWrapAround_IfTestingBounds ()
		{
			// Arrange
			const uint nearlyEnd = BusInterconnect.SRAM_START_ADDRESS + BusInterconnect.MASK_SRAM - 2;
			_cpu.Registers.SP = nearlyEnd;

			var opcode = InstructionEmiter.Pop (false, 1 << R0);
			_bus.WriteHalfWord (0x20000000, opcode);

			// Act
			var act = () => _cpu.Step ();

			// Assert
			act.Should ().NotThrow ();
			_cpu.Registers[SP].Should ().Be (nearlyEnd + 4);
		}
	}

	public class Push
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;

		const uint STACK_INITIAL = 0x20001000;

		public Push ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);

			_cpu.Registers.PC = 0x20000000;
			_cpu.Registers.SP = STACK_INITIAL;
		}

		[Fact]
		public void ShouldPushSingleRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (false, 1 << R0);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R0] = 0xCAFEBABE;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[SP].Should ().Be (STACK_INITIAL - 4);
			_bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0xCAFEBABE);
		}

		[Fact]
		public void ShouldPushMultipleRegistersInAscendingOrder ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (false, (1 << R1) | (1 << R2));
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R1] = 0x11111111;
			_cpu.Registers[R2] = 0x22222222;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[SP].Should ().Be (STACK_INITIAL - 8);
			_bus.ReadWord (STACK_INITIAL - 8).Should ().Be (0x11111111);
			_bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0x22222222);
		}

		[Fact]
		public void ShouldPushLrAndRegisters ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (true, 1 << R3);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 0x33333333;
			_cpu.Registers[LR] = 0xFFFFFFFF;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[SP].Should ().Be (STACK_INITIAL - 8);
			_bus.ReadWord (STACK_INITIAL - 8).Should ().Be (0x33333333);
			_bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0xFFFFFFFF);
		}

		[Fact]
		public void ShouldPushOnlyLr ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (true, 0);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[LR] = 0xABCDEF00;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[SP].Should ().Be (STACK_INITIAL - 4);
			_bus.ReadWord (STACK_INITIAL - 4).Should ().Be (0xABCDEF00);
		}

		[Fact]
		public void ShouldConsumeCyclesProportionalToRegisterCount ()
		{
			// Arrange
			var opcode = InstructionEmiter.Push (true, (1 << R0) | (1 << R1) | (1 << R2));
			_bus.WriteHalfWord (0x20000000, opcode);

			var initialCycles = _cpu.Cycles;

			// Act
			_cpu.Step ();

			// Assert
			(_cpu.Cycles - initialCycles).Should ().Be (5);
		}
	}

	public class Ldmia
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		private const uint CODE_BASE = 0x20000000;

		public Ldmia ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);

			_cpu.Registers.PC = CODE_BASE;
		}

		[Fact]
		public void ShouldExecuteLdmiaWithWriteBack ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ldmia (R0, 1 << R1 | 1 << R2);
			_bus.WriteHalfWord (CODE_BASE, opcode);
			const uint baseAddr = 0x20000010;
			_cpu.Registers[R0] = baseAddr;

			_bus.WriteWord (baseAddr, 0xF00DF00D);
			_bus.WriteWord (baseAddr + 4, 0x4242);

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers.PC.Should ().Be (CODE_BASE + 2);
			_cpu.Registers[R0].Should ().Be (baseAddr + 8);
			_cpu.Registers[R1].Should ().Be (0xF00DF00D);
			_cpu.Registers[R2].Should ().Be (0x4242);
		}

		[Fact]
		public void ShouldExecuteLdmiaWithoutWriteBackIfBaseInList ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ldmia (R5, 1 << R5);
			_bus.WriteHalfWord (CODE_BASE, opcode);
			const uint baseAddr = 0x20000010;
			_cpu.Registers[R5] = baseAddr;
			_bus.WriteWord (baseAddr, 0xF00DF00D);

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers.PC.Should ().Be (CODE_BASE + 2);
			_cpu.Registers[R5].Should ().Be (0xF00DF00D);
		}

		[Fact]
		public void ShouldConsumeCorrectCycles ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ldmia (R0, 1 << R1 | 1 << R2);
			_bus.WriteHalfWord (CODE_BASE, opcode);
			_cpu.Registers[R0] = 0x20000010;

			var initialCycles = _cpu.Cycles;

			// Act
			_cpu.Step ();

			// Assert
			(_cpu.Cycles - initialCycles).Should ().Be (3);
		}
	}
}
