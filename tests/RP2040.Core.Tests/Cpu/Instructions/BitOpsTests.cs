using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
namespace RP2040.tests.Cpu.Instructions;

public class BitOpsTests
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
	
	public class Ands
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Ands ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus(_bus);
        
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecute ()
		{
			// Arrange
			var opcode = InstructionEmiter.Ands (R5, R0);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R5] = 0xffff0000;
			_cpu.Registers[R0] = 0xf00fffff;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R5].Should ().Be (0xf00f0000);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
		}
	}

	public class Asrs
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		public Asrs()
		{
			_bus = new BusInterconnect();
			_cpu = new CortexM0Plus(_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecuteAsrsImmediate5 ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsImm5(R3, R2, 31);
			_bus.WriteHalfWord(0x20000000, opcode);

			_cpu.Registers[R2] = 0x80000000;
			_cpu.Registers.C = true;

			// Act
			_cpu.Step();

			// Assert
			_cpu.Registers[R3].Should().Be(0xffffffff); // -1 (Sign extension)
			_cpu.Registers.PC.Should().Be(0x20000002);
        
			_cpu.Registers.N.Should().BeTrue();
			_cpu.Registers.Z.Should().BeFalse();
			_cpu.Registers.C.Should().BeFalse();
		}
		
		[Fact]
		public void ShouldExecuteAsrsImmediate5AndUpdateCarry ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsImm5(R3, R2, 0); 
			_bus.WriteHalfWord(0x20000000, opcode);

			_cpu.Registers[R2] = 0x80000000;
			_cpu.Registers.C = false;

			// Act
			_cpu.Step();

			// Assert
			_cpu.Registers[R3].Should().Be(0xffffffff);
			_cpu.Registers.PC.Should().Be(0x20000002);
			_cpu.Registers.N.Should().BeTrue();
			_cpu.Registers.Z.Should().BeFalse();
			_cpu.Registers.C.Should().BeTrue();
		}
	}
}
