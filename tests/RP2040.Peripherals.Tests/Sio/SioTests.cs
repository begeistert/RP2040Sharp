using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals.Sio;
using RP2040.Peripherals.Tests.Fixtures;

namespace RP2040.Peripherals.Tests.Sio;

/// <summary>
/// Tests for the SIO hardware divider (§2.3.1.6 of RP2040 TRM).
/// </summary>
public abstract class SioTests
{
    private sealed class Fixture : IDisposable
    {
        public BusInterconnect Bus { get; }
        public CortexM0Plus Cpu { get; }
        public SioPeripheral Sio { get; }

        public Fixture()
        {
            Bus = new BusInterconnect();
            Cpu = new CortexM0Plus(Bus);
            Sio = new SioPeripheral(Cpu);
        }

        public void Dispose() => Bus.Dispose();
    }

    // Unsigned divide register offsets (local from SIO base 0xD0000000)
    private const uint DIV_UDIVIDEND = 0x060;
    private const uint DIV_UDIVISOR  = 0x064;
    private const uint DIV_SDIVIDEND = 0x068;
    private const uint DIV_SDIVISOR  = 0x06C;
    private const uint DIV_QUOTIENT  = 0x070;
    private const uint DIV_REMAINDER = 0x074;
    private const uint DIV_CSR       = 0x078;

    private const uint SPINLOCK_BASE = 0x100;

    public class UnsignedDivide
    {
        [Fact]
        public void Divide_100_by_7_returns_quotient_14_remainder_2()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_UDIVIDEND, 100);
            f.Sio.WriteWord(DIV_UDIVISOR, 7);

            f.Sio.ReadWord(DIV_QUOTIENT).Should().Be(14u);
            f.Sio.ReadWord(DIV_REMAINDER).Should().Be(2u);
        }

        [Fact]
        public void Divide_sets_CSR_READY_bit()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_UDIVIDEND, 50);
            f.Sio.WriteWord(DIV_UDIVISOR, 5);

            var csr = f.Sio.ReadWord(DIV_CSR);
            (csr & 0x2).Should().Be(2u, "READY bit must be set after divide");
        }

        [Fact]
        public void Divide_by_zero_returns_0xFFFFFFFF_quotient()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_UDIVIDEND, 42);
            f.Sio.WriteWord(DIV_UDIVISOR, 0);

            f.Sio.ReadWord(DIV_QUOTIENT).Should().Be(0xFFFFFFFFu);
            f.Sio.ReadWord(DIV_REMAINDER).Should().Be(42u);
        }

        [Fact]
        public void Divide_large_value_rounds_down()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_UDIVIDEND, 0xFFFFFFFF);
            f.Sio.WriteWord(DIV_UDIVISOR, 0x10000);

            f.Sio.ReadWord(DIV_QUOTIENT).Should().Be(0xFFFFu);
        }
    }

    public class SignedDivide
    {
        [Fact]
        public void Signed_divide_negative_dividend()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_SDIVIDEND, unchecked((uint)-100));
            f.Sio.WriteWord(DIV_SDIVISOR, 7);

            // -100 / 7 = -14 remainder -2 (truncation towards zero)
            f.Sio.ReadWord(DIV_QUOTIENT).Should().Be(unchecked((uint)-14));
            f.Sio.ReadWord(DIV_REMAINDER).Should().Be(unchecked((uint)-2));
        }

        [Fact]
        public void Signed_divide_both_negative()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_SDIVIDEND, unchecked((uint)-48));
            f.Sio.WriteWord(DIV_SDIVISOR, unchecked((uint)-6));

            f.Sio.ReadWord(DIV_QUOTIENT).Should().Be(8u);
            f.Sio.ReadWord(DIV_REMAINDER).Should().Be(0u);
        }

        [Fact]
        public void Signed_divide_by_zero_positive_dividend()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_SDIVIDEND, 10);
            f.Sio.WriteWord(DIV_SDIVISOR, 0);

            // positive dividend / 0 → quotient = 1 per RP2040 spec
            f.Sio.ReadWord(DIV_QUOTIENT).Should().Be(1u);
        }

        [Fact]
        public void Signed_divide_by_zero_negative_dividend()
        {
            using var f = new Fixture();
            f.Sio.WriteWord(DIV_SDIVIDEND, unchecked((uint)-10));
            f.Sio.WriteWord(DIV_SDIVISOR, 0);

            // negative dividend / 0 → quotient = -1 = 0xFFFFFFFF per RP2040 spec
            f.Sio.ReadWord(DIV_QUOTIENT).Should().Be(0xFFFFFFFFu);
        }
    }

    public class DividerSave
    {
        [Fact]
        public void Writing_quotient_sets_DIRTY_bit()
        {
            using var f = new Fixture();
            // Normal divide first
            f.Sio.WriteWord(DIV_UDIVIDEND, 10);
            f.Sio.WriteWord(DIV_UDIVISOR, 2);
            // Verify READY is set
            (f.Sio.ReadWord(DIV_CSR) & 0x2).Should().Be(2u);

            // Save quotient (simulating context save)
            f.Sio.WriteWord(DIV_QUOTIENT, 99);
            // DIRTY bit should now be set (bit 0)
            (f.Sio.ReadWord(DIV_CSR) & 0x1).Should().Be(1u, "writing quotient sets DIRTY");
        }
    }

    public class Spinlocks
    {
        [Fact]
        public void Claim_unclaimed_spinlock_returns_nonzero()
        {
            using var f = new Fixture();
            var result = f.Sio.ReadWord(SPINLOCK_BASE);  // spinlock 0
            result.Should().NotBe(0u, "claiming a free spinlock returns its bit");
        }

        [Fact]
        public void Claim_already_taken_spinlock_returns_zero()
        {
            using var f = new Fixture();
            f.Sio.ReadWord(SPINLOCK_BASE);  // claim spinlock 0
            var second = f.Sio.ReadWord(SPINLOCK_BASE);
            second.Should().Be(0u, "spinlock already held returns 0");
        }

        [Fact]
        public void Release_spinlock_allows_reclaim()
        {
            using var f = new Fixture();
            f.Sio.ReadWord(SPINLOCK_BASE);          // claim
            f.Sio.WriteWord(SPINLOCK_BASE, 0);      // release (any write)
            var reclaim = f.Sio.ReadWord(SPINLOCK_BASE);
            reclaim.Should().NotBe(0u, "reclaiming after release must succeed");
        }

        [Fact]
        public void SPINLOCK_ST_reflects_claimed_locks()
        {
            using var f = new Fixture();
            const uint SPINLOCK_ST = 0x05C;
            f.Sio.ReadWord(SPINLOCK_BASE);       // claim spinlock 0
            f.Sio.ReadWord(SPINLOCK_BASE + 4);   // claim spinlock 1
            var st = f.Sio.ReadWord(SPINLOCK_ST);
            (st & 0x3).Should().Be(0x3u, "SPINLOCK_ST bits 0-1 reflect claimed locks");
        }
    }
}
