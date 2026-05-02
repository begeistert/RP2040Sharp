using RP2040.Core.Cpu;
using RP2040.Core.Memory;

namespace RP2040.Peripherals.Usb;

/// <summary>
/// RP2040 USB Controller (USBCTRL) — device mode + CDC enumeration support.
/// Memory map (AHB slot 1, bits [27:20] = 0x01):
///   0x50100000 - 0x50100FFF : DPRAM (4 KB)
///   0x50110000 - 0x50110FFF : Controller registers (USBCTRL_REGS)
///
/// The device-side endpoint FSM is sufficient to drive TinyUSB through enumeration
/// and bulk CDC-ACM transfers. Companion host driver lives in <see cref="UsbCdcHost"/>.
/// Equivalent to rp2040js: src/peripherals/usb.ts (device mode subset).
/// </summary>
public sealed class UsbPeripheral : IMemoryMappedDevice
{
    private const int USB_IRQ = 5;

    // ── DPRAM (4 KB) ──────────────────────────────────────────────────────
    private const uint DPRAM_BASE = 0x50100000u;
    private const uint DPRAM_SIZE = 0x1000u;

    // ── REGS base offset from the AHB slot base ───────────────────────────
    private const uint REGS_OFFSET = 0x10000u;

    // ── DPRAM offsets ─────────────────────────────────────────────────────
    private const uint EP1_IN_CONTROL          = 0x008;
    private const uint EP0_IN_BUFFER_CONTROL   = 0x080;
    private const uint EP0_OUT_BUFFER_CONTROL  = 0x084;
    private const uint EP15_OUT_BUFFER_CONTROL = 0x0FC;
    private const uint EP0_BUFFER              = 0x100;

    // EP buffer-control bits
    private const uint USB_BUF_CTRL_AVAILABLE = 1u << 10;
    private const uint USB_BUF_CTRL_FULL      = 1u << 15;
    private const uint USB_BUF_CTRL_LEN_MASK  = 0x3FFu;

    // INTR bits (subset)
    private const uint INTR_BUFF_STATUS  = 1u << 4;
    private const uint INTR_BUS_RESET    = 1u << 12;
    private const uint INTR_DEV_CONN_DIS = 1u << 13;
    private const uint INTR_SETUP_REQ    = 1u << 16;

    // SIE_STATUS bits (subset)
    private const uint SIE_VBUS_DETECTED = 1u << 0;
    private const uint SIE_CONNECTED     = 1u << 16;
    private const uint SIE_SETUP_REC     = 1u << 17;
    private const uint SIE_BUS_RESET     = 1u << 19;

    // MAIN_CTRL bits
    private const uint MAIN_CTRL_CONTROLLER_EN = 1u << 0;
    private const uint MAIN_CTRL_HOST_NDEVICE  = 1u << 1;

    // ── Register offsets within REGS region ──────────────────────────────
    private const uint R_ADDR_ENDP0     = 0x000;
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

    private readonly uint[] _addrEndp = new uint[16];

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
    private uint _usbphyTrim = 0x04040000u;
    private uint _intr;
    private uint _inte;
    private uint _intf;

    private bool _controllerEnabled;
    private bool _hostMode;

    public uint Size => 0x20000u;

    // ── Device-mode callbacks ────────────────────────────────────────────
    /// <summary>Fired the first time the firmware sets MAIN_CTRL.CONTROLLER_EN in device mode.</summary>
    public Action? OnUsbEnabled;
    /// <summary>Fired when firmware acknowledges a bus reset (writes SIE_BUS_RESET as W1C).</summary>
    public Action? OnResetReceived;
    /// <summary>Fired when firmware completes an IN transfer (data flowing device → host).</summary>
    public Action<int, byte[]>? OnEndpointWrite;
    /// <summary>Fired when firmware arms an OUT endpoint (host → device); host should call <see cref="EndpointReadDone"/>.</summary>
    public Action<int, int>? OnEndpointRead;

    public UsbPeripheral(CortexM0Plus? cpu = null)
    {
        _cpu = cpu;
    }

    // ── IMemoryMappedDevice ───────────────────────────────────────────────

    public uint ReadWord(uint address)
    {
        var offset = address & 0x1FFFFu;

        if (offset < DPRAM_SIZE)
            return ReadDpramWord(offset);

        var reg = offset - REGS_OFFSET;
        return reg switch
        {
            var r when r < 0x040 => _addrEndp[r >> 2],

            R_MAIN_CTRL      => _mainCtrl,
            R_SOF_RW         => _sofRw,
            R_SOF_RD         => _sofRw,
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
            var shift = (int)(offset & 1) << 3;
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

        if (offset < DPRAM_SIZE)
        {
            WriteDpramWord(offset & ~3u, value);
            DpramUpdated(offset & ~3u, value);
            return;
        }

        var reg = offset - REGS_OFFSET;
        switch (reg)
        {
            case var r when r < 0x040:
                _addrEndp[r >> 2] = value & 0x07FF_0000u | (value & 0xFFu);
                break;

            case R_MAIN_CTRL:
                _mainCtrl = value & 0xC0000003u;
                _hostMode = (value & MAIN_CTRL_HOST_NDEVICE) != 0;
                if ((value & MAIN_CTRL_CONTROLLER_EN) != 0 && !_controllerEnabled)
                {
                    _controllerEnabled = true;
                    if (!_hostMode) OnUsbEnabled?.Invoke();
                }
                break;
            case R_SOF_RW:         _sofRw    = value & 0x7FFu; break;
            case R_SIE_CTRL:       _sieCtrl  = value; break;
            case R_SIE_STATUS:
                {
                    var clearMask = value;
                    if ((clearMask & SIE_BUS_RESET) != 0 && !_hostMode)
                        OnResetReceived?.Invoke();
                    _sieStatus &= ~clearMask;
                    SieStatusUpdated();
                }
                break;
            case R_INT_EP_CTRL:    _intEpCtrl = value; break;
            case R_BUFF_STATUS:    _buffStatus &= ~value; BuffStatusUpdated(); break;
            case R_BUFF_CPU_SHOULD_HANDLE: _buffCpuShouldHandle &= ~value; break;
            case R_EP_ABORT:       _epAbort   = value; _epAbortDone |= value; break;
            case R_EP_ABORT_DONE:  _epAbortDone &= ~value; break;
            case R_EP_STALL_ARM:   _epStallArm  = value; break;
            case R_NAK_POLL:       _nakPoll     = value; break;
            case R_EP_STATUS_STALL_NAK: _epStatusStallNak &= ~value; break;
            case R_USB_MUXING:     _usbMuxing   = value;
                // pico-sdk hw_enumeration_fix waits for SIE_CONNECTED after rerouting muxing
                if ((value & 0b0100) != 0 && (value & 0b0001) == 0)
                    _sieStatus |= SIE_CONNECTED;
                break;
            case R_USB_PWR:        _usbPwr      = value;
                // VBUS detect override
                if ((value & (1u << 2)) != 0)
                {
                    if ((value & (1u << 3)) != 0) _sieStatus |= SIE_VBUS_DETECTED;
                    else                          _sieStatus &= ~SIE_VBUS_DETECTED;
                }
                break;
            case R_USBPHY_DIRECT:  _usbphyDirect = value; break;
            case R_USBPHY_DIRECT_OVERRIDE: _usbphyDirectOverride = value; break;
            case R_USBPHY_TRIM:    _usbphyTrim   = value; break;
            case R_INTR:           _intr &= ~value; CheckInterrupts(); break;
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
            // EP buffer-control writes can be 16-bit too; route through the 32-bit path
            var alignedW = offset & ~3u;
            DpramUpdated(alignedW, ReadDpramWord(alignedW));
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
            var alignedW = offset & ~3u;
            DpramUpdated(alignedW, ReadDpramWord(alignedW));
            return;
        }
        var aligned = address & ~3u;
        var shift   = (int)((address & 3) << 3);
        var current = ReadWord(aligned);
        WriteWord(aligned, (current & ~(0xFFu << shift)) | ((uint)value << shift));
    }

    // ── Public host-side helpers ─────────────────────────────────────────

    /// <summary>Simulate a bus reset received by the device. Sets BUS_RESET in SIE_STATUS.</summary>
    public void SignalBusReset()
    {
        _sieStatus |= SIE_BUS_RESET | SIE_CONNECTED;
        SieStatusUpdated();
    }

    /// <summary>Simulate an isolated SETUP_REC bit assertion (no payload).</summary>
    public void SignalSetupPacket()
    {
        _sieStatus |= SIE_SETUP_REC;
        SieStatusUpdated();
    }

    /// <summary>Inject an 8-byte SETUP packet into DPRAM[0..8] and raise SETUP_REC.</summary>
    public void SendSetupPacket(ReadOnlySpan<byte> setup)
    {
        if (setup.Length != 8) throw new ArgumentException("SETUP packet must be 8 bytes", nameof(setup));
        setup.CopyTo(_dpram.AsSpan(0, 8));
        _sieStatus |= SIE_SETUP_REC;
        SieStatusUpdated();
    }

    /// <summary>Provide data for an OUT endpoint that the firmware previously armed.</summary>
    public void EndpointReadDone(int endpoint, ReadOnlySpan<byte> data)
    {
        var bufCtrlReg = EP0_OUT_BUFFER_CONTROL + (uint)endpoint * 8;
        var bufCtrl = ReadDpramWord(bufCtrlReg);
        var requestedLen = (int)(bufCtrl & USB_BUF_CTRL_LEN_MASK);
        var newLen = Math.Min(data.Length, requestedLen);

        var bufferOffset = GetEndpointBufferOffset(endpoint, out_: true);
        data[..newLen].CopyTo(_dpram.AsSpan((int)bufferOffset, newLen));

        bufCtrl |= USB_BUF_CTRL_FULL;
        bufCtrl = (bufCtrl & ~USB_BUF_CTRL_LEN_MASK) | ((uint)newLen & USB_BUF_CTRL_LEN_MASK);
        WriteDpramWord(bufCtrlReg, bufCtrl);

        IndicateBufferReady(endpoint, isOut: true);
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

    // ── DPRAM endpoint FSM ───────────────────────────────────────────────

    private void DpramUpdated(uint offset, uint value)
    {
        if (_hostMode) return;
        if ((value & USB_BUF_CTRL_AVAILABLE) == 0) return;
        if (offset < EP0_IN_BUFFER_CONTROL || offset > EP15_OUT_BUFFER_CONTROL) return;

        var endpoint = (int)((offset - EP0_IN_BUFFER_CONTROL) >> 3);
        var isOut = (offset & 4) != 0;
        var bufLen = (int)(value & USB_BUF_CTRL_LEN_MASK);
        var bufferOffset = GetEndpointBufferOffset(endpoint, isOut);

        // Consume AVAILABLE flag
        value &= ~USB_BUF_CTRL_AVAILABLE;

        if (isOut)
        {
            WriteDpramWord(offset, value);
            OnEndpointRead?.Invoke(endpoint, bufLen);
        }
        else
        {
            // IN: data flows device → host. Capture buffer, clear FULL, indicate ready.
            value &= ~USB_BUF_CTRL_FULL;
            WriteDpramWord(offset, value);
            var buffer = new byte[bufLen];
            _dpram.AsSpan((int)bufferOffset, bufLen).CopyTo(buffer);
            IndicateBufferReady(endpoint, isOut: false);
            OnEndpointWrite?.Invoke(endpoint, buffer);
        }
    }

    private uint GetEndpointBufferOffset(int endpoint, bool out_)
    {
        if (endpoint == 0) return EP0_BUFFER;
        var ctrlOffset = EP1_IN_CONTROL + 8u * (uint)(endpoint - 1) + (out_ ? 4u : 0u);
        return ReadDpramWord(ctrlOffset) & 0xFFC0u;
    }

    private void IndicateBufferReady(int endpoint, bool isOut)
    {
        _buffStatus |= 1u << (endpoint * 2 + (isOut ? 1 : 0));
        BuffStatusUpdated();
    }

    private void BuffStatusUpdated()
    {
        if (_buffStatus != 0) _intr |= INTR_BUFF_STATUS;
        else                  _intr &= ~INTR_BUFF_STATUS;
        CheckInterrupts();
    }

    private void SieStatusUpdated()
    {
        // Map SIE_STATUS bits to INTR bits (device-mode subset).
        SyncIntrBit(SIE_SETUP_REC, INTR_SETUP_REQ);
        SyncIntrBit(SIE_BUS_RESET, INTR_BUS_RESET);
        SyncIntrBit(SIE_CONNECTED, INTR_DEV_CONN_DIS);
        CheckInterrupts();
    }

    private void SyncIntrBit(uint sieBit, uint intrBit)
    {
        if ((_sieStatus & sieBit) != 0) _intr |= intrBit;
        else                            _intr &= ~intrBit;
    }

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
