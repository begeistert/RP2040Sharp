using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
namespace RP2040.tests.Cpu.Instructions;

public class FlowOpsTests
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
	const int PC = 15;
	
	public class Bl
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		public Bl()
		{
			_bus = new BusInterconnect();
			_cpu = new CortexM0Plus(_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecuteWithPositiveOffset ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bl (0x34);
			_bus.WriteWord (0x20000000, opcode);
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.PC.Should ().Be (0x20000038);
			_cpu.Registers.LR.Should ().Be (0x20000005);
			_cpu.Cycles.Should ().Be (4);
		}

		[Fact]
		public void ShouldExecuteWithNegativeOffset ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bl (-0x10);
			_bus.WriteWord (0x20000000, opcode);
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.PC.Should ().Be (0x20000004 - 0x10);
			_cpu.Registers.LR.Should ().Be (0x20000005);
			_cpu.Cycles.Should ().Be (4);
		}
		
		[Fact]
		public void ShouldExecuteWithBiggerNegativeOffset ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bl (-3242);
			_bus.WriteWord (0x20000000, opcode);
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.PC.Should ().Be (0x20000004 - 3242);
			_cpu.Registers.LR.Should ().Be (0x20000005);
			_cpu.Cycles.Should ().Be (4);
		}
	}
}
