using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
namespace RP2040.tests.Cpu.Instructions;

public class ArithmeticOpsTests
{
	const int R0 = 0;
	const int R1 = 1;
	const int R2 = 2;
	const int R3 = 3;
	const int R4 = 4;
	const int R5 = 5;
	const int R8 = 8;
	const int R12 = 12;
	
	const int IP = 12;
	const int SP = 13;
	const int PC = 15;
	
	public class Adcs
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Adcs ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus(_bus);
        
			// Configuración común opcional
			_cpu.Registers.PC = 0x20000000;
		}
		
		[Fact]
		public void ShouldExecute()
		{
			// Arrange
			var opcode = InstructionEmiter.Adcs (R5, R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R4] = 55;
			_cpu.Registers[R5] = 66;
			_cpu.Registers.C = true;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R5].Should ().Be (122u);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldSetNegativeAndOverflowFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.Adcs (R5, R4);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R4] = 0x7fffffff;
			_cpu.Registers[R5] = 0;
			_cpu.Registers.C = true;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R5].Should ().Be (0x80000000u);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldNotSetOverflowFlagWhenAddingZeroesWithCarry ()
		{
			// Arrange
			var opcode = InstructionEmiter.Adcs (R3, R2);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R2] = 0;
			_cpu.Registers[R3] = 0;
			_cpu.Registers.C = true;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R3].Should ().Be (1u);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldSetZeroCarryAndOverflowFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.Adcs (R0, R0);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R0] = 0x80000000;
			_cpu.Registers.C = false;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R0].Should ().Be (0);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeTrue ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeTrue ();
		}
	}

	public class Add
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Add ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus(_bus);
        
			_cpu.Registers.PC = 0x20000000;
		}
		
		[Fact]
		public void ShouldExecuteAddSp ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddSpImm7 (0x10);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers.SP = 0x10000040;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.SP.Should ().Be (0x10000050);
		}

		[Fact]
		public void ShouldExecuteAddSpPlusImmediate ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddSpImm8 (R1, 0x10);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers.SP = 0x54;
			_cpu.Registers[R1] = 0;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.SP.Should ().Be (0x54);
			_cpu.Registers[R1].Should ().Be (0x64);
		}

		[Fact]
		public void ShouldExecuteAddHighRegisters ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddHighRegisters (R1, IP);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R1] = 66;
			_cpu.Registers[R12] = 44;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R1].Should ().Be (110);
		}

		[Fact]
		public void ShouldExecuteAddHighRegistersWithoutUpdateTheFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddHighRegisters (R3, R12);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 0x00002000;
			_cpu.Registers[R12] = 0xffffe000;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R3].Should ().Be (0x00000000);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldExecuteAddHighRegistersWithSpWithoutUpdateTheFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddHighRegisters (SP, R8);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[SP] = 0x20030000;
			_cpu.Registers.Z = true;
			_cpu.Registers[R8] = 0x13;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[SP].Should ().Be (0x20030010);
			_cpu.Registers.Z.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldExecuteAddHighRegistersWithPc ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddHighRegisters (PC, R8);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R8] = 0x11;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[PC].Should ().Be (0x20000014);
		}
	}

	public class Adds
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Adds ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus(_bus);
        
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecuteImmediate3 ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddsImm3 (R1, R2, 3);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R2] = 2;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R1].Should ().Be (5);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldExecuteImmediate8 ()
		{
			
		}
	}
}
