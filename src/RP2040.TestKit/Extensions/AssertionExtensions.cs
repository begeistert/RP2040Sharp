using RP2040.Core.Cpu;
using RP2040.Peripherals.Gpio;
using RP2040.TestKit.Assertions;
using RP2040.TestKit.Probes;

namespace RP2040.TestKit.Extensions;

/// <summary>
/// <c>.Should()</c> extension methods for RP2040 simulation types.
/// </summary>
public static class AssertionExtensions
{
    public static CortexM0Assertions Should(this CortexM0Plus cpu) => new(cpu);

    public static UartProbeAssertions Should(this UartProbe probe) => new(probe);

    public static GpioAssertions Should(this GpioPin pin) => new(pin);
}
