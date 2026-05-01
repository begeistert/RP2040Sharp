using FluentAssertions;
using RP2040.Peripherals;
using Xunit;

namespace RP2040.Peripherals.Tests.Bus;

/// <summary>
/// Verifies the RP2040 atomic register alias protocol.
/// Every APB/AHB peripheral register is mirrored at three alias windows:
///   base + 0x1000 → XOR (toggle bits)
///   base + 0x2000 → SET (bit-set / atomic OR)
///   base + 0x3000 → CLR (bit-clear / atomic AND NOT)
/// These are transparent to individual peripherals — the APBBridge/AHBBridge
/// handles the read-modify-write before dispatching the normalised value.
/// </summary>
public class AtomicAliasTests : IDisposable
{
    // Watchdog SCRATCH0 at APB slot 22 (base 0x40058000, offset 0x0C)
    // Watchdog is chosen because SCRATCH registers are plain read/write with no side effects.
    private const uint WDG_BASE    = 0x40058000;
    private const uint SCRATCH0    = 0x0C;

    private const uint NORMAL_ADDR = WDG_BASE + SCRATCH0;          // 0x4005800C
    private const uint XOR_ADDR    = WDG_BASE + 0x1000 + SCRATCH0; // 0x4005900C
    private const uint SET_ADDR    = WDG_BASE + 0x2000 + SCRATCH0; // 0x4005A00C
    private const uint CLR_ADDR    = WDG_BASE + 0x3000 + SCRATCH0; // 0x4005B00C

    private readonly RP2040Machine _m;

    public AtomicAliasTests() => _m = new RP2040Machine();
    public void Dispose() => _m.Dispose();

    // ── Normal write baseline ────────────────────────────────────────────

    [Fact]
    public void Normal_write_stores_value()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xDEADBEEF);
        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0xDEADBEEFu);
    }

    // ── XOR alias (+0x1000) ──────────────────────────────────────────────

    [Fact]
    public void XOR_alias_toggles_bits()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xFF00_FF00u);
        _m.Bus.WriteWord(XOR_ADDR, 0x0F0F_0F0Fu);   // XOR alias

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0xFF00_FF00u ^ 0x0F0F_0F0Fu,
            "XOR alias should toggle the written bits");
    }

    [Fact]
    public void XOR_alias_with_all_ones_inverts_register()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xAAAAAAAAu);
        _m.Bus.WriteWord(XOR_ADDR, 0xFFFFFFFFu);     // XOR with all-ones = bitwise NOT

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0x55555555u, "XOR all-ones = invert");
    }

    [Fact]
    public void XOR_alias_with_zero_is_noop()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0x12345678u);
        _m.Bus.WriteWord(XOR_ADDR, 0x0u);

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0x12345678u, "XOR with 0 leaves value unchanged");
    }

    // ── SET alias (+0x2000) ──────────────────────────────────────────────

    [Fact]
    public void SET_alias_sets_bits_atomically()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0x0000_0000u);
        _m.Bus.WriteWord(SET_ADDR, 0x0F0F_F0F0u);    // SET alias (OR)

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0x0F0F_F0F0u, "SET alias ORs new bits into register");
    }

    [Fact]
    public void SET_alias_preserves_existing_bits()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xF0F0_0000u);
        _m.Bus.WriteWord(SET_ADDR, 0x0F0F_0000u);    // set lower nibbles

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0xFFFF_0000u, "SET OR'd with existing bits");
    }

    [Fact]
    public void SET_alias_with_zero_is_noop()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xBEEF_CAFEu);
        _m.Bus.WriteWord(SET_ADDR, 0x0u);

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0xBEEF_CAFEu, "SET with 0 leaves value unchanged");
    }

    // ── CLR alias (+0x3000) ──────────────────────────────────────────────

    [Fact]
    public void CLR_alias_clears_bits_atomically()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xFFFF_FFFFu);
        _m.Bus.WriteWord(CLR_ADDR, 0x0F0F_F0F0u);    // CLR alias (AND NOT)

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0xFFFF_FFFFu & ~0x0F0F_F0F0u,
            "CLR alias ANDs NOT the written bits");
    }

    [Fact]
    public void CLR_alias_preserves_other_bits()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xA5A5_5A5Au);
        _m.Bus.WriteWord(CLR_ADDR, 0x0F0F_0F0Fu);

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0xA5A5_5A5Au & ~0x0F0F_0F0Fu);
    }

    [Fact]
    public void CLR_alias_with_all_ones_clears_register()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xFFFF_FFFFu);
        _m.Bus.WriteWord(CLR_ADDR, 0xFFFF_FFFFu);    // CLR all-ones = zero

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0u, "CLR all-ones zeroes the register");
    }

    [Fact]
    public void CLR_alias_with_zero_is_noop()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0xDEAD_C0DEu);
        _m.Bus.WriteWord(CLR_ADDR, 0x0u);

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0xDEAD_C0DEu, "CLR with 0 leaves value unchanged");
    }

    // ── Sequence: normal → XOR → SET → CLR ──────────────────────────────

    [Fact]
    public void Sequence_normal_xor_set_clr_produces_expected_result()
    {
        _m.Bus.WriteWord(NORMAL_ADDR, 0x0000_0000u); // start: 0x00000000
        _m.Bus.WriteWord(SET_ADDR,    0xFFFF_0000u); // SET:   0xFFFF0000
        _m.Bus.WriteWord(XOR_ADDR,    0x0F0F_0000u); // XOR:   0xF0F00000
        _m.Bus.WriteWord(CLR_ADDR,    0xF000_0000u); // CLR:   0x00F00000

        _m.Bus.ReadWord(NORMAL_ADDR).Should().Be(0x00F0_0000u, "chained atomic ops produce correct result");
    }
}
