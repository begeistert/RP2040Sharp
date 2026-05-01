using FluentAssertions;
using RP2040.Core.Helpers;
using RP2040.tests.Fixtures;

namespace RP2040.tests.Cpu.Instructions;

public abstract class BitOpsExtTests
{
    // ================================================================
    // ROR (Rotate Right, register)
    // ================================================================
    public class Ror : CpuTestBase
    {
        [Fact]
        public void Should_Rotate_Right_By_8()
        {
            var opcode = InstructionEmiter.Ror(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x12345678;
            Cpu.Registers[R1] = 8;

            Cpu.Step();

            // rotate 0x12345678 right 8: 0x78_123456
            Cpu.Registers[R0].Should().Be(0x78123456);
            // carry = bit(8-1)=bit7 of original = bit7 of 0x78 = 0 (0111_1000)
            Cpu.Registers.C.Should().Be(false);
        }

        [Fact]
        public void Should_Not_Change_Value_When_ShiftIs_Zero()
        {
            var opcode = InstructionEmiter.Ror(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0xABCDEF01;
            Cpu.Registers[R1] = 0;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0xABCDEF01);
        }

        [Fact]
        public void Should_Rotate_By_32_Returning_Same_Value()
        {
            var opcode = InstructionEmiter.Ror(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x12345678;
            Cpu.Registers[R1] = 32;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0x12345678);
            Cpu.Registers.C.Should().Be(false, "bit31 = 0");
        }

        [Fact]
        public void Should_Set_N_Flag_When_Result_Is_Negative()
        {
            var opcode = InstructionEmiter.Ror(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x01;   // shifting right will put bit0 to bit31
            Cpu.Registers[R1] = 1;

            Cpu.Step();

            Cpu.Registers.N.Should().Be(true, "bit0 rotated to bit31 sets N");
            Cpu.Registers.C.Should().Be(true, "bit0 was the carry");
        }
    }

    // ================================================================
    // SXTH (Sign-extend Halfword)
    // ================================================================
    public class Sxth : CpuTestBase
    {
        [Fact]
        public void Should_SignExtend_Negative_Halfword()
        {
            var opcode = InstructionEmiter.Sxth(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0xFFFF8001;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0xFFFF8001);
        }

        [Fact]
        public void Should_SignExtend_Positive_Halfword()
        {
            var opcode = InstructionEmiter.Sxth(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0xFFFF7FFF;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0x00007FFF);
        }
    }

    // ================================================================
    // SXTB (Sign-extend Byte)
    // ================================================================
    public class Sxtb : CpuTestBase
    {
        [Fact]
        public void Should_SignExtend_Negative_Byte()
        {
            var opcode = InstructionEmiter.Sxtb(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0xFFFFFF80;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0xFFFFFF80);
        }

        [Fact]
        public void Should_SignExtend_Positive_Byte()
        {
            var opcode = InstructionEmiter.Sxtb(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0xABCDEF7F;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0x0000007F);
        }
    }

    // ================================================================
    // UXTH (Zero-extend Halfword)
    // ================================================================
    public class Uxth : CpuTestBase
    {
        [Fact]
        public void Should_ZeroExtend_Halfword()
        {
            var opcode = InstructionEmiter.Uxth(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0xABCDBEEF;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0x0000BEEF);
        }
    }

    // ================================================================
    // UXTB (Zero-extend Byte)
    // ================================================================
    public class Uxtb : CpuTestBase
    {
        [Fact]
        public void Should_ZeroExtend_Byte()
        {
            var opcode = InstructionEmiter.Uxtb(R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0xABCDEF42;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0x00000042);
        }
    }

    // ================================================================
    // CLZ (Count Leading Zeros, Thumb-2 32-bit)
    // ================================================================
    public class Clz : CpuTestBase
    {
        [Fact]
        public void Should_Count_Leading_Zeros()
        {
            var (h1, h2) = InstructionEmiter.Clz(R0, R1);
            Bus.WriteHalfWord(0x20000000, h1);
            Bus.WriteHalfWord(0x20000002, h2);
            Cpu.Registers[R1] = 0x00080000;   // 12 leading zeros

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(12);
        }

        [Fact]
        public void Should_Return_32_For_Zero_Input()
        {
            var (h1, h2) = InstructionEmiter.Clz(R0, R1);
            Bus.WriteHalfWord(0x20000000, h1);
            Bus.WriteHalfWord(0x20000002, h2);
            Cpu.Registers[R1] = 0;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(32);
        }

        [Fact]
        public void Should_Return_0_For_MSB_Set()
        {
            var (h1, h2) = InstructionEmiter.Clz(R0, R1);
            Bus.WriteHalfWord(0x20000000, h1);
            Bus.WriteHalfWord(0x20000002, h2);
            Cpu.Registers[R1] = 0x80000000;

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0);
        }

        [Fact]
        public void Should_Advance_PC_By_4()
        {
            var (h1, h2) = InstructionEmiter.Clz(R0, R1);
            Bus.WriteHalfWord(0x20000000, h1);
            Bus.WriteHalfWord(0x20000002, h2);
            Cpu.Registers[R1] = 1;

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000004);
        }
    }
}
