using RP2040.Core.Cpu;
using RP2040.Core.Memory;
using RP2040.Peripherals.Timer;

namespace RP2040.Peripherals.Tests.Timer;

/// <summary>
/// Tests for the RP2040 Timer peripheral (§4.6 of RP2040 TRM).
/// The Timer counts microseconds at 125 MHz.
/// </summary>
public abstract class TimerTests
{
    private const uint CLK_HZ = 125_000_000;

    private sealed class Fixture : IDisposable
    {
        public BusInterconnect Bus { get; }
        public CortexM0Plus Cpu { get; }
        public TimerPeripheral Timer { get; }

        public Fixture()
        {
            Bus = new BusInterconnect();
            Cpu = new CortexM0Plus(Bus);
            Timer = new TimerPeripheral(Cpu, CLK_HZ);
        }

        public void Dispose() => Bus.Dispose();

        /// <summary>Advance the timer by the given number of microseconds.</summary>
        public void AdvanceMicros(long us) => Timer.Tick(us * CLK_HZ / 1_000_000);
    }

    private const uint ALARM0   = 0x010;
    private const uint ALARM1   = 0x014;
    private const uint ALARM2   = 0x018;
    private const uint ALARM3   = 0x01C;
    private const uint ARMED    = 0x020;
    private const uint TIMERAWL = 0x028;
    private const uint INTR     = 0x034;
    private const uint INTE     = 0x038;
    private const uint INTF     = 0x03C;
    private const uint INTS     = 0x040;

    public class AlarmBasic
    {
        [Fact]
        public void Writing_alarm0_arms_it()
        {
            using var f = new Fixture();
            f.Timer.WriteWord(ALARM0, 1000u);
            (f.Timer.ReadWord(ARMED) & 1u).Should().Be(1u, "alarm0 should be armed");
        }

        [Fact]
        public void Alarm_fires_after_deadline()
        {
            using var f = new Fixture();
            var target = (uint)f.Timer.ReadWord(TIMERAWL) + 500u;
            f.Timer.WriteWord(ALARM0, target);

            f.AdvanceMicros(600);

            (f.Timer.ReadWord(INTR) & 1u).Should().Be(1u, "alarm0 raw interrupt must be set");
        }

        [Fact]
        public void Alarm_disarms_when_fired()
        {
            using var f = new Fixture();
            var target = (uint)f.Timer.ReadWord(TIMERAWL) + 100u;
            f.Timer.WriteWord(ALARM0, target);
            f.AdvanceMicros(200);

            f.Timer.ReadWord(ARMED).Should().Be(0u, "alarm0 should auto-disarm after firing");
        }

        [Fact]
        public void Alarm_INTR_cleared_by_writing_1()
        {
            using var f = new Fixture();
            var target = (uint)f.Timer.ReadWord(TIMERAWL) + 50u;
            f.Timer.WriteWord(ALARM0, target);
            f.AdvanceMicros(100);

            // Verify it fired
            (f.Timer.ReadWord(INTR) & 1u).Should().Be(1u);

            // Clear it
            f.Timer.WriteWord(INTR, 1u);
            f.Timer.ReadWord(INTR).Should().Be(0u, "INTR should be cleared after writing 1");
        }
    }

    public class InterruptRouting
    {
        [Fact]
        public void INTS_reflects_INTR_when_INTE_enabled()
        {
            using var f = new Fixture();
            f.Timer.WriteWord(INTE, 0xFu);  // enable all 4 alarm interrupts
            var target = (uint)f.Timer.ReadWord(TIMERAWL) + 50u;
            f.Timer.WriteWord(ALARM1, target);
            f.AdvanceMicros(100);

            var ints = f.Timer.ReadWord(INTS);
            (ints & 0x2u).Should().Be(0x2u, "INTS bit1 (alarm1) should be set");
        }

        [Fact]
        public void INTF_force_shows_in_INTS_when_INTE_set()
        {
            using var f = new Fixture();
            f.Timer.WriteWord(INTE, 0x4u);  // enable alarm2
            f.Timer.WriteWord(INTF, 0x4u);  // force alarm2

            var ints = f.Timer.ReadWord(INTS);
            (ints & 0x4u).Should().Be(0x4u, "forced interrupt should appear in INTS when INTE enabled");
        }

        [Fact]
        public void INTF_does_not_show_in_INTS_when_INTE_disabled()
        {
            using var f = new Fixture();
            // INTE stays 0 (default)
            f.Timer.WriteWord(INTF, 0x4u);  // force alarm2

            f.Timer.ReadWord(INTS).Should().Be(0u, "forced interrupt hidden when INTE=0");
        }
    }

    public class MultipleAlarms
    {
        [Fact]
        public void All_four_alarms_can_fire_independently()
        {
            using var f = new Fixture();
            var now = f.Timer.ReadWord(TIMERAWL);
            f.Timer.WriteWord(ALARM0, now + 100u);
            f.Timer.WriteWord(ALARM1, now + 200u);
            f.Timer.WriteWord(ALARM2, now + 300u);
            f.Timer.WriteWord(ALARM3, now + 400u);

            f.AdvanceMicros(500);

            (f.Timer.ReadWord(INTR) & 0xFu).Should().Be(0xFu, "all four alarms should have fired");
        }

        [Fact]
        public void Alarm_that_hasnt_elapsed_does_not_fire()
        {
            using var f = new Fixture();
            var now = f.Timer.ReadWord(TIMERAWL);
            f.Timer.WriteWord(ALARM0, now + 1000u);

            f.AdvanceMicros(100);

            f.Timer.ReadWord(INTR).Should().Be(0u, "alarm should not fire before deadline");
        }
    }
}
