using RP2040.Core.Memory;

namespace RP2040.Peripherals.Ssi;

/// <summary>
/// XIP SSI peripheral stub (0x18000000).
/// Simulates the QSPI SSI used for XIP flash access.
/// Always reports TX-not-full and RX-not-empty so stage-2 boot code
/// CMD_READ_STATUS (0x05) checks complete without hanging.
/// </summary>
public sealed class SsiPeripheral : IMemoryMappedDevice
{
    private const uint SSI_CTRLR0       = 0x000;
    private const uint SSI_CTRLR1       = 0x004;
    private const uint SSI_SSIENR       = 0x008;
    private const uint SSI_MWCR         = 0x00C;
    private const uint SSI_SER          = 0x010;
    private const uint SSI_BAUDR        = 0x014;
    private const uint SSI_TXFTLR       = 0x018;
    private const uint SSI_RXFTLR       = 0x01C;
    private const uint SSI_TXFLR        = 0x020;
    private const uint SSI_RXFLR        = 0x024;
    private const uint SSI_SR           = 0x028;
    private const uint SSI_IMR          = 0x02C;
    private const uint SSI_ISR          = 0x030;
    private const uint SSI_RISR         = 0x034;
    private const uint SSI_ICR          = 0x048;
    private const uint SSI_IDR          = 0x058;
    private const uint SSI_VERSION_ID   = 0x05C;
    private const uint SSI_DR0          = 0x060;
    private const uint SSI_RX_SAMPLE_DLY = 0x0F0;
    private const uint SSI_SPI_CTRL_R0  = 0x0F4;
    private const uint SSI_TXD_DRIVE_EDGE = 0x0F8;

    // SR bits: TFE=TX empty, RFNE=RX not empty, TFNF=TX not full, BUSY
    private const uint SR_TFNF  = 1u << 1;  // TX FIFO not full
    private const uint SR_RFNE  = 1u << 3;  // RX FIFO not empty
    private const uint SR_TFE   = 1u << 2;  // TX FIFO empty

    private const uint CMD_READ_STATUS = 0x05;

    private uint _ctrlr0;
    private uint _ctrlr1;
    private uint _ssienr;
    private uint _baudr    = 2;
    private uint _dr0      = 0;
    private uint _spiCtrlr0;
    private uint _txDriveEdge;
    private uint _rxSampleDly;

    public uint Size => 0x1000;

    public uint ReadWord(uint address) => address switch
    {
        SSI_CTRLR0      => _ctrlr0,
        SSI_CTRLR1      => _ctrlr1,
        SSI_SSIENR      => _ssienr,
        SSI_BAUDR       => _baudr,
        SSI_SR          => SR_TFE | SR_RFNE | SR_TFNF,  // always ready
        SSI_IDR         => 0x51535049,   // "QSPI" identifier
        SSI_VERSION_ID  => 0x3430312A,
        SSI_DR0         => _dr0,
        SSI_SPI_CTRL_R0 => _spiCtrlr0,
        SSI_TXD_DRIVE_EDGE => _txDriveEdge,
        SSI_RX_SAMPLE_DLY  => _rxSampleDly,
        _               => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case SSI_CTRLR0:     _ctrlr0    = value; break;
            case SSI_CTRLR1:     _ctrlr1    = value; break;
            case SSI_SSIENR:     _ssienr    = value; break;
            case SSI_BAUDR:      _baudr     = value; break;
            case SSI_SPI_CTRL_R0:   _spiCtrlr0   = value; break;
            case SSI_TXD_DRIVE_EDGE: _txDriveEdge = value; break;
            case SSI_RX_SAMPLE_DLY:  _rxSampleDly = value; break;
            case SSI_DR0:
                // Stage-2 boot sends CMD_READ_STATUS; respond with 0 (not busy)
                _dr0 = 0u;
                break;
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
