using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using RP2040.Core.Cpu;

namespace RP2040.TestKit.Assertions;

/// <summary>FluentAssertions extension for <see cref="CortexM0Plus"/>.</summary>
public sealed class CortexM0Assertions : ReferenceTypeAssertions<CortexM0Plus, CortexM0Assertions>
{
    private readonly AssertionChain _chain;

    public CortexM0Assertions(CortexM0Plus subject, AssertionChain chain) : base(subject, chain)
        => _chain = chain;

    protected override string Identifier => "cpu";

    public AndConstraint<CortexM0Assertions> HaveRegister(int index, uint expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Registers[index] == expected)
            .FailWith("Expected R{0} to be 0x{1:X8}{reason}, but found 0x{2:X8}.",
                index, expected, Subject.Registers[index]);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    public AndConstraint<CortexM0Assertions> HavePC(uint expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Registers.PC == expected)
            .FailWith("Expected PC to be 0x{0:X8}{reason}, but found 0x{1:X8}.",
                expected, Subject.Registers.PC);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    public AndConstraint<CortexM0Assertions> HaveSP(uint expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Registers.SP == expected)
            .FailWith("Expected SP to be 0x{0:X8}{reason}, but found 0x{1:X8}.",
                expected, Subject.Registers.SP);
        return new AndConstraint<CortexM0Assertions>(this);
    }

    public AndConstraint<CortexM0Assertions> HaveCycles(long expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
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
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(actual == expected)
            .FailWith("Expected flag {0} to be {1}{reason}, but found {2}.", name, expected, actual);
        return new AndConstraint<CortexM0Assertions>(this);
    }
}
