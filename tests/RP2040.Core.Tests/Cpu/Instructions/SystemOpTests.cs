using FluentAssertions;
using RP2040.Core.Cpu;
using RP2040.Core.Helpers;
using RP2040.Core.Memory;
namespace RP2040.tests.Cpu.Instructions;

public class SystemOpTests
{
	private readonly CortexM0Plus _cpu;
	private readonly BusInterconnect _bus;

	public SystemOpTests()
	{
		_bus = new BusInterconnect();
		_cpu = new CortexM0Plus(_bus);
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
}
