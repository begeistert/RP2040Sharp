using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals.Dma;

namespace RP2040.Peripherals.Tests.Dma;

/// <summary>
/// Tests for the RP2040 DMA peripheral.
/// </summary>
public abstract class DmaTests
{
    private const uint CHANNEL_COUNT = 12;

    // Per-channel register offsets: base = ch * 0x40
    private static uint ChBase(int ch) => (uint)(ch * 0x40);
    private static uint READ_ADDR(int ch)    => ChBase(ch) + 0x00;
    private static uint WRITE_ADDR(int ch)   => ChBase(ch) + 0x04;
    private static uint TRANS_COUNT(int ch)  => ChBase(ch) + 0x08;
    private static uint CTRL_TRIG(int ch)    => ChBase(ch) + 0x0C;

    // System registers
    private const uint INTR      = 0x400;
    private const uint INTE0     = 0x404;
    private const uint INTF0     = 0x408;
    private const uint INTS0     = 0x40C;

    // CTRL bits
    private const uint CTRL_EN            = 1u << 0;
    private const uint CTRL_DATA_SIZE_WORD = 2u << 2;   // SIZE=2 (word = 4 bytes)
    private const uint CTRL_INCR_READ     = 1u << 4;
    private const uint CTRL_INCR_WRITE    = 1u << 5;
    private const uint CTRL_TREQ_PERMANENT = 0x3Fu << 15;  // TREQ_SEL=63 = always ready

    private sealed class Fixture : IDisposable
    {
        public BusInterconnect Bus { get; }
        public CortexM0Plus Cpu { get; }
        public DmaPeripheral Dma { get; }

        public Fixture()
        {
            Bus = new BusInterconnect();
            Cpu = new CortexM0Plus(Bus);
            Dma = new DmaPeripheral(Bus, Cpu);
        }

        public void Dispose() => Bus.Dispose();

        /// <summary>Write a 32-bit word to SRAM at the given address.</summary>
        public void WriteToSram(uint addr, uint value) =>
            Bus.WriteWord(addr, value);

        /// <summary>Read a 32-bit word from SRAM.</summary>
        public uint ReadFromSram(uint addr) =>
            Bus.ReadWord(addr);
    }

    public class BasicTransfer
    {
        [Fact]
        public void Single_word_transfer_copies_data()
        {
            using var f = new Fixture();

            // Write source data to SRAM
            f.WriteToSram(0x20000000u, 0x12345678u);

            // Configure ch0: read from 0x20000000, write to 0x20001000, 1 word
            f.Dma.WriteWord(READ_ADDR(0),   0x20000000u);
            f.Dma.WriteWord(WRITE_ADDR(0),  0x20001000u);
            f.Dma.WriteWord(TRANS_COUNT(0), 1u);
            f.Dma.WriteWord(CTRL_TRIG(0),
                CTRL_EN | CTRL_DATA_SIZE_WORD | CTRL_INCR_READ | CTRL_INCR_WRITE | CTRL_TREQ_PERMANENT);

            f.ReadFromSram(0x20001000u).Should().Be(0x12345678u);
        }

        [Fact]
        public void Multi_word_transfer_copies_block()
        {
            using var f = new Fixture();

            // Write 4 words to SRAM
            for (uint i = 0; i < 4; i++)
                f.WriteToSram(0x20002000u + i * 4, 0xAABBCC00u | i);

            f.Dma.WriteWord(READ_ADDR(0),   0x20002000u);
            f.Dma.WriteWord(WRITE_ADDR(0),  0x20003000u);
            f.Dma.WriteWord(TRANS_COUNT(0), 4u);
            f.Dma.WriteWord(CTRL_TRIG(0),
                CTRL_EN | CTRL_DATA_SIZE_WORD | CTRL_INCR_READ | CTRL_INCR_WRITE | CTRL_TREQ_PERMANENT);

            for (uint i = 0; i < 4; i++)
                f.ReadFromSram(0x20003000u + i * 4).Should().Be(0xAABBCC00u | i);
        }

        [Fact]
        public void TRANS_COUNT_reads_back_before_trigger()
        {
            using var f = new Fixture();
            f.Dma.WriteWord(TRANS_COUNT(1), 64u);
            f.Dma.ReadWord(TRANS_COUNT(1)).Should().Be(64u);
        }
    }

    public class Chaining
    {
        [Fact]
        public void Channel_chaining_triggers_second_channel()
        {
            using var f = new Fixture();

            // Source for ch0 and ch1
            f.WriteToSram(0x20004000u, 0xCAFEBABEu);
            f.WriteToSram(0x20005000u, 0xDEAD1234u);

            // Pre-configure ch1 via AL1 alias (offset +0x10 within ch block) — NO trigger
            const uint CH1_BASE = 1 * 0x40;
            f.Dma.WriteWord(CH1_BASE + 0x00, 0x20005000u);  // READ_ADDR
            f.Dma.WriteWord(CH1_BASE + 0x04, 0x20007000u);  // WRITE_ADDR
            f.Dma.WriteWord(CH1_BASE + 0x08, 1u);           // TRANS_COUNT
            // Write CTRL via AL1 alias (no trigger): ch1 base + 0x10
            // CHAIN_TO = ch1 (itself = disable chaining) → bits [14:11] = 1
            f.Dma.WriteWord(CH1_BASE + 0x10,
                CTRL_EN | CTRL_DATA_SIZE_WORD | CTRL_INCR_READ | CTRL_INCR_WRITE | CTRL_TREQ_PERMANENT
                | (1u << 11));  // CHAIN_TO = 1 (self = no chain)

            // Trigger ch0 with CHAIN_TO=1 (bits [14:11] = 1 → 1 << 11)
            f.Dma.WriteWord(READ_ADDR(0),   0x20004000u);
            f.Dma.WriteWord(WRITE_ADDR(0),  0x20006000u);
            f.Dma.WriteWord(TRANS_COUNT(0), 1u);
            f.Dma.WriteWord(CTRL_TRIG(0),
                CTRL_EN | CTRL_DATA_SIZE_WORD | CTRL_INCR_READ | CTRL_INCR_WRITE | CTRL_TREQ_PERMANENT
                | (1u << 11));  // CHAIN_TO = 1

            // ch0 should have run and chained to ch1
            f.ReadFromSram(0x20006000u).Should().Be(0xCAFEBABEu, "ch0 data at dest");
            f.ReadFromSram(0x20007000u).Should().Be(0xDEAD1234u, "ch1 data at dest after chain");
        }
    }

    public class Interrupts
    {
        [Fact]
        public void INTR_bit_set_after_channel_completes()
        {
            using var f = new Fixture();
            f.WriteToSram(0x20008000u, 0xABCDEF01u);

            f.Dma.WriteWord(READ_ADDR(2),   0x20008000u);
            f.Dma.WriteWord(WRITE_ADDR(2),  0x20009000u);
            f.Dma.WriteWord(TRANS_COUNT(2), 1u);
            f.Dma.WriteWord(CTRL_TRIG(2),
                CTRL_EN | CTRL_DATA_SIZE_WORD | CTRL_INCR_READ | CTRL_INCR_WRITE | CTRL_TREQ_PERMANENT);

            (f.Dma.ReadWord(INTR) & (1u << 2)).Should().Be(1u << 2, "INTR bit2 should be set after ch2 completes");
        }

        [Fact]
        public void INTR_cleared_by_writing_1()
        {
            using var f = new Fixture();
            f.WriteToSram(0x2000A000u, 0u);

            f.Dma.WriteWord(READ_ADDR(3),   0x2000A000u);
            f.Dma.WriteWord(WRITE_ADDR(3),  0x2000B000u);
            f.Dma.WriteWord(TRANS_COUNT(3), 1u);
            f.Dma.WriteWord(CTRL_TRIG(3),
                CTRL_EN | CTRL_DATA_SIZE_WORD | CTRL_TREQ_PERMANENT);

            f.Dma.WriteWord(INTR, 1u << 3);  // clear ch3 interrupt
            (f.Dma.ReadWord(INTR) & (1u << 3)).Should().Be(0u, "INTR bit should be cleared");
        }
    }

    public class HalfWordTransfer
    {
        [Fact]
        public void Half_word_transfer_copies_2_bytes()
        {
            using var f = new Fixture();
            // Write halfword to SRAM
            f.Bus.WriteHalfWord(0x2000C000u, (ushort)0xABCD);

            f.Dma.WriteWord(READ_ADDR(4),   0x2000C000u);
            f.Dma.WriteWord(WRITE_ADDR(4),  0x2000D000u);
            f.Dma.WriteWord(TRANS_COUNT(4), 1u);
            f.Dma.WriteWord(CTRL_TRIG(4),
                CTRL_EN | (1u << 2) /* SIZE=1 halfword */ | CTRL_INCR_READ | CTRL_INCR_WRITE | CTRL_TREQ_PERMANENT);

            f.Bus.ReadHalfWord(0x2000D000u).Should().Be((ushort)0xABCD);
        }
    }
}

