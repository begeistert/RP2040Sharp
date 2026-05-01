using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals;

namespace RP2040.Peripherals.Tests.Fixtures;

/// <summary>
/// Base fixture that sets up an RP2040Machine for peripheral integration tests.
/// </summary>
public abstract class MachineTestBase : IDisposable
{
    protected readonly RP2040Machine Machine;

    protected MachineTestBase()
    {
        Machine = new RP2040Machine();
    }

    public void Dispose()
    {
        Machine.Dispose();
        GC.SuppressFinalize(this);
    }
}
