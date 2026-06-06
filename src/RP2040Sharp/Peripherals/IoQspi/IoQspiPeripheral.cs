using RP2040.Core.Memory;
using RP2040.Peripherals.Ssi;

namespace RP2040.Peripherals.IoQspi;

/// <summary>
/// IO_QSPI peripheral stub (0x40018000).
/// Controls the QSPI GPIO pins (SCLK, SS, SD0-SD3). Stores FUNCSEL/CTRL
/// registers but has no electrical simulation.
///
/// The SS pin (pin 1) OUTOVER field is monitored: OUTOVER=2 (drive low)
/// asserts the QSPI chip-select and OUTOVER≠2 (after a drive-low) deasserts
/// it. These transitions are forwarded to <see cref="SsiPeripheral"/> so
/// the SSI can delimit flash command transactions.
/// </summary>
public sealed class IoQspiPeripheral : IMemoryMappedDevice
{
    // 6 QSPI GPIO pins: SCLK, SS, SD0, SD1, SD2, SD3
    // Each pin: STATUS (0, RO) + CTRL (4, R/W) → stride 8 bytes
    private const int PIN_COUNT = 6;
    private const int PIN_SS    = 1;   // SS = chip-select pin index

    // IO_QSPI OUTOVER values (bits [9:8] of each pin's CTRL register)
    private const uint OUTOVER_DRIVE_LOW = 2u;
    private const int  OUTOVER_SHIFT     = 8;   // shift count must be int in C#
    private const uint OUTOVER_MASK      = 3u << 8;

    private readonly uint[] _ctrl = new uint[PIN_COUNT];

    // SSI peripheral to notify on CS assert/deassert
    private SsiPeripheral? _ssi;

    public uint Size => 0x1000;

    /// <summary>
    /// Wire this peripheral to the SSI so SS CTRL OUTOVER changes propagate
    /// as CS assert/deassert signals.
    /// </summary>
    public void AttachSsi(SsiPeripheral ssi) => _ssi = ssi;

    public uint ReadWord(uint address)
    {
        var pin = (int)(address >> 3) & 0x1F;
        if (pin >= PIN_COUNT) return 0;
        var field = address & 7;
        return field switch
        {
            // STATUS register: report INFROMPAD (bit 17) and INTOPERI (bit 19) as HIGH
            // for all QSPI pins. Bit 17 is the critical one: the bootrom reads
            // GPIO_QSPI_SS_STATUS.INFROMPAD to detect whether the BOOTSEL button is
            // pressed (active-low). A zero would mean "BOOTSEL held → USB BOOTSEL mode".
            0 => 0x000A0000u,  // INFROMPAD=1 (bit17), INTOPERI=1 (bit19)
            4 => _ctrl[pin],
            _ => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        var pin = (int)(address >> 3) & 0x1F;
        if (pin >= PIN_COUNT) return;
        if ((address & 7) == 4)
        {
            var prev = _ctrl[pin];
            _ctrl[pin] = value;

            // Monitor the SS pin (pin 1) OUTOVER field [9:8].
            // OUTOVER=2 (DRIVE_LOW) → CS asserted (flash_cs_force(low)).
            // Any other value after DRIVE_LOW → CS deasserted.
            if (pin == PIN_SS && _ssi != null)
            {
                var prevOutover = (prev  & OUTOVER_MASK) >> OUTOVER_SHIFT;
                var newOutover  = (value & OUTOVER_MASK) >> OUTOVER_SHIFT;
                if (prevOutover != newOutover)
                {
                    if (newOutover == OUTOVER_DRIVE_LOW)
                        _ssi.OnCsAssert();
                    else if (prevOutover == OUTOVER_DRIVE_LOW)
                        _ssi.OnCsDeassert();
                }
            }
        }
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 2) << 3);
        WriteWord(aligned, (ReadWord(aligned) & ~(0xFFFFu << shift)) | ((uint)value << shift));
    }

    public void WriteByte(uint address, byte value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 3) << 3);
        WriteWord(aligned, (ReadWord(aligned) & ~(0xFFu << shift)) | ((uint)value << shift));
    }
}
