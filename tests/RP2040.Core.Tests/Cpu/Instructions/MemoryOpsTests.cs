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
	const int PC = 15;
	
	readonly CortexM0Plus _cpu;
	readonly BusInterconnect _bus;
	public MemoryOpsTests ()
	{
		_bus = new BusInterconnect ();
		_cpu = new CortexM0Plus(_bus);
        
		_cpu.Registers.PC = 0x20000000;
	}

	[Fact]
	public void Pop ()
	{
		// Arrange
		var opcode = InstructionEmiter.Pop (true, (1 << R4) | (1 << R5) | (1 << R6));
		_bus.WriteHalfWord (0x20000000, opcode);
		
		_cpu.Registers[SP] = BusInterconnect.SRAM_START_ADDRESS + 0xf0;
		_bus.WriteWord (0x200000f0, 0x40);
		_bus.WriteWord (0x200000f4, 0x50);
		_bus.WriteWord (0x200000f8, 0x60);
		_bus.WriteWord (0x200000fc, 0x42);
		
		// Act
		_cpu.Step ();
		
		// Assert
		_cpu.Registers[SP].Should ().Be (BusInterconnect.SRAM_START_ADDRESS + 0x100);
		_cpu.Registers[R4].Should ().Be (0x40);
		_cpu.Registers[R5].Should ().Be (0x50);
		_cpu.Registers[R6].Should ().Be (0x60);
		_cpu.Registers[PC].Should ().Be (0x42);
	}
}
