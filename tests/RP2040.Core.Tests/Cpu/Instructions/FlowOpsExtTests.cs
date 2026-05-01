using FluentAssertions;
using RP2040.Core.Helpers;
using RP2040.tests.Fixtures;

namespace RP2040.tests.Cpu.Instructions;

public abstract class FlowOpsExtTests
{
    // ================================================================
    // CBZ (Compare and Branch if Zero)
    // ================================================================
    public class Cbz : CpuTestBase
    {
        [Fact]
        public void Should_Branch_When_Register_Is_Zero()
        {
            // CBZ R0, #4 → branch to PC+4+4 = 0x20000000+2+4+2 = 0x20000008
            var opcode = InstructionEmiter.Cbz(R0, 4);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0;

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000008);
        }

        [Fact]
        public void Should_NotBranch_When_Register_IsNonZero()
        {
            var opcode = InstructionEmiter.Cbz(R0, 4);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 1;

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000002);
        }

        [Fact]
        public void Should_Branch_With_Zero_Offset()
        {
            // CBZ R1, #0 → branch to PC+2+0+2 = 0x20000004 (next-next instruction)
            var opcode = InstructionEmiter.Cbz(R1, 0);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0;

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000004);
        }

        [Fact]
        public void Should_Branch_With_Large_Offset_Using_i_Bit()
        {
            // offset = 66 → i=1, imm5[4:0]=bits[5:1] of 66 = 1
            var opcode = InstructionEmiter.Cbz(R2, 66);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R2] = 0;

            Cpu.Step();

            // ARM target = instrAddr + 4 + imm32 = 0x20000000 + 4 + 66 = 0x20000046
            Cpu.Registers.PC.Should().Be(0x20000046);
        }
    }

    // ================================================================
    // CBNZ (Compare and Branch if Non-Zero)
    // ================================================================
    public class Cbnz : CpuTestBase
    {
        [Fact]
        public void Should_Branch_When_Register_Is_NonZero()
        {
            var opcode = InstructionEmiter.Cbnz(R0, 4);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 42;

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000008);
        }

        [Fact]
        public void Should_NotBranch_When_Register_Is_Zero()
        {
            var opcode = InstructionEmiter.Cbnz(R0, 4);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0;

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000002);
        }
    }
}
