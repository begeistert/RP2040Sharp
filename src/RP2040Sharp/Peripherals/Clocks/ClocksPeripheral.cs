using RP2040.Core.Memory;

namespace RP2040.Peripherals.Clocks;

/// <summary>
/// Clocks peripheral (0x40008000).
/// Manages 8 clock domains. In simulation all clocks run at their default
/// frequencies — the peripheral stores register writes and returns SELECTED=1
/// so firmware clock-init sequences complete without spinning.
/// </summary>
public sealed class ClocksPeripheral : IMemoryMappedDevice
{
    // ── Register offsets ────────────────────────────────────────────────
    // CLK_GPOUT0..3 (CTRL/DIV/SELECTED, stride 0x0C)
    private const uint CLK_GPOUT0_CTRL     = 0x000;
    private const uint CLK_GPOUT0_DIV      = 0x004;
    private const uint CLK_GPOUT0_SELECTED = 0x008;
    // ... up to GPOUT3 at 0x024/0x028/0x02C
    private const uint CLK_REF_CTRL        = 0x030;
    private const uint CLK_REF_DIV         = 0x034;
    private const uint CLK_REF_SELECTED    = 0x038;
    private const uint CLK_SYS_CTRL        = 0x03C;
    private const uint CLK_SYS_DIV         = 0x040;
    private const uint CLK_SYS_SELECTED    = 0x044;
    private const uint CLK_PERI_CTRL       = 0x048;
    private const uint CLK_PERI_SELECTED   = 0x050;
    private const uint CLK_USB_CTRL        = 0x054;
    private const uint CLK_USB_DIV         = 0x058;
    private const uint CLK_USB_SELECTED    = 0x05C;
    private const uint CLK_ADC_CTRL        = 0x060;
    private const uint CLK_ADC_DIV         = 0x064;
    private const uint CLK_ADC_SELECTED    = 0x068;
    private const uint CLK_RTC_CTRL        = 0x06C;
    private const uint CLK_RTC_DIV         = 0x070;
    private const uint CLK_RTC_SELECTED    = 0x074;
    private const uint CLK_SYS_RESUS_CTRL   = 0x078;
    private const uint CLK_SYS_RESUS_STATUS = 0x07C;
    private const uint FC0_REF_KHZ          = 0x080;
    private const uint FC0_MIN_KHZ          = 0x084;
    private const uint FC0_MAX_KHZ          = 0x088;
    private const uint FC0_DELAY            = 0x08C;
    private const uint FC0_INTERVAL         = 0x090;
    private const uint FC0_SRC             = 0x094;
    private const uint FC0_STATUS          = 0x098;
    private const uint FC0_RESULT          = 0x09C;
    private const uint WAKE_EN0            = 0x0A0;
    private const uint WAKE_EN1            = 0x0A4;
    private const uint SLEEP_EN0           = 0x0A8;
    private const uint SLEEP_EN1           = 0x0AC;
    private const uint ENABLED0            = 0x0B0;
    private const uint ENABLED1            = 0x0B4;
    private const uint INTR               = 0x0B8;
    private const uint INTE               = 0x0BC;
    private const uint INTF               = 0x0C0;
    private const uint INTS               = 0x0C4;

    // We store ctrl/div for each domain index (0=gpout0..3, 4=ref, 5=sys, 6=peri, 7=usb, 8=adc, 9=rtc)
    private readonly uint[] _ctrl     = new uint[10];
    private readonly uint[] _div      = new uint[10];
    private uint _resusCtrl;
    private uint _fc0Src;
    private uint _wakeEn0 = 0xFFFFFFFF, _wakeEn1 = 0xFFFF;
    private uint _sleepEn0 = 0xFFFFFFFF, _sleepEn1 = 0xFFFF;
    private uint _inte;

    // Default divider = 1.0 (integer=1, frac=0 → top byte = 0x01, rest 0 → 0x01000000)
    private const uint DIV_DEFAULT = 0x01000000;

    public ClocksPeripheral()
    {
        for (int i = 0; i < _div.Length; i++)
            _div[i] = DIV_DEFAULT;
    }

    public uint Size => 0x1000;

    public uint ReadWord(uint address)
    {
        return address switch
        {
            // GPOUTn: stride 0x0C, base 0x000
            var a when a >= 0x000 && a <= 0x02C =>
                ReadClockDomain((a / 0x0C), (a % 0x0C)),
            CLK_REF_CTRL        => _ctrl[4],
            CLK_REF_DIV         => _div[4],
            CLK_REF_SELECTED    => 1u,   // glitchless mux: clock selected
            CLK_SYS_CTRL        => _ctrl[5],
            CLK_SYS_DIV         => _div[5],
            CLK_SYS_SELECTED    => 1u,
            CLK_PERI_CTRL       => _ctrl[6],
            CLK_PERI_SELECTED   => 1u,
            CLK_USB_CTRL        => _ctrl[7],
            CLK_USB_DIV         => _div[7],
            CLK_USB_SELECTED    => 1u,
            CLK_ADC_CTRL        => _ctrl[8],
            CLK_ADC_DIV         => _div[8],
            CLK_ADC_SELECTED    => 1u,
            CLK_RTC_CTRL        => _ctrl[9],
            CLK_RTC_DIV         => _div[9],
            CLK_RTC_SELECTED    => 1u,
            CLK_SYS_RESUS_CTRL   => _resusCtrl,
            CLK_SYS_RESUS_STATUS => 0,   // no resuscitation needed
            FC0_SRC             => _fc0Src,
            FC0_STATUS          => 0x10, // FC_DONE
            FC0_RESULT          => 125_000 << 5,  // 125 MHz: KHZ field at bits[28:5], so 125000 << 5
            WAKE_EN0            => _wakeEn0,
            WAKE_EN1            => _wakeEn1,
            SLEEP_EN0           => _sleepEn0,
            SLEEP_EN1           => _sleepEn1,
            ENABLED0            => 0xFFFFFFFF,
            ENABLED1            => 0xFFFF,
            INTR                => 0,
            INTE                => _inte,
            INTF                => 0,
            INTS                => 0,
            _                   => 0,
        };
    }

    private uint ReadClockDomain(uint domain, uint field) => field switch
    {
        0x00 => _ctrl[domain],
        0x04 => _div[domain],
        0x08 => 1u,   // SELECTED always 1
        _    => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case var a when a >= 0x000 && a <= 0x02C:
                WriteClockDomain(a / 0x0Cu, a % 0x0Cu, value); break;
            case CLK_REF_CTRL:      _ctrl[4] = value; break;
            case CLK_REF_DIV:       _div[4]  = value; break;
            case CLK_SYS_CTRL:      _ctrl[5] = value; break;
            case CLK_SYS_DIV:       _div[5]  = value; break;
            case CLK_PERI_CTRL:     _ctrl[6] = value; break;
            case CLK_USB_CTRL:      _ctrl[7] = value; break;
            case CLK_USB_DIV:       _div[7]  = value; break;
            case CLK_ADC_CTRL:      _ctrl[8] = value; break;
            case CLK_ADC_DIV:       _div[8]  = value; break;
            case CLK_RTC_CTRL:      _ctrl[9] = value; break;
            case CLK_RTC_DIV:       _div[9]  = value; break;
            case CLK_SYS_RESUS_CTRL: _resusCtrl = value; break;
            case FC0_SRC:           _fc0Src = value; break;
            case WAKE_EN0:          _wakeEn0 = value; break;
            case WAKE_EN1:          _wakeEn1 = value; break;
            case SLEEP_EN0:         _sleepEn0 = value; break;
            case SLEEP_EN1:         _sleepEn1 = value; break;
            case INTE:              _inte = value; break;
        }
    }

    private void WriteClockDomain(uint domain, uint field, uint value)
    {
        switch (field)
        {
            case 0x00: _ctrl[domain] = value; break;
            case 0x04: _div[domain]  = value; break;
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
