using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using RP2040.Core.Cpu;

namespace RP2040.TestKit.Assertions;

/// <summary>FluentAssertions extension for <see cref="CortexM0Plus"/>.</summary>
public sealed class CortexM0Assertions : ReferenceTypeAssertions<CortexM0Plus, CortexM0Assertions>
{
    public CortexM0Assertions(CortexM0Plus subject) : base(subject) { }

    protected override string Identifier => "cpu";

    public AndConstraint<CortexM0Assertions> HaveRegister(int index, uint expected,
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Registers[index] == expected)
            .FailWith("Expected R{0} to be 0x{1:X8}{reason}, but found 0x{2:X8}.",
                index, expected, Subject.Registers[index]);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    public AndConstraint<CortexM0Assertions> HavePC(uint expected,
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Registers.PC == expected)
            .FailWith("Expected PC to be 0x{0:X8}{reason}, but found 0x{1:X8}.",
                expected, Subject.Registers.PC);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    public AndConstraint<CortexM0Assertions> HaveSP(uint expected,
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Registers.SP == expected)
            .FailWith("Expected SP to be 0x{0:X8}{reason}, but found 0x{1:X8}.",
                expected, Subject.Registers.SP);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    public AndConstraint<CortexM0Assertions> HaveCycles(long expected,
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Cycles == expected)
            .FailWith("Expected Cycles to be {0}{reason}, but found {1}.",
                expected, Subject.Cycles);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    public AndConstraint<CortexM0Assertions> HaveZeroFlag(bool expected,
        string because = "", params object[] becauseArgs)
        => HaveFlag("Z", Subject.Registers.Z, expected, because, becauseArgs);

    public AndConstraint<CortexM0Assertions> HaveCarryFlag(bool expected,
        string because = "", params object[] becauseArgs)
        => HaveFlag("C", Subject.Registers.C, expected, because, becauseArgs);

    public AndConstraint<CortexM0Assertions> HaveNegativeFlag(bool expected,
        string because = "", params object[] becauseArgs)
        => HaveFlag("N", Subject.Registers.N, expected, because, becauseArgs);

    public AndConstraint<CortexM0Assertions> HaveOverflowFlag(bool expected,
        string because = "", params object[] becauseArgs)
        => HaveFlag("V", Subject.Registers.V, expected, because, becauseArgs);

    private AndConstraint<CortexM0Assertions> HaveFlag(string name, bool actual, bool expected,
        string because, object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(actual == expected)
            .FailWith("Expected flag {0} to be {1}{reason}, but found {2}.", name, expected, actual);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    // ── Health checks (handy for CI / firmware smoke tests) ──────────────────────

    /// <summary>Assert the CPU has not locked up (no HardFault escalation / firmware crash).</summary>
    public AndConstraint<CortexM0Assertions> NotBeLockedUp(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.IsLockedUp)
            .FailWith("Expected the CPU not to be locked up{reason}, but it was " +
                      "(a HardFault escalated — the firmware crashed). PC=0x{0:X8}.", Subject.Registers.PC);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    /// <summary>
    /// Assert the CPU is healthy: not locked up and not handling a HardFault (IPSR != 3).
    /// The go-to smoke check after running firmware.
    /// </summary>
    public AndConstraint<CortexM0Assertions> NotHaveFaulted(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.IsLockedUp && Subject.Registers.IPSR != 3)
            .FailWith("Expected the CPU not to have faulted{reason}, but IPSR={0} (3 = HardFault) " +
                      "and IsLockedUp={1}.", Subject.Registers.IPSR, Subject.IsLockedUp);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    /// <summary>Assert the CPU is in Thread mode (IPSR == 0), i.e. not inside an exception handler.</summary>
    public AndConstraint<CortexM0Assertions> BeInThreadMode(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Registers.IPSR == 0)
            .FailWith("Expected the CPU to be in Thread mode{reason}, but it was handling exception {0}.",
                Subject.Registers.IPSR);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    /// <summary>
    /// Assert the CPU has executed no more than <paramref name="maxInstructions"/> since reset.
    /// Useful as a deterministic compiler-regression guard (instruction count is reproducible).
    /// </summary>
    public AndConstraint<CortexM0Assertions> HaveExecutedAtMost(long maxInstructions,
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Cycles <= maxInstructions)
            .FailWith("Expected the CPU to execute at most {0} instructions{reason}, but it executed {1}.",
                maxInstructions, Subject.Cycles);
        return new AndConstraint<CortexM0Assertions>(this);
    }
}
