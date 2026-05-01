using RP2040.Core.Memory;

namespace RP2040.Peripherals.SysCfg;

/// <summary>
/// SysCfg peripheral (0x40004000).
/// System configuration registers (NMI masks, processor config, etc.).
/// </summary>
public sealed class SysCfgPeripheral : IMemoryMappedDevice
{
    private const uint PROC0_NMI_MASK          = 0x00;
    private const uint PROC1_NMI_MASK          = 0x04;
    private const uint PROC_CONFIG             = 0x08;
    private const uint PROC_IN_SYNC_BYPASS     = 0x0C;
    private const uint PROC_IN_SYNC_BYPASS_HI  = 0x10;
    private const uint DBGFORCE               = 0x14;
    private const uint MEMPOWERDOWN           = 0x18;

    private uint _proc0NmiMask;
    private uint _proc1NmiMask;
    private uint _procConfig;
    private uint _procInSyncBypass;
    private uint _procInSyncBypassHi;
    private uint _dbgforce;
    private uint _mempowerdown;

    public uint Size => 0x1000;

    public uint ReadWord(uint address) => address switch
    {
        PROC0_NMI_MASK         => _proc0NmiMask,
        PROC1_NMI_MASK         => _proc1NmiMask,
        PROC_CONFIG            => _procConfig,
        PROC_IN_SYNC_BYPASS    => _procInSyncBypass,
        PROC_IN_SYNC_BYPASS_HI => _procInSyncBypassHi,
        DBGFORCE               => _dbgforce,
        MEMPOWERDOWN           => _mempowerdown,
        _                      => 0,
    };

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        switch (address)
        {
            case PROC0_NMI_MASK:         _proc0NmiMask         = value; break;
            case PROC1_NMI_MASK:         _proc1NmiMask         = value; break;
            case PROC_CONFIG:            _procConfig           = value; break;
            case PROC_IN_SYNC_BYPASS:    _procInSyncBypass     = value; break;
            case PROC_IN_SYNC_BYPASS_HI: _procInSyncBypassHi   = value; break;
            case DBGFORCE:               _dbgforce             = value; break;
            case MEMPOWERDOWN:           _mempowerdown         = value; break;
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
