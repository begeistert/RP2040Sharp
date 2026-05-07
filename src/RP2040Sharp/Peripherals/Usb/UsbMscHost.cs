namespace RP2040.Peripherals.Usb;

/// <summary>
/// Host-side USB Mass Storage Class (MSC) driver implementing the Bulk-Only Transport
/// (BOT) protocol.  Works alongside <see cref="UsbCdcHost"/> when the device exposes a
/// composite CDC + MSC configuration (e.g. CircuitPython's CIRCUITPY drive).
///
/// Enumeration is handled entirely by <see cref="UsbCdcHost"/>; this driver subscribes
/// to the companion events and simply handles the MSC bulk endpoints.
///
/// Initialisation sequence (once SET_CONFIGURATION is acknowledged):
///   TEST_UNIT_READY → READ_CAPACITY(10) → <see cref="OnReady"/>
///
/// After <see cref="OnReady"/> fires, callers may enqueue sector reads/writes via
/// <see cref="RequestRead"/> and <see cref="RequestWrite"/>.  Commands are processed in
/// order; each requires simulation cycles (run the machine) to complete.
/// </summary>
public sealed class UsbMscHost
{
    // ── CBW / CSW constants ──────────────────────────────────────────────────
    private const uint CBW_SIGNATURE = 0x43425355u; // "USBC"
    private const uint CSW_SIGNATURE = 0x53425355u; // "USBS"
    private const int  CBW_SIZE      = 31;
    private const int  CSW_SIZE      = 13;
    private const byte CBW_FLAG_DATA_IN  = 0x80; // device → host
    private const byte CBW_FLAG_DATA_OUT = 0x00; // host → device

    // ── SCSI opcodes ─────────────────────────────────────────────────────────
    private const byte SCSI_TEST_UNIT_READY  = 0x00;
    private const byte SCSI_READ_CAPACITY_10 = 0x25;
    private const byte SCSI_READ_10          = 0x28;
    private const byte SCSI_WRITE_10         = 0x2A;

    private const int READ_CAPACITY_RESP_SIZE = 8;
    private const int SECTOR_BYTES            = 512;

    private enum Phase
    {
        NotConnected,
        WaitCsw,        // sent a no-data-phase CBW, waiting for CSW
        WaitDataIn,     // waiting for sector data from device
        WaitDataOut,    // waiting for device to arm OUT ep (WRITE data phase)
        WaitCswData,    // received all data, now waiting for CSW
    }

    private readonly UsbCdcHost    _cdc;
    private readonly UsbPeripheral _usb;

    private Phase  _phase = Phase.NotConnected;
    private uint   _tag;

    // Whether the MSC OUT endpoint is armed by the device (ready to receive CBW / write data).
    private bool _outArmed;

    // Accumulation buffer for incoming IN data (sector data, CSW, READ_CAPACITY response).
    private readonly List<byte> _rxBuf   = new();
    private int                 _rxNeed;  // bytes expected before transitioning state

    // Is the current init/command doing READ_CAPACITY (special in-data parsing)?
    private bool _isReadCapacity;

    // Active and pending commands.
    private MscCommand?             _active;
    private readonly Queue<MscCommand> _queue = new();

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Fired once MSC initialisation (TEST_UNIT_READY + READ_CAPACITY) succeeds.</summary>
    public Action? OnReady;

    /// <summary>true after <see cref="OnReady"/> has fired.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Total logical blocks on the device (valid after <see cref="OnReady"/>).</summary>
    public uint BlockCount { get; private set; }

    /// <summary>Logical block size in bytes, typically 512 (valid after <see cref="OnReady"/>).</summary>
    public uint BlockSize { get; private set; } = SECTOR_BYTES;

    public UsbMscHost(UsbCdcHost cdc)
    {
        _cdc = cdc;
        _usb = cdc.Usb;
        cdc.OnConfigurationComplete += HandleConfigurationComplete;
        _usb.OnEndpointWrite        += HandleEndpointWrite;
        _usb.OnEndpointRead         += HandleEndpointRead;
    }

    /// <summary>
    /// Enqueue a 512-byte sector read from logical block <paramref name="lba"/>.
    /// <paramref name="callback"/> receives the sector bytes once the transfer completes.
    /// </summary>
    public void RequestRead(uint lba, Action<byte[]> callback)
    {
        _queue.Enqueue(new MscCommand(lba, readData: null, callback));
        TryStart();
    }

    /// <summary>
    /// Enqueue a 512-byte sector write to logical block <paramref name="lba"/>.
    /// <paramref name="callback"/> is invoked (with an empty array) after the CSW confirms
    /// success.
    /// </summary>
    public void RequestWrite(uint lba, byte[] data, Action<byte[]>? callback = null)
    {
        if (data.Length != BlockSize)
            throw new ArgumentException($"Data must be {BlockSize} bytes.", nameof(data));
        _queue.Enqueue(new MscCommand(lba, readData: data, callback));
        TryStart();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void HandleConfigurationComplete()
    {
        IsConnected = false;
        BlockCount  = 0;
        BlockSize   = SECTOR_BYTES;
        _phase      = Phase.NotConnected;
        _outArmed   = false;
        _rxBuf.Clear();
        _active = null;
        _queue.Clear();

        // Queue the init sequence; delivery happens when the device arms the OUT endpoint.
        _queue.Enqueue(MscCommand.TestUnitReady());
        _queue.Enqueue(MscCommand.ReadCapacity());
        TryStart();
    }

    private void HandleEndpointRead(int ep, int size)
    {
        if (_cdc.MscOutEndpoint < 0 || ep != _cdc.MscOutEndpoint) return;

        _outArmed = true;

        // WRITE data phase: device armed OUT to receive sector data.
        if (_phase == Phase.WaitDataOut && _active?.WriteData != null)
        {
            _outArmed = false;
            _usb.EndpointReadDone(ep, _active.WriteData);
            _phase  = Phase.WaitCswData;
            _rxNeed = CSW_SIZE;
            return;
        }

        // Otherwise try to start the next queued command.
        TryStart();
    }

    private void HandleEndpointWrite(int ep, byte[] data)
    {
        if (_cdc.MscInEndpoint < 0 || ep != _cdc.MscInEndpoint) return;
        if (_phase == Phase.NotConnected) return;

        _rxBuf.AddRange(data);
        ProcessRx();
    }

    // ── State machine ────────────────────────────────────────────────────────

    private void TryStart()
    {
        if (!_outArmed)      return;
        if (_queue.Count == 0) return;
        if (_active != null) return; // wait for current command to finish

        _active   = _queue.Dequeue();
        _outArmed = false;
        _rxBuf.Clear();

        var cbw = BuildCbw(_active);
        _usb.EndpointReadDone(_cdc.MscOutEndpoint, cbw);

        if (_active.IsTestUnitReady)
        {
            _phase  = Phase.WaitCsw;
            _rxNeed = CSW_SIZE;
            _isReadCapacity = false;
        }
        else if (_active.IsReadCapacity)
        {
            _phase  = Phase.WaitDataIn;
            _rxNeed = READ_CAPACITY_RESP_SIZE;
            _isReadCapacity = true;
        }
        else if (_active.WriteData == null) // READ
        {
            _phase  = Phase.WaitDataIn;
            _rxNeed = SECTOR_BYTES;
            _isReadCapacity = false;
        }
        else // WRITE
        {
            _phase  = Phase.WaitDataOut;
            _rxNeed = 0;
            _isReadCapacity = false;
        }
    }

    private void ProcessRx()
    {
        while (true)
        {
            switch (_phase)
            {
                case Phase.WaitDataIn:
                    if (_rxBuf.Count < _rxNeed) return;
                    if (_isReadCapacity)
                    {
                        BlockCount = ReadBe32(_rxBuf, 0) + 1;
                        BlockSize  = ReadBe32(_rxBuf, 4);
                    }
                    else
                    {
                        // Store sector data on the active command for later delivery.
                        _active!.ReceivedData = _rxBuf.GetRange(0, _rxNeed).ToArray();
                    }
                    _rxBuf.RemoveRange(0, _rxNeed);
                    _phase  = Phase.WaitCswData;
                    _rxNeed = CSW_SIZE;
                    continue;

                case Phase.WaitCsw:
                case Phase.WaitCswData:
                    if (_rxBuf.Count < CSW_SIZE) return;
                    var sig = ReadLe32(_rxBuf, 0);
                    _rxBuf.RemoveRange(0, CSW_SIZE);
                    if (sig != CSW_SIGNATURE)
                    {
                        // Phase error — reset and try again.
                        _phase  = Phase.NotConnected;
                        _active = null;
                        return;
                    }
                    CompleteCommand();
                    return;

                default:
                    return;
            }
        }
    }

    private void CompleteCommand()
    {
        var cmd = _active!;
        _active = null;

        if (cmd.IsTestUnitReady || cmd.IsReadCapacity)
        {
            // Init phase.
            if (_queue.Count > 0 && _queue.Peek().IsReadCapacity)
            {
                // Still have READ_CAPACITY init command queued — proceed normally.
            }
            if (cmd.IsReadCapacity)
            {
                IsConnected = true;
                _phase = Phase.NotConnected; // idle
                OnReady?.Invoke();
            }
        }
        else
        {
            // User command finished.
            cmd.Callback?.Invoke(cmd.ReceivedData ?? Array.Empty<byte>());
            _phase = Phase.NotConnected; // idle
        }

        TryStart();
    }

    // ── CBW construction ─────────────────────────────────────────────────────

    private byte[] BuildCbw(MscCommand cmd)
    {
        _tag++;
        var cbw = new byte[CBW_SIZE];
        WriteLe32(cbw, 0, CBW_SIGNATURE);
        WriteLe32(cbw, 4, _tag);

        if (cmd.IsTestUnitReady)
        {
            WriteLe32(cbw, 8, 0);
            cbw[12] = CBW_FLAG_DATA_OUT;
            cbw[14] = 6;
            cbw[15] = SCSI_TEST_UNIT_READY;
        }
        else if (cmd.IsReadCapacity)
        {
            WriteLe32(cbw, 8, READ_CAPACITY_RESP_SIZE);
            cbw[12] = CBW_FLAG_DATA_IN;
            cbw[14] = 10;
            cbw[15] = SCSI_READ_CAPACITY_10;
        }
        else if (cmd.WriteData == null) // READ(10)
        {
            WriteLe32(cbw, 8, SECTOR_BYTES);
            cbw[12] = CBW_FLAG_DATA_IN;
            cbw[14] = 10;
            cbw[15] = SCSI_READ_10;
            WriteBe32(cbw, 17, cmd.Lba);
            cbw[23] = 0;
            cbw[24] = 1; // transfer length = 1 sector
        }
        else // WRITE(10)
        {
            WriteLe32(cbw, 8, SECTOR_BYTES);
            cbw[12] = CBW_FLAG_DATA_OUT;
            cbw[14] = 10;
            cbw[15] = SCSI_WRITE_10;
            WriteBe32(cbw, 17, cmd.Lba);
            cbw[23] = 0;
            cbw[24] = 1; // transfer length = 1 sector
        }
        return cbw;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static uint ReadLe32(List<byte> b, int o)
        => (uint)(b[o] | (b[o+1] << 8) | (b[o+2] << 16) | (b[o+3] << 24));

    private static uint ReadBe32(List<byte> b, int o)
        => (uint)((b[o] << 24) | (b[o+1] << 16) | (b[o+2] << 8) | b[o+3]);

    private static void WriteLe32(byte[] b, int o, uint v)
    {
        b[o]   = (byte) v;
        b[o+1] = (byte)(v >>  8);
        b[o+2] = (byte)(v >> 16);
        b[o+3] = (byte)(v >> 24);
    }

    private static void WriteBe32(byte[] b, int o, uint v)
    {
        b[o]   = (byte)(v >> 24);
        b[o+1] = (byte)(v >> 16);
        b[o+2] = (byte)(v >>  8);
        b[o+3] = (byte) v;
    }

    // ── Command record ───────────────────────────────────────────────────────

    private sealed class MscCommand
    {
        public uint          Lba            { get; }
        public byte[]?       WriteData      { get; }
        public byte[]?       ReceivedData   { get; set; }
        public Action<byte[]>? Callback     { get; }
        public bool          IsTestUnitReady { get; }
        public bool          IsReadCapacity  { get; }

        /// <summary>Init: TEST_UNIT_READY (no-data).</summary>
        public static MscCommand TestUnitReady() => new(isTest: true);
        /// <summary>Init: READ_CAPACITY(10).</summary>
        public static MscCommand ReadCapacity()   => new(isTest: false);

        private MscCommand(bool isTest)
        {
            IsTestUnitReady = isTest;
            IsReadCapacity  = !isTest;
        }

        /// <summary>User READ or WRITE command.</summary>
        public MscCommand(uint lba, byte[]? readData, Action<byte[]>? callback)
        {
            Lba       = lba;
            WriteData = readData; // null ⟹ READ, non-null ⟹ WRITE
            Callback  = callback;
        }
    }
}
