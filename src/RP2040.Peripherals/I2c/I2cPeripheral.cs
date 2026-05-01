using RP2040.Core.Memory;

namespace RP2040.Peripherals.I2c;

/// <summary>
/// RP2040 I2C peripheral (DesignWare DW_apb_i2c).
/// I2C0 base: 0x40044000, I2C1 base: 0x40048000.
/// Transfer simulation via injectable callbacks.
/// </summary>
public sealed class I2cPeripheral : IMemoryMappedDevice
{
    private const uint IC_CON            = 0x000;
    private const uint IC_TAR            = 0x004;
    private const uint IC_DATA_CMD       = 0x010;
    private const uint IC_SS_SCL_HCNT   = 0x014;
    private const uint IC_SS_SCL_LCNT   = 0x018;
    private const uint IC_FS_SCL_HCNT   = 0x01C;
    private const uint IC_FS_SCL_LCNT   = 0x020;
    private const uint IC_INTR_STAT     = 0x02C;
    private const uint IC_INTR_MASK     = 0x030;
    private const uint IC_RAW_INTR_STAT = 0x034;
    private const uint IC_RX_TL         = 0x038;
    private const uint IC_TX_TL         = 0x03C;
    private const uint IC_CLR_INTR      = 0x040;
    private const uint IC_CLR_RD_REQ    = 0x050;
    private const uint IC_CLR_TX_ABRT   = 0x054;
    private const uint IC_CLR_STOP_DET  = 0x060;
    private const uint IC_CLR_START_DET = 0x064;
    private const uint IC_ENABLE        = 0x06C;
    private const uint IC_STATUS        = 0x070;
    private const uint IC_TXFLR         = 0x074;
    private const uint IC_RXFLR         = 0x078;
    private const uint IC_SDA_HOLD      = 0x07C;
    private const uint IC_TX_ABRT_SOURCE= 0x080;
    private const uint IC_ENABLE_STATUS = 0x09C;
    private const uint IC_COMP_PARAM_1  = 0x0F4;
    private const uint IC_COMP_VERSION  = 0x0F8;
    private const uint IC_COMP_TYPE     = 0x0FC;

    // IC_STATUS bits
    private const uint ST_ACTIVITY  = 1u << 0;
    private const uint ST_TFNF      = 1u << 1;  // TX FIFO not full
    private const uint ST_TFE       = 1u << 2;  // TX FIFO empty
    private const uint ST_RFNE      = 1u << 3;  // RX FIFO not empty
    private const uint ST_RFF       = 1u << 4;  // RX FIFO full
    private const uint ST_MST_ACTV  = 1u << 5;  // Master FSM active

    private const int FIFO_DEPTH = 16;

    private uint _con  = 0x65;  // default: master, 7-bit, fast-mode enabled, restart enabled
    private uint _tar;
    private uint _intrMask = 0x8FF;
    private uint _rawIntr;
    private uint _rxTl;
    private uint _txTl;
    private uint _enable;
    private uint _sdaHold = 0x1;

    private readonly Queue<byte> _rxFifo = new(FIFO_DEPTH);

    /// <summary>Called on each byte write: (targetAddress, data).</summary>
    public Action<byte, byte>? OnWrite;

    /// <summary>Called on each byte read request: (targetAddress) → rx byte.</summary>
    public Func<byte, byte>? OnRead;

    public uint Size => 0x1000;

    // ── IMemoryMappedDevice ──────────────────────────────────────────

    public uint ReadWord(uint address)
    {
        return address switch
        {
            IC_CON            => _con,
            IC_TAR            => _tar,
            IC_DATA_CMD       => PopRxFifo(),
            IC_SS_SCL_HCNT    => 0,
            IC_SS_SCL_LCNT    => 0,
            IC_FS_SCL_HCNT    => 0x06,
            IC_FS_SCL_LCNT    => 0x0D,
            IC_INTR_STAT      => _rawIntr & _intrMask,
            IC_INTR_MASK      => _intrMask,
            IC_RAW_INTR_STAT  => _rawIntr,
            IC_RX_TL          => _rxTl,
            IC_TX_TL          => _txTl,
            IC_CLR_INTR       => ClearAllInterrupts(),
            IC_CLR_RD_REQ     => ClearBit(8),
            IC_CLR_TX_ABRT    => ClearBit(6),
            IC_CLR_STOP_DET   => ClearBit(9),
            IC_CLR_START_DET  => ClearBit(10),
            IC_ENABLE         => _enable,
            IC_STATUS         => BuildStatus(),
            IC_TXFLR          => 0,   // TX FIFO always drained in simulation
            IC_RXFLR          => (uint)_rxFifo.Count,
            IC_SDA_HOLD       => _sdaHold,
            IC_TX_ABRT_SOURCE => 0,
            IC_ENABLE_STATUS  => _enable & 1,
            IC_COMP_PARAM_1   => 0x00FFFF6E,
            IC_COMP_VERSION   => 0x3230312A,
            IC_COMP_TYPE      => 0x44570140,
            _ => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case IC_CON:       _con = value & 0x7FF; break;
            case IC_TAR:       _tar = value & 0x3FF; break;
            case IC_DATA_CMD:  HandleDataCmd(value); break;
            case IC_INTR_MASK: _intrMask = value & 0xFFF; break;
            case IC_RX_TL:     _rxTl = value & 0xFF; break;
            case IC_TX_TL:     _txTl = value & 0xFF; break;
            case IC_ENABLE:    _enable = value & 3; break;
            case IC_SDA_HOLD:  _sdaHold = value & 0xFFFFFF; break;
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

    // ── Private ──────────────────────────────────────────────────────

    private bool IsEnabled => (_enable & 1) != 0;

    private void HandleDataCmd(uint value)
    {
        if (!IsEnabled) return;

        var isRead = (value & (1u << 8)) != 0;
        var addr   = (byte)(_tar & 0x7F);

        if (isRead)
        {
            var rxByte = OnRead?.Invoke(addr) ?? 0;
            if (_rxFifo.Count < FIFO_DEPTH)
                _rxFifo.Enqueue(rxByte);
            _rawIntr |= 1u << 2;  // RX_FULL or similar
        }
        else
        {
            OnWrite?.Invoke(addr, (byte)(value & 0xFF));
        }

        // Signal STOP_DET when STOP bit set
        if ((value & (1u << 9)) != 0)
            _rawIntr |= 1u << 9;
    }

    private uint PopRxFifo()
    {
        if (_rxFifo.TryDequeue(out var data))
            return data;
        return 0;
    }

    private uint BuildStatus()
    {
        uint st = ST_TFE | ST_TFNF;  // TX always ready in simulation
        if (_rxFifo.Count > 0)  st |= ST_RFNE;
        if (_rxFifo.Count >= FIFO_DEPTH) st |= ST_RFF;
        return st;
    }

    private uint ClearAllInterrupts()
    {
        _rawIntr = 0;
        return 0;
    }

    private uint ClearBit(int bit)
    {
        _rawIntr &= ~(1u << bit);
        return 0;
    }

    /// <summary>Inject a byte into the RX FIFO (simulates a slave device responding).</summary>
    public void InjectByte(byte value)
    {
        if (_rxFifo.Count < FIFO_DEPTH)
            _rxFifo.Enqueue(value);
    }
}
