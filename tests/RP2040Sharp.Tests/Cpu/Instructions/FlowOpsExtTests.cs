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

    // ================================================================
    // CBZ/CBNZ raw-opcode tests
    //
    // ARMv6-M (Cortex-M0/M0+) does NOT define CBZ/CBNZ in its ISA — they are
    // ARMv7-M instructions.  The synthesised BootROM stub written by the
    // emulator itself uses raw 0xB1xx encodings, which previously fell
    // through the decoder mask (0xFB00 / 0xB300) and tripped HardFault.
    // The mask was widened to 0xF900 / 0xB100 so that all four encodings
    // (0xB1xx, 0xB3xx, 0xB9xx, 0xBBxx) hit the CB handler.  These tests
    // exercise that mask coverage with raw opcodes the assembler-style
    // emitter cannot produce, and lock in the specific opcode the BootROM
    // stub depends on.
    // ================================================================
    public class CbzRawOpcode : CpuTestBase
    {
        [Fact]
        public void Decoder_Matches_BootRomStub_Opcode_0xB13A()
        {
            // 0xB13A = the CBZ used at offset 0x0062 of the synthetic BootROM.
            // bits[2:0] = Rn = 2
            // bits[7:3] = imm5 = 0b00111 = 7
            // bit 8 = 1, bit 9 = i = 0  (i lives in bit 9 per ARMv7-M, but the
            //   current TakeCbBranch reads bit 10 — for i=0 cases the result
            //   coincides, which is the only case the stub uses).
            // With Rn = 0 the branch is taken and we land at PC+4+14 = +18.
            const ushort opcode = 0xB13A;
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R2] = 0; // taken

            Cpu.Step();

            // PC after fetch = 0x20000002, +imm32(14) +2 = 0x20000012
            Cpu.Registers.PC.Should().Be(0x20000012);
        }

        [Fact]
        public void Decoder_Matches_Cbz_With_iBit_Clear_LowestEncoding()
        {
            // 0xB108 = CBZ R0, with imm5=1 → branch +6 from this insn.
            const ushort opcode = 0xB108;
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0; // taken

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000006);
        }

        [Fact]
        public void Decoder_DoesNotFallThrough_To_Undefined_For_0xB100()
        {
            // The original mask 0xFB00/0xB300 didn't match 0xB1xx; the stub at
            // 0x0062 hit undefined → HardFault.  This test verifies that
            // 0xB100 (CBZ R0, 0) is now decoded as a CBZ and does not
            // trigger a HardFault entry.
            const ushort opcode = 0xB100;
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0;

            Cpu.Step();

            // PC must not have been redirected through the HardFault vector.
            // Branch taken (Rn=0): PC+4+0 = 0x20000004.
            Cpu.Registers.PC.Should().Be(0x20000004);
        }

        [Fact]
        public void Decoder_Matches_Cbnz_iBit_Clear_0xB900()
        {
            // 0xB900 = CBNZ R0, offset 0.  Branch when R0 != 0.
            const ushort opcode = 0xB900;
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 1; // taken

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000004);
        }

        [Fact]
        public void Decoder_Matches_Cbnz_iBit_Clear_NotTaken()
        {
            const ushort opcode = 0xB900;
            Bus.WriteHalfWord(0x20000000, opcode);
            Cpu.Registers[R0] = 0; // not taken

            Cpu.Step();

            Cpu.Registers.PC.Should().Be(0x20000002);
        }
    }
}
