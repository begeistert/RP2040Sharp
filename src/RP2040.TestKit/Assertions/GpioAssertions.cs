using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using RP2040.Peripherals.Gpio;

namespace RP2040.TestKit.Assertions;

/// <summary>FluentAssertions extension for <see cref="GpioPin"/>.</summary>
public sealed class GpioAssertions : ReferenceTypeAssertions<GpioPin, GpioAssertions>
{
    private readonly AssertionChain _chain;

    public GpioAssertions(GpioPin subject, AssertionChain chain) : base(subject, chain)
        => _chain = chain;

    protected override string Identifier => "pin";

    public AndConstraint<GpioAssertions> BeHigh(
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.DigitalValue)
            .FailWith("Expected GPIO pin to be HIGH{reason}, but it was LOW.");
        return new AndConstraint<GpioAssertions>(this);
    }

    public AndConstraint<GpioAssertions> BeLow(
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.DigitalValue)
            .FailWith("Expected GPIO pin to be LOW{reason}, but it was HIGH.");
        return new AndConstraint<GpioAssertions>(this);
    }

    public AndConstraint<GpioAssertions> BeOutput(
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsOutput)
            .FailWith("Expected GPIO pin to be configured as OUTPUT{reason}, but it was INPUT.");
        return new AndConstraint<GpioAssertions>(this);
    }

    public AndConstraint<GpioAssertions> BeInput(
        string because = "", params object[] becauseArgs)
    {
        _chain.BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.IsOutput)
            .FailWith("Expected GPIO pin to be configured as INPUT{reason}, but it was OUTPUT.");
        return new AndConstraint<GpioAssertions>(this);
    }
}
