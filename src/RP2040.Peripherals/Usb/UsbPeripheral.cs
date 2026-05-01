using RP2040.Core.Cpu;
using RP2040.Core.Memory;

namespace RP2040.Peripherals.Usb;

/// <summary>
/// RP2040 USB Controller (USBCTRL).
/// Memory map (AHB slot 1, bits [27:20] = 0x01):
///   0x50100000 - 0x50100FFF : DPRAM (4 KB)
///   0x50110000 - 0x50110FFF : Controller registers (USBCTRL_REGS)
/// Both regions arrive here because AhbBridge dispatches by 1 MB block.
/// </summary>
public sealed class UsbPeripheral : IMemoryMappedDevice
{
    // ── IRQ ──────────────────────────────────────────────────────────────
    private const int USB_IRQ = 5;

    // ── DPRAM (4 KB) ──────────────────────────────────────────────────────
    private const uint DPRAM_BASE   = 0x50100000u;
    private const uint DPRAM_SIZE   = 0x1000u;       // 4 KB

    // ── REGS base offset from the AHB slot base ───────────────────────────
    private const uint REGS_OFFSET  = 0x10000u;       // 0x50110000 - 0x50100000

    // ── Register offsets within REGS region ──────────────────────────────
    private const uint R_ADDR_ENDP0     = 0x000;
    // 0x004-0x03C: ADDR_ENDP1-15 (each 4 bytes, starting at 0x004)
    private const uint R_MAIN_CTRL      = 0x040;
    private const uint R_SOF_RW         = 0x044;
    private const uint R_SOF_RD         = 0x048;
    private const uint R_SIE_CTRL       = 0x04C;
    private const uint R_SIE_STATUS     = 0x050;
    private const uint R_INT_EP_CTRL    = 0x054;
    private const uint R_BUFF_STATUS    = 0x058;
    private const uint R_BUFF_CPU_SHOULD_HANDLE = 0x05C;
    private const uint R_EP_ABORT       = 0x060;
    private const uint R_EP_ABORT_DONE  = 0x064;
    private const uint R_EP_STALL_ARM   = 0x068;
    private const uint R_NAK_POLL       = 0x06C;
    private const uint R_EP_STATUS_STALL_NAK = 0x070;
    private const uint R_USB_MUXING     = 0x074;
    private const uint R_USB_PWR        = 0x078;
    private const uint R_USBPHY_DIRECT  = 0x07C;
    private const uint R_USBPHY_DIRECT_OVERRIDE = 0x080;
    private const uint R_USBPHY_TRIM    = 0x084;
    private const uint R_INTR           = 0x08C;
    private const uint R_INTE           = 0x090;
    private const uint R_INTF           = 0x094;
    private const uint R_INTS           = 0x098;

    // ── Fields ────────────────────────────────────────────────────────────
    private readonly CortexM0Plus? _cpu;
    private readonly byte[] _dpram = new byte[DPRAM_SIZE];

    // ADDR_ENDP registers: 16 endpoints × 4 bytes
    private readonly uint[] _addrEndp  = new uint[16];

    // Controller registers
    private uint _mainCtrl;
    private uint _sofRw;
    private uint _sieCtrl;
    private uint _sieStatus;
    private uint _intEpCtrl;
    private uint _buffStatus;
    private uint _buffCpuShouldHandle;
    private uint _epAbort;
    private uint _epAbortDone;
    private uint _epStallArm;
    private uint _nakPoll;
    private uint _epStatusStallNak;
    private uint _usbMuxing;
    private uint _usbPwr;
    private uint _usbphyDirect;
    private uint _usbphyDirectOverride;
    private uint _usbphyTrim  = 0x04040000u; // default TRIM values
    private uint _intr;
    private uint _inte;
    private uint _intf;

    public uint Size => 0x20000u; // covers both DPRAM and REGS within same AHB slot

    public UsbPeripheral(CortexM0Plus? cpu = null)
    {
        _cpu = cpu;
    }

    // ── IMemoryMappedDevice ───────────────────────────────────────────────

    public uint ReadWord(uint address)
    {
        var offset = address & 0x1FFFFu; // strip region/atomic bits

        // DPRAM region
        if (offset < DPRAM_SIZE)
            return ReadDpramWord(offset);

        // REGS region
        var reg = offset - REGS_OFFSET;
        return reg switch
        {
            // ADDR_ENDP0-15: offsets 0x000-0x03C (every 4 bytes)
            var r when r < 0x040 => _addrEndp[r >> 2],

            R_MAIN_CTRL      => _mainCtrl,
            R_SOF_RW         => _sofRw,
            R_SOF_RD         => _sofRw,    // read returns same counter
            R_SIE_CTRL       => _sieCtrl,
            R_SIE_STATUS     => _sieStatus,
            R_INT_EP_CTRL    => _intEpCtrl,
            R_BUFF_STATUS    => _buffStatus,
            R_BUFF_CPU_SHOULD_HANDLE => _buffCpuShouldHandle,
            R_EP_ABORT       => _epAbort,
            R_EP_ABORT_DONE  => _epAbortDone,
            R_EP_STALL_ARM   => _epStallArm,
            R_NAK_POLL       => _nakPoll,
            R_EP_STATUS_STALL_NAK => _epStatusStallNak,
            R_USB_MUXING     => _usbMuxing,
            R_USB_PWR        => _usbPwr,
            R_USBPHY_DIRECT  => _usbphyDirect,
            R_USBPHY_DIRECT_OVERRIDE => _usbphyDirectOverride,
            R_USBPHY_TRIM    => _usbphyTrim,
            R_INTR           => _intr,
            R_INTE           => _inte,
            R_INTF           => _intf,
            R_INTS           => (_intr | _intf) & _inte,
            _                => 0u,
        };
    }

    public ushort ReadHalfWord(uint address)
    {
        var offset = address & 0x1FFFFu;
        if (offset < DPRAM_SIZE)
        {
            var aligned = offset & ~1u;
            var shift   = (int)(offset & 1) << 3;
            return (ushort)(ReadDpramWord(aligned & ~3u) >> shift);
        }
        return (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));
    }

    public byte ReadByte(uint address)
    {
        var offset = address & 0x1FFFFu;
        if (offset < DPRAM_SIZE)
            return _dpram[offset];
        return (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));
    }

    public void WriteWord(uint address, uint value)
    {
        var offset = address & 0x1FFFFu;

        // DPRAM region
        if (offset < DPRAM_SIZE)
        {
            WriteDpramWord(offset & ~3u, value);
            return;
        }

        // REGS region
        var reg = offset - REGS_OFFSET;
        switch (reg)
        {
            // ADDR_ENDP0-15
            case var r when r < 0x040:
                _addrEndp[r >> 2] = value & 0x07FF_0000u | (value & 0xFFu);
                break;

            case R_MAIN_CTRL:      _mainCtrl = value & 0xC0000003u; break;
            case R_SOF_RW:         _sofRw    = value & 0x7FFu; break;
            case R_SIE_CTRL:       _sieCtrl  = value; break;
            case R_SIE_STATUS:     _sieStatus &= ~value; CheckInterrupts(); break; // W1C
            case R_INT_EP_CTRL:    _intEpCtrl = value; break;
            case R_BUFF_STATUS:    _buffStatus &= ~value; CheckInterrupts(); break; // W1C
            case R_BUFF_CPU_SHOULD_HANDLE: _buffCpuShouldHandle &= ~value; break; // W1C
            case R_EP_ABORT:       _epAbort   = value; break;
            case R_EP_ABORT_DONE:  _epAbortDone &= ~value; break; // W1C
            case R_EP_STALL_ARM:   _epStallArm  = value; break;
            case R_NAK_POLL:       _nakPoll     = value; break;
            case R_EP_STATUS_STALL_NAK: _epStatusStallNak = value; break;
            case R_USB_MUXING:     _usbMuxing   = value; break;
            case R_USB_PWR:        _usbPwr      = value; break;
            case R_USBPHY_DIRECT:  _usbphyDirect = value; break;
            case R_USBPHY_DIRECT_OVERRIDE: _usbphyDirectOverride = value; break;
            case R_USBPHY_TRIM:    _usbphyTrim   = value; break;
            case R_INTR:           _intr &= ~value; CheckInterrupts(); break; // W1C
            case R_INTE:           _inte = value; CheckInterrupts(); break;
            case R_INTF:           _intf = value; CheckInterrupts(); break;
        }
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        var offset = address & 0x1FFFFu;
        if (offset < DPRAM_SIZE)
        {
            _dpram[offset & ~1u]     = (byte)(value & 0xFF);
            _dpram[(offset & ~1u)+1] = (byte)(value >> 8);
            return;
        }
        var aligned = address & ~3u;
        var shift   = (int)((address & 2) << 3);
        var current = ReadWord(aligned);
        WriteWord(aligned, (current & ~(0xFFFFu << shift)) | ((uint)value << shift));
    }

    public void WriteByte(uint address, byte value)
    {
        var offset = address & 0x1FFFFu;
        if (offset < DPRAM_SIZE)
        {
            _dpram[offset] = value;
            return;
        }
        var aligned = address & ~3u;
        var shift   = (int)((address & 3) << 3);
        var current = ReadWord(aligned);
        WriteWord(aligned, (current & ~(0xFFu << shift)) | ((uint)value << shift));
    }

    // ── Public helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Simulate a USB bus reset received by the device.
    /// Sets the BUS_RESET bit in SIE_STATUS and fires the interrupt.
    /// </summary>
    public void SignalBusReset()
    {
        _sieStatus |= 1u << 12;   // BUS_RESET
        _intr      |= 1u << 12;
        CheckInterrupts();
    }

    /// <summary>
    /// Simulate a setup packet arriving at EP0.
    /// Sets SETUP_REQ in SIE_STATUS.
    /// </summary>
    public void SignalSetupPacket()
    {
        _sieStatus |= 1u << 17;   // SETUP_REC
        _intr      |= 1u << 17;
        CheckInterrupts();
    }

    /// <summary>Copy <paramref name="data"/> into DPRAM at <paramref name="dpramOffset"/>.</summary>
    public void WriteDpram(uint dpramOffset, ReadOnlySpan<byte> data)
    {
        if (dpramOffset + data.Length > DPRAM_SIZE)
            throw new ArgumentOutOfRangeException(nameof(dpramOffset));
        data.CopyTo(_dpram.AsSpan((int)dpramOffset));
    }

    /// <summary>Read <paramref name="length"/> bytes from DPRAM at <paramref name="dpramOffset"/>.</summary>
    public byte[] ReadDpram(uint dpramOffset, int length)
    {
        var result = new byte[length];
        _dpram.AsSpan((int)dpramOffset, length).CopyTo(result);
        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private uint ReadDpramWord(uint byteOffset)
    {
        var i = (int)(byteOffset & ~3u);
        return (uint)(_dpram[i] | (_dpram[i+1] << 8) | (_dpram[i+2] << 16) | (_dpram[i+3] << 24));
    }

    private void WriteDpramWord(uint byteOffset, uint value)
    {
        var i = (int)(byteOffset & ~3u);
        _dpram[i]   = (byte) value;
        _dpram[i+1] = (byte)(value >>  8);
        _dpram[i+2] = (byte)(value >> 16);
        _dpram[i+3] = (byte)(value >> 24);
    }

    private void CheckInterrupts()
    {
        if (_cpu == null) return;
        _cpu.SetInterrupt(USB_IRQ, ((_intr | _intf) & _inte) != 0);
    }
}
