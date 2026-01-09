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

	readonly CortexM0Plus _cpu;
	readonly BusInterconnect _bus;
	public BitOpsTests ()
	{
		_bus = new BusInterconnect ();
		_cpu = new CortexM0Plus (_bus);

		_cpu.Registers.PC = 0x20000000;
	}

	[Fact]
	public void Ands ()
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

	public class Asrs
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		public Asrs ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecuteAsrsImmediate5 ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsImm5 (R3, R2, 31);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R2] = 0x80000000;
			_cpu.Registers.C = true;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0xffffffff); // -1 (Sign extension)
			_cpu.Registers.PC.Should ().Be (0x20000002);

			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldExecuteAsrsImmediate5AndUpdateCarry ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsImm5 (R3, R2, 0);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R2] = 0x80000000;
			_cpu.Registers.C = false;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0xffffffff);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldExecuteAsrsRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsRegister (R3, R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 0x80000040;
			_cpu.Registers[R4] = 0xff500007;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0xff000000);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldExecuteAsrsRegisterWithCarry ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsRegister (R3, R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 0x40000040;
			_cpu.Registers[R4] = 50;
			_cpu.Registers.C = true;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeTrue ();
			_cpu.Registers.C.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldExecuteAsrsRegisterWithCarryAndUpdateZero ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsRegister (R3, R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 0x40000040;
			_cpu.Registers[R4] = 31;
			_cpu.Registers.C = true;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeTrue ();
			_cpu.Registers.C.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldExecuteAsrsRegisterWithCarryAndUpdateNegative ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsRegister (R3, R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 0x80000040;
			_cpu.Registers[R4] = 50;
			_cpu.Registers.C = true;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0xffffffff);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldExecuteAsrsRegisterWithShiftZeroAndCarryAndUpdateNegative ()
		{
			// Arrange
			var opcode = InstructionEmiter.AsrsRegister (R3, R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 0x80000040;
			_cpu.Registers[R4] = 0;
			_cpu.Registers.C = true;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0x80000040);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
		}
	}

	public class Bics
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		public Bics ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecute ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bics (R0, R3);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R0] = 0xff;
			_cpu.Registers[R3] = 0x0f;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R0].Should ().Be (0xf0);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldExecuteAndSetNegativeFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.Bics (R0, R3);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R0] = 0xffffffff;
			_cpu.Registers[R3] = 0x0000ffff;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R0].Should ().Be (0xffff0000);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
		}
	}

	[Fact]
	public void Eors ()
	{
		// Arrange
		var opcode = InstructionEmiter.Eors (R1, R3);
		_bus.WriteHalfWord (0x20000000, opcode);

		_cpu.Registers[R1] = 0xf0f0f0f0;
		_cpu.Registers[R3] = 0x08ff3007;

		// Act
		_cpu.Step ();

		// Assert
		_cpu.Registers[R1].Should ().Be (0xf80fc0f7);
		_cpu.Registers.N.Should ().BeTrue ();
		_cpu.Registers.Z.Should ().BeFalse ();
	}

	public class Mov
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		public Mov ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecute ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mov (R3, R8);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R8] = 55;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (55);
		}

		[Fact]
		public void ShouldExecuteWithPc ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mov (R3, PC);
			_bus.WriteHalfWord (0x20000000, opcode);

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0x20000004);
		}

		[Fact]
		public void ShouldExecuteWithSp ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mov (SP, R8);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R8] = 55;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[SP].Should ().Be (52);
		}

		[Fact]
		public void MOV_Should_Clear_Lower_2Bits_Of_SP()
		{
			// Arrange
			_cpu.Registers.PC = 0x20000000;
			var opcode = InstructionEmiter.Mov(SP, R5);
			_bus.WriteHalfWord(0x20000000, opcode);
			_cpu.Registers.R5 = 0x53;
			
			// Act
			_cpu.Step();
			
			// Assert
			_cpu.Registers.SP.Should().Be(0x50);
		}
		
		[Fact]
		public void MOV_Should_Clear_Lower_Bit_Of_PC()
		{
			_cpu.Registers.PC = 0x20000000;
			var opcode = InstructionEmiter.Mov(PC, R5);
			_bus.WriteHalfWord(0x20000000, opcode); // mov pc, r5
			_cpu.Registers.R5 = 0x53;
    
			_cpu.Step();
    
			_cpu.Registers.PC.Should().Be(0x52); 
		}
	}

	public class Movs
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		public Movs()
		{
			_bus = new BusInterconnect();
			_cpu = new CortexM0Plus(_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecute ()
		{
			// Arrange
			var opcode = InstructionEmiter.Movs (R5, 128);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			// Act
			_cpu.Step();
			
			// Assert
			_cpu.Registers[R5].Should().Be(128);
			_cpu.Registers.PC.Should().Be(0x20000002);
		}
	
	}

	[Fact]
	public void Mvns ()
	{
		// Arrange
		var opcode = InstructionEmiter.Mvns (R4, R3);
		_bus.WriteHalfWord (0x20000000, opcode);

		_cpu.Registers[R3] = 0x11115555;

		// Act
		_cpu.Step ();

		// Assert
		_cpu.Registers[R4].Should ().Be (0xeeeeaaaa);
		_cpu.Registers.N.Should ().BeTrue ();
		_cpu.Registers.Z.Should ().BeFalse ();
	}

	public class Lsls
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		public Lsls ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecuteImmSimple ()
		{
			// Arrange
			var opcode = InstructionEmiter.LslsImm5 (R5, R5, 18);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R5] = 0b00000000000000000011; // 0b11
			_cpu.Registers.C = false;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R5].Should ().Be (0b11000000000000000000);
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.PC.Should ().Be (0x20000002);
		}

		[Fact]
		public void ShouldExecuteRegisterBottomByteOnly ()
		{
			// Arrange
			var opcode = InstructionEmiter.LslsRegister (R5, R0);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R5] = 0b00000000000000000011;
			_cpu.Registers[R0] = 0xFF003302;
			_cpu.Registers.C = false;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R5].Should ().Be (0b00000000000000001100);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.C.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldExecuteRegisterSaturation32 ()
		{
			// Arrange
			var opcode = InstructionEmiter.LslsRegister (R3, R4);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R3] = 1;
			_cpu.Registers[R4] = 0x20;
			_cpu.Registers.C = false;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (0);
			_cpu.Registers.PC.Should ().Be (0x20000002);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeTrue (); // Result is 0
			_cpu.Registers.C.Should ().BeTrue (); // Bit 0 shifted out
		}

		[Fact]
		public void ShouldExecuteImmWithCarry ()
		{
			// Arrange
			var opcode = InstructionEmiter.LslsImm5 (R5, R5, 18);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R5] = 0x4001;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R5].Should ().Be (0x40000);
			_cpu.Registers.C.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldExecuteWithImmZeroDispatchAsMovs ()
		{
			// Arrange
			var opcode = InstructionEmiter.LslsImm5 (R5, R5, 0); // LSLS R5, R5, #0
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R5] = 0xFFFF;
			_cpu.Registers.C = true;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R5].Should ().Be (0xFFFF);
			_cpu.Registers.C.Should ().BeTrue ("LSL #0 should preserve Carry flag (MOVS behavior)");
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.N.Should ().BeFalse ();
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
