using RP2040.Core.Memory;

namespace RP2040.Peripherals.Spi;

/// <summary>
/// RP2040 SPI peripheral (PL022).
/// SPI0 base: 0x4003C000, SPI1 base: 0x40040000.
/// TX/RX FIFOs have capacity 8 each. Transfer simulation via injectable callback.
/// </summary>
public sealed class SpiPeripheral : IMemoryMappedDevice
{
    private const uint SSPCR0  = 0x000;  // Control 0: SCR, SPH, SPO, FRF, DSS
    private const uint SSPCR1  = 0x004;  // Control 1: SOD, MS, SSE, LBM
    private const uint SSPDR   = 0x008;  // Data register (FIFO)
    private const uint SSPSR   = 0x00C;  // Status
    private const uint SSPCPSR = 0x010;  // Clock prescaler
    private const uint SSPIMSC = 0x014;  // Interrupt mask set/clear
    private const uint SSPRIS  = 0x018;  // Raw interrupt status
    private const uint SSPMIS  = 0x01C;  // Masked interrupt status
    private const uint SSPICR  = 0x020;  // Interrupt clear
    private const uint SSPDMACR= 0x024;  // DMA control

    // SSPSR bits
    private const uint SR_TFE = 1u << 0;  // TX FIFO empty
    private const uint SR_TNF = 1u << 1;  // TX FIFO not full
    private const uint SR_RNE = 1u << 2;  // RX FIFO not empty
    private const uint SR_RFF = 1u << 3;  // RX FIFO full
    private const uint SR_BSY = 1u << 4;  // Busy

    private const int FIFO_DEPTH = 8;

    private uint _cr0;
    private uint _cr1;
    private uint _cpsr;
    private uint _imsc;
    private uint _ris;

    private readonly Queue<ushort> _txFifo = new(FIFO_DEPTH);
    private readonly Queue<ushort> _rxFifo = new(FIFO_DEPTH);

    /// <summary>
    /// Transfer callback. Called with the TX byte/halfword; return value is the RX data.
    /// If null, RX data is 0.
    /// </summary>
    public Func<ushort, ushort>? OnTransfer;

    public uint Size => 0x1000;

    // ── IMemoryMappedDevice ──────────────────────────────────────────

    public uint ReadWord(uint address)
    {
        return address switch
        {
            SSPCR0  => _cr0,
            SSPCR1  => _cr1,
            SSPDR   => ReadData(),
            SSPSR   => BuildStatus(),
            SSPCPSR => _cpsr,
            SSPIMSC => _imsc,
            SSPRIS  => _ris,
            SSPMIS  => _ris & _imsc,
            _       => 0,
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
            case SSPCR0:  _cr0  = value; break;
            case SSPCR1:  _cr1  = value & 0xF; break;
            case SSPDR:   WriteData((ushort)value); break;
            case SSPCPSR: _cpsr = value & 0xFE; break;  // even values only, bits[7:0]
            case SSPIMSC: _imsc = value & 0xF; break;
            case SSPICR:  _ris &= ~(value & 0x3); break;  // clear RORIC and RTIC
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

    private bool IsEnabled => (_cr1 & (1u << 1)) != 0;

    private void WriteData(ushort txData)
    {
        if (!IsEnabled || _txFifo.Count >= FIFO_DEPTH)
            return;

        // In simulation, perform the transfer immediately
        var rxData = OnTransfer?.Invoke(txData) ?? 0;
        if (_rxFifo.Count < FIFO_DEPTH)
            _rxFifo.Enqueue(rxData);
    }

    private uint ReadData()
    {
        if (_rxFifo.TryDequeue(out var data))
            return data;
        return 0;
    }

    private uint BuildStatus()
    {
        uint sr = SR_TFE;  // TX FIFO always appears empty in simulation (immediate transfer)
        if (_txFifo.Count < FIFO_DEPTH) sr |= SR_TNF;
        if (_rxFifo.Count > 0)         sr |= SR_RNE;
        if (_rxFifo.Count >= FIFO_DEPTH) sr |= SR_RFF;
        return sr;
    }

    /// <summary>Inject a byte into the RX FIFO (simulates incoming data).</summary>
    public void InjectByte(byte value)
    {
        if (_rxFifo.Count < FIFO_DEPTH)
            _rxFifo.Enqueue(value);
    }
}
