using FluentAssertions;
using RP2040.Core.Helpers;
using RP2040.tests.Fixtures;

namespace RP2040.tests.Cpu.Instructions;

public abstract class MemoryOpsStoreTests
{
    // ================================================================
    // STR (Store Word)
    // ================================================================
    public class StrImmediate : CpuTestBase
    {
        [Fact]
        public void Should_Store_Word_At_RnPlusImm5()
        {
            var opcode = InstructionEmiter.Str(R1, R0, 8);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 0xDEADBEEF;

            Cpu.Step();

            Bus.ReadWord(0x20000108).Should().Be(0xDEADBEEF);
        }

        [Fact]
        public void Should_Store_Word_AtBase_WhenImm_IsZero()
        {
            var opcode = InstructionEmiter.Str(R2, R3, 0);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R3] = 0x20000200;
            Cpu.Registers[R2] = 0x12345678;

            Cpu.Step();

            Bus.ReadWord(0x20000200).Should().Be(0x12345678);
        }
    }

    public class StrSpRelative : CpuTestBase
    {
        [Fact]
        public void Should_Store_Word_Relative_To_SP()
        {
            var opcode = InstructionEmiter.StrSpRelative(R0, 16);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers.SP = 0x20010000;
            Cpu.Registers[R0] = 0xCAFEBABE;

            Cpu.Step();

            Bus.ReadWord(0x20010010).Should().Be(0xCAFEBABE);
        }
    }

    public class StrRegister : CpuTestBase
    {
        [Fact]
        public void Should_Store_Word_At_RnPlusRm()
        {
            var opcode = InstructionEmiter.StrRegister(R2, R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 0x10;
            Cpu.Registers[R2] = 0xFEEDC0DE;

            Cpu.Step();

            Bus.ReadWord(0x20000110).Should().Be(0xFEEDC0DE);
        }
    }

    // ================================================================
    // STRB (Store Byte)
    // ================================================================
    public class StrbImmediate : CpuTestBase
    {
        [Fact]
        public void Should_Store_Byte_At_RnPlusImm5()
        {
            var opcode = InstructionEmiter.Strb(R1, R0, 3);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 0xABCD1234;   // only low byte stored

            Cpu.Step();

            Bus.ReadByte(0x20000103).Should().Be(0x34);
        }

        [Fact]
        public void Should_Store_Only_LowByte()
        {
            var opcode = InstructionEmiter.Strb(R0, R1, 0);
            Bus.WriteHalfWord(0x20000000, opcode);
            Bus.WriteWord(0x20000200, 0xFFFFFFFF);
            Cpu.Registers[R1] = 0x20000200;
            Cpu.Registers[R0] = 0xAB;

            Cpu.Step();

            Bus.ReadByte(0x20000200).Should().Be(0xAB);
            // Remaining bytes must remain unchanged
            Bus.ReadByte(0x20000201).Should().Be(0xFF);
        }
    }

    // ================================================================
    // STRH (Store Halfword)
    // ================================================================
    public class StrhImmediate : CpuTestBase
    {
        [Fact]
        public void Should_Store_Halfword_At_RnPlusImm5()
        {
            var opcode = InstructionEmiter.Strh(R1, R0, 4);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 0xABCDEF12;   // only low halfword stored

            Cpu.Step();

            Bus.ReadHalfWord(0x20000104).Should().Be(0xEF12);
        }
    }

    // ================================================================
    // LDRB (Load Byte, zero-extend)
    // ================================================================
    public class LdrbImmediate : CpuTestBase
    {
        [Fact]
        public void Should_Load_Byte_ZeroExtended()
        {
            var opcode = InstructionEmiter.Ldrb(R1, R0, 2);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Bus.WriteByte(0x20000102, 0xAB);

            Cpu.Step();

            Cpu.Registers[R1].Should().Be(0xAB);
        }

        [Fact]
        public void Should_ZeroExtend_High_Byte_Value()
        {
            var opcode = InstructionEmiter.Ldrb(R0, R1, 0);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R1] = 0x20000200;
            Bus.WriteByte(0x20000200, 0xFF);

            Cpu.Step();

            Cpu.Registers[R0].Should().Be(0x000000FF);
        }
    }

    // ================================================================
    // LDRH (Load Halfword, zero-extend)
    // ================================================================
    public class LdrhImmediate : CpuTestBase
    {
        [Fact]
        public void Should_Load_Halfword_ZeroExtended()
        {
            var opcode = InstructionEmiter.Ldrh(R1, R0, 0);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Bus.WriteHalfWord(0x20000100, 0xBEEF);

            Cpu.Step();

            Cpu.Registers[R1].Should().Be(0x0000BEEF);
        }
    }

    // ================================================================
    // LDRSB (Load Signed Byte, sign-extend)
    // ================================================================
    public class Ldrsb : CpuTestBase
    {
        [Fact]
        public void Should_SignExtend_Negative_Byte()
        {
            var opcode = InstructionEmiter.Ldrsb(R2, R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 5;
            Bus.WriteByte(0x20000105, 0x80);    // -128 as signed byte

            Cpu.Step();

            Cpu.Registers[R2].Should().Be(0xFFFFFF80);
        }

        [Fact]
        public void Should_SignExtend_Positive_Byte()
        {
            var opcode = InstructionEmiter.Ldrsb(R2, R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 0;
            Bus.WriteByte(0x20000100, 0x7F);    // +127

            Cpu.Step();

            Cpu.Registers[R2].Should().Be(0x0000007F);
        }
    }

    // ================================================================
    // LDRSH (Load Signed Halfword, sign-extend)
    // ================================================================
    public class Ldrsh : CpuTestBase
    {
        [Fact]
        public void Should_SignExtend_Negative_Halfword()
        {
            var opcode = InstructionEmiter.Ldrsh(R2, R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 0;
            Bus.WriteHalfWord(0x20000100, 0x8000);  // -32768

            Cpu.Step();

            Cpu.Registers[R2].Should().Be(0xFFFF8000);
        }

        [Fact]
        public void Should_SignExtend_Positive_Halfword()
        {
            var opcode = InstructionEmiter.Ldrsh(R2, R0, R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0x20000100;
            Cpu.Registers[R1] = 0;
            Bus.WriteHalfWord(0x20000100, 0x7FFF);  // +32767

            Cpu.Step();

            Cpu.Registers[R2].Should().Be(0x00007FFF);
        }
    }

    // ================================================================
    // STR + LDR round-trip
    // ================================================================
    public class StoreLoadRoundTrip : CpuTestBase
    {
        [Fact]
        public void Strb_Ldrb_RoundTrip_ShouldPreserve_Byte()
        {
            const byte value = 0xCD;
            const uint addr = 0x20000400;

            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Strb(R0, R1, 0));
            Bus.WriteHalfWord(0x20000002, InstructionEmiter.Ldrb(R2, R1, 0));

            Cpu.Registers[R0] = value;
            Cpu.Registers[R1] = addr;

            Cpu.Step();
            Cpu.Step();

            Cpu.Registers[R2].Should().Be(value);
        }

        [Fact]
        public void Strh_Ldrh_RoundTrip_ShouldPreserve_Halfword()
        {
            const ushort value = 0x1234;
            const uint addr = 0x20000500;

            Bus.WriteHalfWord(0x20000000, InstructionEmiter.Strh(R0, R1, 0));
            Bus.WriteHalfWord(0x20000002, InstructionEmiter.Ldrh(R2, R1, 0));

            Cpu.Registers[R0] = value;
            Cpu.Registers[R1] = addr;

            Cpu.Step();
            Cpu.Step();

            Cpu.Registers[R2].Should().Be(value);
        }
    }

    // ================================================================
    // STMIA (Store Multiple Increment After)
    // ================================================================
    public class Stmia : CpuTestBase
    {
        [Fact]
        public void Should_Store_Multiple_And_WriteBack()
        {
            var opcode = InstructionEmiter.Stmia(R0, 1 << R1 | 1 << R2);
            Bus.WriteHalfWord(0x20000000, opcode);
            const uint baseAddr = 0x20000010;
            Cpu.Registers[R0] = baseAddr;
            Cpu.Registers[R1] = 0xAABBCCDD;
            Cpu.Registers[R2] = 0x11223344;

            Cpu.Step();

            Bus.ReadWord(baseAddr).Should().Be(0xAABBCCDD);
            Bus.ReadWord(baseAddr + 4).Should().Be(0x11223344);
            Cpu.Registers[R0].Should().Be(baseAddr + 8, "STMIA always writes back");
        }

        [Fact]
        public void Should_WriteBack_EvenWhenRn_InList()
        {
            var opcode = InstructionEmiter.Stmia(R0, 1 << R0 | 1 << R1);
            Bus.WriteHalfWord(0x20000000, opcode);
            const uint baseAddr = 0x20000020;
            Cpu.Registers[R0] = baseAddr;
            Cpu.Registers[R1] = 0x5A5A5A5A;

            Cpu.Step();

            // STMIA always writes back (unlike LDMIA)
            Cpu.Registers[R0].Should().Be(baseAddr + 8);
        }
    }
}
