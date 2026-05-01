using FluentAssertions;
using RP2040.Core.Helpers;
using RP2040.tests.Fixtures;

namespace RP2040.tests.Cpu.Instructions;

public abstract class SystemOpsExtTests
{
    // ================================================================
    // CPSID / CPSIE
    // ================================================================
    public class Cps : CpuTestBase
    {
        [Fact]
        public void Cpsid_Should_Set_PRIMASK()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Cpsid);
            Cpu.Registers.PRIMASK = 0;

            Cpu.Step();

            Cpu.Registers.PRIMASK.Should().Be(1);
        }

        [Fact]
        public void Cpsie_Should_Clear_PRIMASK()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Cpsie);
            Cpu.Registers.PRIMASK = 1;

            Cpu.Step();

            Cpu.Registers.PRIMASK.Should().Be(0);
        }

        [Fact]
        public void Cpsie_Should_Set_InterruptsUpdated_Flag()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Cpsie);

            Cpu.Step();

            Cpu.Registers.InterruptsUpdated.Should().Be(true);
        }
    }

    // ================================================================
    // WFI
    // ================================================================
    public class Wfi : CpuTestBase
    {
        [Fact]
        public void Should_Set_Waiting_Flag()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Wfi);

            Cpu.Step();

            Cpu.Registers.Waiting.Should().Be(true);
        }
    }

    // ================================================================
    // SEV / WFE
    // ================================================================
    public class SevWfe : CpuTestBase
    {
        [Fact]
        public void Sev_Should_Set_EventRegistered()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Sev);

            Cpu.Step();

            Cpu.Registers.EventRegistered.Should().Be(true);
        }

        [Fact]
        public void Wfe_Should_Set_Waiting_When_No_Event()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Wfe);
            Cpu.Registers.EventRegistered = false;

            Cpu.Step();

            Cpu.Registers.Waiting.Should().Be(true);
        }

        [Fact]
        public void Wfe_Should_Clear_Event_And_Not_Wait_When_Event_Registered()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Wfe);
            Cpu.Registers.EventRegistered = true;

            Cpu.Step();

            Cpu.Registers.Waiting.Should().Be(false);
            Cpu.Registers.EventRegistered.Should().Be(false);
        }
    }

    // ================================================================
    // BKPT
    // ================================================================
    public class Bkpt : CpuTestBase
    {
        [Fact]
        public void Should_Invoke_OnBreakpoint_Callback_With_Imm8()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Bkpt(42));

            byte? received = null;
            Cpu.OnBreakpoint = imm => received = imm;

            Cpu.Step();

            received.Should().Be(42);
        }

        [Fact]
        public void Should_Not_Throw_When_No_Callback_Registered()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Bkpt(0));
            Cpu.OnBreakpoint = null;

            var act = () => Cpu.Step();
            act.Should().NotThrow();
        }
    }

    // ================================================================
    // SVC (triggers PendingSVCall)
    // ================================================================
    public class Svc : CpuTestBase
    {
        [Fact]
        public void Should_Set_PendingSVCall_Flag()
        {
            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Svc(0));

            Cpu.Step();

            Cpu.Registers.PendingSVCall.Should().Be(true);
            Cpu.Registers.InterruptsUpdated.Should().Be(true);
        }
    }
}
