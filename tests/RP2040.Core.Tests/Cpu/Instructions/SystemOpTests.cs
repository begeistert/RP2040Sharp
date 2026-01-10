using FluentAssertions;
using RP2040.Core.Helpers;
using RP2040.tests.Fixtures;
namespace RP2040.tests.Cpu.Instructions;

public abstract class SystemOpTests
{
	public class Dmb : CpuTestBase
	{
		[Fact]
		public void Should_ExecuteDataMemoryBarrier ()
		{
			// Arrange
			var opcode = InstructionEmiter.Dmb;
			Bus.WriteWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000004);
		}
	}

	public class Dsb : CpuTestBase
	{
		[Fact]
		public void Should_ExecuteDataSynchronizationBarrier ()
		{
			// Arrange
			var opcode = InstructionEmiter.Dsb;
			Bus.WriteWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000004);
		}
	}

	public class Nop : CpuTestBase
	{
		[Fact]
		public void Should_ExecuteNoOperation ()
		{
			// Arrange
			var opcode = InstructionEmiter.Nop;
			Bus.WriteWord (0x20000000, opcode);

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.PC.Should ().Be (0x20000002);
		}
	}

	public class Mrs : CpuTestBase
	{
		private const int SYSM_APSR = 0;
		private const int SYSM_IPSR = 5;
		private const int SYSM_MSP = 8;
		private const int SYSM_PSP = 9;
		private const int SYSM_PRIMASK = 16;
		private const int SYSM_CONTROL = 20;

		public Mrs ()
		{
			Cpu.Registers.CONTROL = 0;
			Cpu.Registers.IPSR = 0;
		}

		[Fact]
		public void Should_ReadIpsr_And_MaskReservedBits ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R0, SYSM_IPSR);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers[R0] = 55; // Dirty value
			Cpu.Registers.IPSR = 0; // Thread Mode

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R0].Should ().Be (0, "IPSR should be 0 in Thread Mode");
			Cpu.Registers.PC.Should ().Be (0x20000004);
		}

		[Fact]
		public void Should_ReadApsr_WithCurrentFlags ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R1, SYSM_APSR);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers.N = true;
			Cpu.Registers.Z = true;
			Cpu.Registers.C = true;
			Cpu.Registers.V = true;

			const uint expectedApsr = 0xF0000000; // N=1, Z=1, C=1, V=1, others 0

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R1].Should ().Be (expectedApsr);
		}

		[Fact]
		public void Should_ReadMainStackPointer_WhenActive ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R2, SYSM_MSP);
			Bus.WriteWord (0x20000000, opcode);

			const uint stackValue = 0x20004000;
			Cpu.Registers.SP = stackValue;
			Cpu.Registers[R2] = 0;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R2].Should ().Be (stackValue, "MRS MSP should read current SP when using MSP");
		}

		[Fact]
		public void Should_ReadProcessStackPointer_FromStorage_WhenUsingMsp ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R3, SYSM_PSP);
			Bus.WriteWord (0x20000000, opcode);

			const uint stackValue = 0x20008000;
			Cpu.Registers.PSP_Storage = stackValue;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R3].Should ().Be (stackValue, "MRS PSP should read Storage when using MSP");
		}

		[Fact]
		public void Should_ReadPrimask ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R4, SYSM_PRIMASK);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers.PRIMASK = 1; // disable interrupts

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R4].Should ().Be (1);
		}

		[Fact]
		public void Should_ReadControlRegister ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R5, SYSM_CONTROL);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers.CONTROL = 2; // SPSEL=1

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R5].Should ().Be (2);
		}

		[Fact]
		public void Should_MaskApsrFlags_WhenReadingIpsr ()
		{
			// Arrange
			var opcode = InstructionEmiter.Mrs (R0, SYSM_IPSR);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers.N = true; // Dirty flag
			Cpu.Registers.IPSR = 0;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers[R0].Should ().Be (0, "MRS IPSR mask should exclude APSR flags (Bit 31)");
		}
	}

	public class Msr : CpuTestBase
	{
		private const int SYSM_APSR = 0;
		private const int SYSM_IPSR = 5;
		private const int SYSM_MSP = 8;
		private const int SYSM_PSP = 9;
		private const int SYSM_PRIMASK = 16;
		private const int SYSM_CONTROL = 20;

		public Msr ()
		{
			Cpu.Registers.CONTROL = 0;
			Cpu.Registers.IPSR = 0;
		}

		[Fact]
		public void Should_UpdateMainStackPointer_WhenActive ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_MSP, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0x1234;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.SP.Should ().Be (0x1234);
			Cpu.Registers.PC.Should ().Be (0x20000004);
		}

		[Fact]
		public void Should_AlignStackPointer_To4Bytes ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_MSP, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0x1233;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.SP.Should ().Be (0x1230, "Should align value to 4 bytes");
		}

		[Fact]
		public void Should_UpdateProcessStackPointer_Storage_WhenInactive ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_PSP, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0x5678;
			var currentSp = Cpu.Registers.SP; // MSP actual

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.SP.Should ().Be (currentSp, "Active SP (MSP) should not change");
			Cpu.Registers.PSP_Storage.Should ().Be (0x5678, "PSP Storage should be updated");
		}

		[Fact]
		public void Should_SwitchToProcessStack_WhenSettingSpsel ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_CONTROL, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers.SP = 0xAAAA0000;
			Cpu.Registers.PSP_Storage = 0xBBBB0000;
			Cpu.Registers[R0] = 2; // Bit 1 = SPSEL=1 (Switch to PSP)

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.CONTROL.Should ().Be (2);
			Cpu.Registers.SP.Should ().Be (0xBBBB0000, "SP should execute hot-swap to PSP value");
			Cpu.Registers.MSP_Storage.Should ().Be (0xAAAA0000, "Old SP should be saved to MSP Storage");
		}

		[Fact]
		public void Should_NotSwitchStacks_WhenInHandlerMode ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_CONTROL, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers.IPSR = 1; // Handler Mode!
			Cpu.Registers.SP = 0xAAAA0000;
			Cpu.Registers.PSP_Storage = 0xBBBB0000;
			Cpu.Registers.CONTROL = 0;

			Cpu.Registers[R0] = 2; // Try set SPSEL=1

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.CONTROL.Should ().Be (0, "Register value updates");
			Cpu.Registers.SP.Should ().Be (0xAAAA0000, "Physical SP MUST NOT change in Handler Mode");
		}

		[Fact]
		public void Should_UpdateFlags_WhenWritingToApsr ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_APSR, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0xF0000000; // Set N, Z, C, V

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.N.Should ().BeTrue ();
			Cpu.Registers.Z.Should ().BeTrue ();
			Cpu.Registers.C.Should ().BeTrue ();
			Cpu.Registers.V.Should ().BeTrue ();
		}

		[Fact]
		public void Should_IgnoreWrites_ToReadOnlyIpsr ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_IPSR, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers[R0] = 0xF0000000;
			Cpu.Registers.N = false;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.N.Should ().BeFalse ("Writing to IPSR should ignore flags");
			Cpu.Registers.IPSR.Should ().Be (0, "IPSR is read-only");
		}

		[Fact]
		public void Should_IgnorePrivilegedWrites_WhenInUnprivilegedMode ()
		{
			// Arrange
			var opcode = InstructionEmiter.Msr (SYSM_MSP, R0);
			Bus.WriteWord (0x20000000, opcode);

			Cpu.Registers.CONTROL = 1; // nPRIV = 1 (Unprivileged)
			Cpu.Registers[R0] = 0xDEADBEEF;
			var originalSp = Cpu.Registers.SP;

			// Act
			Cpu.Step ();

			// Assert
			Cpu.Registers.SP.Should ().Be (originalSp, "Unprivileged write to MSP should be ignored");
		}
	}
}
