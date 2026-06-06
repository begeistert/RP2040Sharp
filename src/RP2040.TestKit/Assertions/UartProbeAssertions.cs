using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using RP2040.TestKit.Probes;

namespace RP2040.TestKit.Assertions;

/// <summary>FluentAssertions extension for <see cref="UartProbe"/>.</summary>
public sealed class UartProbeAssertions : ReferenceTypeAssertions<UartProbe, UartProbeAssertions>
{
    private readonly AssertionChain _chain;

    public UartProbeAssertions(UartProbe subject, AssertionChain chain) : base(subject, chain)
        => _chain = chain;

    protected override string Identifier => "uart";

    public AndConstraint<UartProbeAssertions> Contain(string expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Text.Contains(expected))
            .FailWith("Expected UART output to contain {0}{reason}, but found {1}.",
                expected, Subject.Text);
        return new AndConstraint<UartProbeAssertions>(this);
    }

    public AndConstraint<UartProbeAssertions> NotContain(string expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.Text.Contains(expected))
            .FailWith("Expected UART output not to contain {0}{reason}, but found {1}.",
                expected, Subject.Text);
        return new AndConstraint<UartProbeAssertions>(this);
    }

    public AndConstraint<UartProbeAssertions> StartWith(string expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Text.StartsWith(expected, StringComparison.Ordinal))
            .FailWith("Expected UART output to start with {0}{reason}, but found {1}.",
                expected, Subject.Text);
        return new AndConstraint<UartProbeAssertions>(this);
    }

    public AndConstraint<UartProbeAssertions> BeEmpty(
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.ByteCount == 0)
            .FailWith("Expected UART to have no output{reason}, but found {0} bytes.", Subject.ByteCount);
        return new AndConstraint<UartProbeAssertions>(this);
    }

    public AndConstraint<UartProbeAssertions> HaveByteCount(int expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.ByteCount == expected)
            .FailWith("Expected UART to have {0} bytes{reason}, but found {1}.",
                expected, Subject.ByteCount);
        return new AndConstraint<UartProbeAssertions>(this);
    }

    public AndConstraint<UartProbeAssertions> ContainLine(string expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Lines.Contains(expected))
            .FailWith("Expected UART output to contain line {0}{reason}.", expected);
        return new AndConstraint<UartProbeAssertions>(this);
    }

    public AndConstraint<UartProbeAssertions> HaveLineCount(int expected,
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.Lines.Count == expected)
            .FailWith("Expected UART output to have {0} lines{reason}, but found {1}.",
                expected, Subject.Lines.Count);
        return new AndConstraint<UartProbeAssertions>(this);
    }
}
