using FluentAssertions;
using RP2040.TestKit;
using RP2040.TestKit.Extensions;

namespace RP2040.TestKit.Tests;

/// <summary>
/// Tests for the CI-oriented TestKit guards: bounded RunUntilHalt with a diagnostic
/// outcome, CPU-health assertions, and the deterministic instruction-count metric.
/// </summary>
public class CiGuardsTests
{
    // Thumb encodings used to build tiny in-memory programs.
    private const ushort BranchSelf = 0xE7FE; // `B .`   — a tight infinite loop
    private const ushort Undefined  = 0xDE00; // `UDF #0` — raises HardFault (ARMv6-M B1.5.6)
    private const uint SramBase = 0x2000_0000;

    private static RP2040TestSimulation NewSimLoopingAt(uint pc)
    {
        var sim = RP2040TestSimulation.Create();
        sim.Rp2040.Bus.WriteHalfWord(pc, BranchSelf);
        sim.Cpu.Registers.PC = pc;
        return sim;
    }

    [Fact]
    public void RunUntilHalt_PredicateMet_ReportsSuccess()
    {
        using var sim = NewSimLoopingAt(SramBase);

        var result = sim.RunUntilHalt(() => sim.InstructionCount >= 50, maxInstructions: 1_000_000);

        result.Outcome.Should().Be(RunOutcome.PredicateMet);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void RunUntilHalt_BudgetReached_WhenPredicateNeverTrue()
    {
        using var sim = NewSimLoopingAt(SramBase);

        var result = sim.RunUntilHalt(() => false, maxInstructions: 5_000);

        result.Outcome.Should().Be(RunOutcome.BudgetReached);
        result.Succeeded.Should().BeFalse();
        result.InstructionsExecuted.Should().BeGreaterThanOrEqualTo(5_000);
    }

    [Fact]
    public void RunUntilHalt_Terminates_EvenOnInfiniteLoop()
    {
        using var sim = NewSimLoopingAt(SramBase);

        // The whole point: a wedged firmware must not hang the harness.
        var act = () => sim.RunUntilHalt(() => false, maxInstructions: 10_000);

        act.Should().NotThrow();
    }

    [Fact]
    public void RunUntilHalt_LockedUp_OnDoubleFault()
    {
        var sim = LockedUpSim();

        var result = sim.RunUntilHalt(() => false, maxInstructions: 1_000_000);

        result.Outcome.Should().Be(RunOutcome.LockedUp);
        sim.Dispose();
    }

    [Fact]
    public void HealthyCpu_PassesHealthAssertions()
    {
        using var sim = NewSimLoopingAt(SramBase);
        sim.RunUntilHalt(() => sim.InstructionCount >= 100, maxInstructions: 1_000_000);

        sim.Cpu.Should().NotBeLockedUp();
        sim.Cpu.Should().NotHaveFaulted();
        sim.Cpu.Should().BeInThreadMode();
    }

    [Fact]
    public void NotBeLockedUp_Fails_OnLockedUpCpu()
    {
        var sim = LockedUpSim();
        sim.RunUntilHalt(() => false, maxInstructions: 100_000);

        var act = () => sim.Cpu.Should().NotBeLockedUp();

        act.Should().Throw<Exception>();
        sim.Dispose();
    }

    [Fact]
    public void InstructionCount_Advances_AndIsBudgetable()
    {
        using var sim = NewSimLoopingAt(SramBase);
        sim.InstructionCount.Should().Be(0);

        sim.RunUntilHalt(() => sim.InstructionCount >= 200, maxInstructions: 1_000_000);

        sim.InstructionCount.Should().BeGreaterThan(0);
        sim.Cpu.Should().HaveExecutedAtMost(sim.InstructionCount);
    }

    [Fact]
    public void HaveExecutedAtMost_Fails_WhenBudgetExceeded()
    {
        using var sim = NewSimLoopingAt(SramBase);
        sim.RunUntilHalt(() => sim.InstructionCount >= 1_000, maxInstructions: 1_000_000);

        var act = () => sim.Cpu.Should().HaveExecutedAtMost(10);

        act.Should().Throw<Exception>();
    }

    /// <summary>
    /// Build a sim wired to lock up: an UDF instruction faults, and the HardFault handler is
    /// itself an UDF, so the fault inside the handler escalates to lockup (ARMv6-M §B1.5.13).
    /// </summary>
    private static RP2040TestSimulation LockedUpSim()
    {
        var sim = RP2040TestSimulation.Create();
        var vtor    = SramBase + 0x1000;
        var handler = SramBase + 0x2000;
        sim.Cpu.Registers.SP   = SramBase + 0x4000;             // valid stack for exception framing
        sim.Cpu.Registers.VTOR = vtor;
        sim.Rp2040.Bus.WriteWord(vtor + 0x0C, handler | 1);     // HardFault vector → handler (Thumb bit)
        sim.Rp2040.Bus.WriteHalfWord(handler, Undefined);       // handler faults again → lockup
        sim.Rp2040.Bus.WriteHalfWord(SramBase, Undefined);      // first fault
        sim.Cpu.Registers.PC = SramBase;
        return sim;
    }
}
