using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using RP2040.Peripherals.Gpio;

namespace RP2040.TestKit.Assertions;

/// <summary>FluentAssertions extension for <see cref="GpioPin"/>.</summary>
public sealed class GpioAssertions : ReferenceTypeAssertions<GpioPin, GpioAssertions>
{
    public GpioAssertions(GpioPin subject) : base(subject) { }

    protected override string Identifier => "pin";

    public AndConstraint<GpioAssertions> BeHigh(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.DigitalValue)
            .FailWith("Expected GPIO pin to be HIGH{reason}, but it was LOW.");
        return new AndConstraint<GpioAssertions>(this);
    }

    public AndConstraint<GpioAssertions> BeLow(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.DigitalValue)
            .FailWith("Expected GPIO pin to be LOW{reason}, but it was HIGH.");
        return new AndConstraint<GpioAssertions>(this);
    }

    public AndConstraint<GpioAssertions> BeOutput(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsOutput)
            .FailWith("Expected GPIO pin to be configured as OUTPUT{reason}, but it was INPUT.");
        return new AndConstraint<GpioAssertions>(this);
    }

    /// <summary>
    /// Assert that the pin is assigned to a PIO state machine (FUNCSEL = 6 or 7).
    /// Use this for pins configured via <c>pio_gpio_init()</c>, which sets IO_BANK0 FUNCSEL
    /// rather than SIO GPIO_OE (which <see cref="BeOutput"/> checks).
    /// </summary>
    public AndConstraint<GpioAssertions> BePioOutput(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsPioOutput)
            .FailWith("Expected GPIO pin to be assigned to a PIO state machine (FUNCSEL=6 or 7){reason}, but it was not.");
        return new AndConstraint<GpioAssertions>(this);
    }

    public AndConstraint<GpioAssertions> BeInput(
        string because = "", params object[] becauseArgs)
    {
        Execute.Assertion.BecauseOf(because, becauseArgs)
            .ForCondition(!Subject.IsOutput)
            .FailWith("Expected GPIO pin to be configured as INPUT{reason}, but it was OUTPUT.");
        return new AndConstraint<GpioAssertions>(this);
    }
}
