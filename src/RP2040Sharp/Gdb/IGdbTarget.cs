using RP2040.Peripherals;

namespace RP2040.Gdb;

/// <summary>
/// The execution target a <see cref="GdbServer"/> drives. Ported from rp2040js
/// (src/gdb/gdb-target.ts). GDB debugs Core 0 (<see cref="RP2040Machine.Cpu"/>).
/// </summary>
public interface IGdbTarget
{
    /// <summary>The machine being debugged.</summary>
    RP2040Machine Machine { get; }

    /// <summary>True while the target is freely running (between <c>continue</c> and a stop).</summary>
    bool Executing { get; }

    /// <summary>Start free-running execution (GDB <c>continue</c>/<c>vCont;c</c>).</summary>
    void Execute();

    /// <summary>Halt free-running execution (GDB interrupt or breakpoint hit).</summary>
    void Stop();
}
