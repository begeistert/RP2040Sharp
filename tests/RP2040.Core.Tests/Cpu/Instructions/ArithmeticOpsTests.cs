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
	const int R6 = 6;
	const int R7 = 7;
	const int R8 = 8;
	const int R9 = 9;
	const int R10 = 10;
	const int R11 = 11;
	const int R12 = 12;

	const int IP = 12;
	const int SP = 13;
	const int PC = 15;
	
	readonly CortexM0Plus _cpu;
	readonly BusInterconnect _bus;
	public ArithmeticOpsTests ()
	{
		_bus = new BusInterconnect ();
		_cpu = new CortexM0Plus(_bus);
        
		// Configuración común opcional
		_cpu.Registers.PC = 0x20000000;
	}
	
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
		
		[Fact]
		public void ShouldExecuteAddEncodingT2WithLowRegistersAndPreserveFlags()
		{
			// Arrange
			// Esto genera el opcode 0x4411 (Encoding T2) en lugar de 0x1811 (Encoding T1)
			var opcode = InstructionEmiter.AddHighRegisters(R1, R2); 
			_bus.WriteHalfWord(0x20000000, opcode);

			_cpu.Registers[R1] = 10;
			_cpu.Registers[R2] = 20;

			_cpu.Registers.N = true;
			_cpu.Registers.Z = true; 
			_cpu.Registers.C = true; 
			_cpu.Registers.V = true;

			// Act
			_cpu.Step();

			// Assert
			_cpu.Registers[R1].Should().Be(30); // 10 + 20

			// Verificamos que NO cambiaron, a pesar de que:
			// - El resultado (30) NO es negativo (N debería ser false si se actualizara)
			// - El resultado NO es cero (Z debería ser false si se actualizara)
			_cpu.Registers.N.Should().BeTrue();
			_cpu.Registers.Z.Should().BeTrue();
			_cpu.Registers.C.Should().BeTrue();
			_cpu.Registers.V.Should().BeTrue();
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
		public void ShouldExecuteImm3 ()
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
		public void ShouldExecuteImm8 ()
		{
			// Arrange
			var opcode = InstructionEmiter.AddsImm8 (R1, 1);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R1] = 0xffffffff;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R1].Should ().Be (0);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeTrue ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteRegister ()
		{
			// Test 1: ADDS R1, R2, R7 (2 + 27 = 29)
			// Requiere: InstructionEmiter.AddsReg(rd, rn, rm)
			var opcode = InstructionEmiter.AddsRegister (R1, R2, R7);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R2] = 2;
			_cpu.Registers[R7] = 27;
       
			_cpu.Step ();
       
			_cpu.Registers[R1].Should ().Be (29);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteRegisterWithSignedOverflow ()
		{
			// Test 2: ADDS R4, R4, R2 (Overflow Signed)
			var opcode = InstructionEmiter.AddsRegister (R4, R4, R2);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R2] = 0x74bc8000;
			_cpu.Registers[R4] = 0x43740000;
       
			_cpu.Step ();
       
			_cpu.Registers[R4].Should ().Be (0xb8308000);
			_cpu.Registers.N.Should ().BeTrue ();  // Resultado es negativo (bit 31=1)
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse (); // No hubo carry unsigned
			_cpu.Registers.V.Should ().BeTrue ();  // Overflow Signed (Pos + Pos = Neg)
		}

		[Fact]
		public void ShouldExecuteRegisterSelfAddWithCarryAndOverflow ()
		{
			// Test 3: ADDS R1, R1, R1 (Neg + Neg = Pos + Carry)
			var opcode = InstructionEmiter.AddsRegister (R1, R1, R1);
			_bus.WriteHalfWord (0x20000000, opcode);

			_cpu.Registers[R1] = 0xbf8d1424;
			_cpu.Registers.C = true; // Inyectamos C para probar que se SOBREESCRIBE, no que se usa.
       
			_cpu.Step ();
       
			_cpu.Registers[R1].Should ().Be (0x7f1a2848);
			_cpu.Registers.N.Should ().BeFalse (); // Positivo
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();  // Carry generado
			_cpu.Registers.V.Should ().BeTrue ();  // Overflow Signed
		}
	}

	public class Adr
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Adr ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus(_bus);
        
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecute ()
		{
			// Arrange
			var opcode = InstructionEmiter.Adr (R4, 0x50);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R4].Should ().Be (0x20000054);
		}
	}
	
	public class Cmn
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Cmn ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus(_bus);
        
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecute ()
		{
			// Arrange
			const uint negativeTwo = (uint)(-2 & 0xFFFFFFFF);
			var opcode = InstructionEmiter.Cmn (R7, R2);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R2] = 1;
			_cpu.Registers[R7] = negativeTwo;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R2].Should ().Be (1);
			_cpu.Registers[R7].Should ().Be (negativeTwo);
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
	}
	
	public class Cmp
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Cmp ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus(_bus);
        
			_cpu.Registers.PC = 0x20000000;
		}

		[Fact]
		public void ShouldExecuteImmp ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpImm  (R5, 66);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R5] = 60;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteImmpAndUpdateCarryFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpImm  (R0, 0);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R0] = 0x80010133;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpRegister  (R5, R0);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R0] = 56;
			_cpu.Registers[R5] = 60;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteRegisterAndNotSetFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpRegister  (R2, R0);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R0] = 0xb71b0000;
			_cpu.Registers[R2] = 0x00b71b00;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteRegisterAndSetNegativeAndOverflowFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpRegister  (R3, R7);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R3] = 0;
			_cpu.Registers[R7] = 0x80000000;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeTrue ();
		}
		
		[Fact]
		public void ShouldExecuteRegisterAndSetNegativeAndCarryFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpRegister  (R3, R7);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R3] = 0x80000000;
			_cpu.Registers[R7] = 0;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeFalse ();
		}

		[Fact]
		public void ShouldExecuteHighRegisterAndSetCarryFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpHighRegister  (R11, R3);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R3] = 0x00000008;
			_cpu.Registers[R11] = 0xffffffff;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteHighRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpHighRegister  (IP, R6);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R6] = 56;
			_cpu.Registers[R12] = 60;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteHighRegisterAndUpdateNegativeAndCarryFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpHighRegister  (R11, R3);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R3] = 0;
			_cpu.Registers[R11] = 0x80000000;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteHighRegisterAndUpdateNegativeAndOverflowFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.CmpHighRegister  (R11, R3);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R3] = 0x80000000;
			_cpu.Registers[R11] = 0;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
			_cpu.Registers.C.Should ().BeFalse ();
			_cpu.Registers.V.Should ().BeTrue ();
		}
	}

	public class Muls
	{
		readonly CortexM0Plus _cpu;
		readonly BusInterconnect _bus;
		public Muls ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);

			_cpu.Registers.PC = 0x20000000;
		}
		
		[Fact]
		public void ShouldExecute ()
		{
			// Arrange
			var opcode = InstructionEmiter.Muls (R0, R2);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R0] = 5;
			_cpu.Registers[R2] = 1000000;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R2].Should ().Be (5000000);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeFalse ();
		}
		
		[Fact]
		public void ShouldExecuteWith32BitNumber ()
		{
			// Arrange
			var opcode = InstructionEmiter.Muls (R0, R2);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R0] = 2654435769;
			_cpu.Registers[R2] = 340573321;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R2].Should ().Be (1);
		}
		
		[Fact]
		public void ShouldExecuteAndSetZeroFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.Muls (R0, R2);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R0] = 0;
			_cpu.Registers[R2] = 1000000;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R2].Should ().Be (0);
			_cpu.Registers.N.Should ().BeFalse ();
			_cpu.Registers.Z.Should ().BeTrue ();
		}
		
		[Fact]
		public void ShouldExecuteAndSetNegativeFlag ()
		{
			// Arrange
			var opcode = InstructionEmiter.Muls (R0, R2);
			_bus.WriteHalfWord (0x20000000, opcode);
			
			_cpu.Registers[R0] = (uint)(-1 & 0xFFFFFFFF);
			_cpu.Registers[R2] = 1000000;
			
			// Act
			_cpu.Step ();
			
			// Assert
			_cpu.Registers[R2].Should ().Be ((uint)(-1000000 & 0xFFFFFFFF));
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeFalse ();
		}
	}
	
	[Fact]
	public void Orrs ()
	{
		// Arrange
		var opcode = InstructionEmiter.Orrs (R5, R0);
		_bus.WriteHalfWord (0x20000000, opcode);
			
		_cpu.Registers[R5] = 0xf00f0000;
		_cpu.Registers[R0] = 0xf000ffff;
			
		// Act
		_cpu.Step ();
			
		// Assert
		_cpu.Registers[R5].Should ().Be (0xf00fffff);
		_cpu.Registers.N.Should ().BeTrue ();
		_cpu.Registers.Z.Should ().BeFalse ();
	}
}
