using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
namespace RP2040.tests.Cpu.Instructions;

public class SystemOpTests
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

	private readonly CortexM0Plus _cpu;
	private readonly BusInterconnect _bus;

	public SystemOpTests ()
	{
		_bus = new BusInterconnect ();
		_cpu = new CortexM0Plus (_bus);
		_cpu.Registers.PC = 0x20000000;
	}

	[Fact]
	public void Bmb ()
	{
		// Arrange
		var opcode = InstructionEmiter.Dmb ();
		_bus.WriteWord (0x20000000, opcode);

		// Act
		_cpu.Step ();

		// Assert
		_cpu.Registers.PC.Should ().Be (0x20000004);
	}

	[Fact]
	public void Bsb ()
	{
		// Arrange
		var opcode = InstructionEmiter.Dsb ();
		_bus.WriteWord (0x20000000, opcode);

		// Act
		_cpu.Step ();

		// Assert
		_cpu.Registers.PC.Should ().Be (0x20000004);
	}

	[Fact]
	public void Nop ()
	{
		// Arrange
		var opcode = InstructionEmiter.Nop ();
		_bus.WriteWord (0x20000000, opcode);

		// Act
		_cpu.Step ();

		// Assert
		_cpu.Registers.PC.Should ().Be (0x20000002);
	}

	public class Mrs
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		private const int SYSM_APSR = 0;
		private const int SYSM_IPSR = 5;
		private const int SYSM_MSP = 8;
		private const int SYSM_PSP = 9;
		private const int SYSM_PRIMASK = 16;
		private const int SYSM_CONTROL = 20;

		public Mrs ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);
			_cpu.Registers.PC = 0x20000000;
			_cpu.Registers.CONTROL = 0;
			_cpu.Registers.IPSR = 0;
		}

		[Fact]
		public void ShouldExecuteMrsIpsr ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R0, SYSM_IPSR);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers[R0] = 55; // Dirty value
			_cpu.Registers.IPSR = 0; // Thread Mode

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R0].Should ().Be (0, "IPSR should be 0 in Thread Mode");
			_cpu.Registers.PC.Should ().Be (0x20000004);
		}

		[Fact]
		public void ShouldReadApsrFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R1, SYSM_APSR);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers.N = true;
			_cpu.Registers.Z = true;
			_cpu.Registers.C = true;
			_cpu.Registers.V = true;

			const uint expectedApsr = 0xF0000000; // N=1, Z=1, C=1, V=1, others 0

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R1].Should ().Be (expectedApsr);
		}

		[Fact]
		public void ShouldReadMsp ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R2, SYSM_MSP);
			_bus.WriteWord (0x20000000, opcode);

			const uint stackValue = 0x20004000;
			_cpu.Registers.SP = stackValue;
			_cpu.Registers[R2] = 0;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R2].Should ().Be (stackValue, "MRS MSP should read current SP when using MSP");
		}

		[Fact]
		public void ShouldReadPsp ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R3, SYSM_PSP);
			_bus.WriteWord (0x20000000, opcode);

			const uint stackValue = 0x20008000;
			_cpu.Registers.PSP_Storage = stackValue;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R3].Should ().Be (stackValue, "MRS PSP should read Storage when using MSP");
		}

		[Fact]
		public void ShouldReadPrimask ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R4, SYSM_PRIMASK);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers.PRIMASK = 1; // disable interrupts

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R4].Should ().Be (1);
		}

		[Fact]
		public void ShouldReadControl ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R5, SYSM_CONTROL);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers.CONTROL = 2; // SPSEL=1

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R5].Should ().Be (2);
		}

		[Fact]
		public void ShouldIsolateRegisters ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R0, SYSM_IPSR);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers.N = true; // Dirty flag
			_cpu.Registers.IPSR = 0;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers[R0].Should ().Be (0, "MRS IPSR mask should exclude APSR flags (Bit 31)");
		}
	}

	public class Msr
	{
		private readonly CortexM0Plus _cpu;
		private readonly BusInterconnect _bus;

		private const int SYSM_APSR = 0;
		private const int SYSM_IPSR = 5;
		private const int SYSM_MSP = 8;
		private const int SYSM_PSP = 9;
		private const int SYSM_PRIMASK = 16;
		private const int SYSM_CONTROL = 20;

		public Msr ()
		{
			_bus = new BusInterconnect ();
			_cpu = new CortexM0Plus (_bus);
			_cpu.Registers.PC = 0x20000000;
			_cpu.Registers.CONTROL = 0;
			_cpu.Registers.IPSR = 0;
		}
		
		[Fact]
		public void ShouldWriteMsp_WhenActive()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr(SYSM_MSP, R0); 
			_bus.WriteWord(0x20000000, opcode);

			_cpu.Registers[R0] = 0x1234;

			// Act
			_cpu.Step();

			// Assert
			_cpu.Registers.SP.Should().Be(0x1230); // 0x1234 aligned to 4 bytes
			_cpu.Registers.PC.Should().Be(0x20000004);
		}

		[Fact]
		public void ShouldWritePsp_WhenInactive()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr(SYSM_PSP, R0);
			_bus.WriteWord(0x20000000, opcode);

			_cpu.Registers[R0] = 0x5678;
			var currentSp = _cpu.Registers.SP; // MSP actual

			// Act
			_cpu.Step();

			// Assert
			_cpu.Registers.SP.Should().Be(currentSp, "Active SP (MSP) should not change");
			_cpu.Registers.PSP_Storage.Should().Be(0x5670, "PSP Storage should be updated");
		}

		[Fact]
		public void ShouldSwitchStack_WhenWritingControl ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_CONTROL, R0);
			_bus.WriteWord (0x20000000, opcode);
			
			_cpu.Registers.SP = 0xAAAA0000; 
			_cpu.Registers.PSP_Storage = 0xBBBB0000;
			_cpu.Registers[R0] = 2; // Bit 1 = SPSEL=1 (Switch to PSP)

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers.CONTROL.Should ().Be (2);
			_cpu.Registers.SP.Should ().Be (0xBBBB0000, "SP should execute hot-swap to PSP value");
			_cpu.Registers.MSP_Storage.Should ().Be (0xAAAA0000, "Old SP should be saved to MSP Storage");
		}

		[Fact]
		public void ShouldNotSwitchStack_IfInHandlerMode()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr(SYSM_CONTROL, R0);
			_bus.WriteWord(0x20000000, opcode);

			_cpu.Registers.IPSR = 1; // Handler Mode!
			_cpu.Registers.SP = 0xAAAA0000;
			_cpu.Registers.PSP_Storage = 0xBBBB0000;
			_cpu.Registers.CONTROL = 0;

			_cpu.Registers[R0] = 2; // Try set SPSEL=1

			// Act
			_cpu.Step();

			// Assert
			_cpu.Registers.CONTROL.Should().Be(0, "Register value updates");
			_cpu.Registers.SP.Should().Be(0xAAAA0000, "Physical SP MUST NOT change in Handler Mode");
		}

		[Fact]
		public void ShouldWriteFlags_Apsr ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_APSR, R0);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers[R0] = 0xF0000000; // Set N, Z, C, V

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers.N.Should ().BeTrue ();
			_cpu.Registers.Z.Should ().BeTrue ();
			_cpu.Registers.C.Should ().BeTrue ();
			_cpu.Registers.V.Should ().BeTrue ();
		}

		[Fact]
		public void ShouldNotCorruptFlags_WhenWritingIpsr ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_IPSR, R0);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers[R0] = 0xF0000000;
			_cpu.Registers.N = false;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers.N.Should ().BeFalse ("Writing to IPSR should ignore flags");
			_cpu.Registers.IPSR.Should ().Be (0, "IPSR is read-only");
		}

		[Fact]
		public void ShouldIgnorePrivilegedWrites_WhenUnprivileged ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_MSP, R0);
			_bus.WriteWord (0x20000000, opcode);

			_cpu.Registers.CONTROL = 1; // nPRIV = 1 (Unprivileged)
			_cpu.Registers[R0] = 0xDEADBEEF;
			var originalSp = _cpu.Registers.SP;

			// Act
			_cpu.Step ();

			// Assert
			_cpu.Registers.SP.Should ().Be (originalSp, "Unprivileged write to MSP should be ignored");
		}
	}
}
