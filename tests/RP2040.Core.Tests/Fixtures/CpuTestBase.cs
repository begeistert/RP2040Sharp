using RP2040.Core.Cpu;
using RP2040.Core.Memory;

namespace RP2040.tests.Fixtures;

public abstract class CpuTestBase : IDisposable
{
	protected readonly CortexM0Plus Cpu;
	protected readonly BusInterconnect Bus;
    
	protected const int R0 = 0;
	protected const int R1 = 1;
	protected const int R2 = 2;
	protected const int R3 = 3;
	protected const int R4 = 4;
	protected const int R5 = 5;
	protected const int R6 = 6;
	protected const int R7 = 7;
	protected const int R8 = 8;
	protected const int R9 = 9;
	protected const int R10 = 10;
	protected const int R11 = 11;
	protected const int R12 = 12;

	protected const int IP = 12;
	protected const int SP = 13;
	protected const int LR = 14;
	protected const int PC = 15;

	protected CpuTestBase()
	{
		Bus = new BusInterconnect();
		Cpu = new CortexM0Plus(Bus);
		Cpu.Registers.PC = 0x20000000;
	}

	public void Dispose ()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected void Dispose(bool disposing)
	{
		Bus.Dispose();
	}
}