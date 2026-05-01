using RP2040.Core.Cpu;
using RP2040.Core.Memory;

namespace RP2040.Peripherals.Dma;

/// <summary>
/// RP2040 DMA controller (base 0x50000000, region 0x5).
/// 12 DMA channels. Transfers execute synchronously when CTRL_TRIG is written
/// with EN=1, making simulation deterministic.
/// </summary>
public sealed class DmaPeripheral : IMemoryMappedDevice
{
    private const int CHANNEL_COUNT = 12;
    private const uint CHANNEL_SIZE  = 0x40;  // 64 bytes per channel

    // Channel register offsets within each 64-byte block
    private const uint OFF_READ_ADDR   = 0x00;
    private const uint OFF_WRITE_ADDR  = 0x04;
    private const uint OFF_TRANS_COUNT = 0x08;
    private const uint OFF_CTRL_TRIG   = 0x0C;

    // System registers (above channel space)
    private const uint REG_INTR        = 0x400;
    private const uint REG_INTE0       = 0x404;
    private const uint REG_INTF0       = 0x408;
    private const uint REG_INTS0       = 0x40C;
    private const uint REG_INTE1       = 0x414;
    private const uint REG_INTF1       = 0x418;
    private const uint REG_INTS1       = 0x41C;
    private const uint REG_TIMER0      = 0x420;
    private const uint REG_TIMER1      = 0x424;
    private const uint REG_TIMER2      = 0x428;
    private const uint REG_TIMER3      = 0x42C;
    private const uint REG_MULTI_CHAN  = 0x430;

    // CTRL bit masks
    private const uint CTRL_EN         = 1u << 0;
    private const uint CTRL_BUSY       = 1u << 24;
    private const uint CTRL_AHB_ERROR  = 1u << 31;
    private const uint CTRL_DATA_SIZE  = 3u << 2;   // bits 3:2
    private const uint CTRL_INCR_READ  = 1u << 4;
    private const uint CTRL_INCR_WRITE = 1u << 5;
    private const uint CTRL_IRQ_QUIET  = 1u << 21;
    private const uint CTRL_CHAIN_TO   = 0xFu << 11;  // bits 14:11

    private readonly BusInterconnect _bus;
    private readonly CortexM0Plus   _cpu;

    // Per-channel state
    private readonly uint[] _readAddr   = new uint[CHANNEL_COUNT];
    private readonly uint[] _writeAddr  = new uint[CHANNEL_COUNT];
    private readonly uint[] _transCount = new uint[CHANNEL_COUNT];
    private readonly uint[] _ctrl       = new uint[CHANNEL_COUNT];

    // System registers
    private uint _intr;   // pending channel complete flags
    private uint _inte0;  // IRQ0 enable mask
    private uint _intf0;  // IRQ0 force mask
    private uint _inte1;  // IRQ1 enable mask
    private uint _intf1;  // IRQ1 force mask

    public uint Size => 0x1000;

    public DmaPeripheral(BusInterconnect bus, CortexM0Plus cpu)
    {
        _bus = bus;
        _cpu = cpu;
        // Default CHAIN_TO: each channel chains to itself (no chaining)
        for (var i = 0; i < CHANNEL_COUNT; i++)
            _ctrl[i] = (uint)i << 11;
    }

    // ── IMemoryMappedDevice ──────────────────────────────────────────

    public uint ReadWord(uint address)
    {
        if (address < CHANNEL_COUNT * CHANNEL_SIZE)
        {
            var ch = (int)(address / CHANNEL_SIZE);
            var off = address % CHANNEL_SIZE;
            return off switch
            {
                OFF_READ_ADDR   => _readAddr[ch],
                OFF_WRITE_ADDR  => _writeAddr[ch],
                OFF_TRANS_COUNT => _transCount[ch],
                OFF_CTRL_TRIG   => _ctrl[ch],
                _ => 0,
            };
        }

        return address switch
        {
            REG_INTR   => _intr,
            REG_INTE0  => _inte0,
            REG_INTF0  => _intf0,
            REG_INTS0  => (_intr | _intf0) & _inte0,
            REG_INTE1  => _inte1,
            REG_INTF1  => _intf1,
            REG_INTS1  => (_intr | _intf1) & _inte1,
            _ => 0,
        };
    }

    public ushort ReadHalfWord(uint address) =>
        (ushort)(ReadWord(address & ~3u) >> (int)((address & 2) << 3));

    public byte ReadByte(uint address) =>
        (byte)(ReadWord(address & ~3u) >> (int)((address & 3) << 3));

    public void WriteWord(uint address, uint value)
    {
        if (address < CHANNEL_COUNT * CHANNEL_SIZE)
        {
            WriteChannelWord(address, value);
            return;
        }

        switch (address)
        {
            case REG_INTR:  _intr &= ~value; break;     // write 1 to clear
            case REG_INTE0: _inte0 = value & 0xFFF; break;
            case REG_INTF0: _intf0 = value & 0xFFF; break;
            case REG_INTE1: _inte1 = value & 0xFFF; break;
            case REG_INTF1: _intf1 = value & 0xFFF; break;
            case REG_MULTI_CHAN:
                // Trigger multiple channels simultaneously
                for (var i = 0; i < CHANNEL_COUNT; i++)
                    if ((value & (1u << i)) != 0 && (_ctrl[i] & CTRL_EN) != 0)
                        ExecuteChannel(i);
                break;
        }
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 2) << 3);
        var current = ReadWord(aligned);
        WriteWord(aligned, (current & ~(0xFFFFu << shift)) | ((uint)value << shift));
    }

    public void WriteByte(uint address, byte value)
    {
        var aligned = address & ~3u;
        var shift = (int)((address & 3) << 3);
        var current = ReadWord(aligned);
        WriteWord(aligned, (current & ~(0xFFu << shift)) | ((uint)value << shift));
    }

    // ── Private ──────────────────────────────────────────────────────

    private void WriteChannelWord(uint address, uint value)
    {
        var ch = (int)(address / CHANNEL_SIZE);
        var off = address % CHANNEL_SIZE;

        switch (off)
        {
            case OFF_READ_ADDR:
                _readAddr[ch] = value;
                break;
            case OFF_WRITE_ADDR:
                _writeAddr[ch] = value;
                break;
            case OFF_TRANS_COUNT:
                _transCount[ch] = value;
                break;
            case OFF_CTRL_TRIG:
                _ctrl[ch] = value & ~CTRL_BUSY;  // BUSY is HW-driven
                if ((value & CTRL_EN) != 0 && _transCount[ch] > 0)
                    ExecuteChannel(ch);
                break;
        }
    }

    private void ExecuteChannel(int ch)
    {
        _ctrl[ch] |= CTRL_BUSY;

        var dataSize = (int)((_ctrl[ch] & CTRL_DATA_SIZE) >> 2);  // 0=byte, 1=half, 2=word
        var incrRead  = (_ctrl[ch] & CTRL_INCR_READ)  != 0;
        var incrWrite = (_ctrl[ch] & CTRL_INCR_WRITE) != 0;
        var count = _transCount[ch];
        var rAddr = _readAddr[ch];
        var wAddr = _writeAddr[ch];
        var stride = 1u << dataSize;

        for (var i = 0u; i < count; i++)
        {
            uint data = dataSize switch
            {
                0 => _bus.ReadByte(rAddr),
                1 => _bus.ReadHalfWord(rAddr),
                _ => _bus.ReadWord(rAddr),
            };

            switch (dataSize)
            {
                case 0: _bus.WriteByte(wAddr, (byte)data); break;
                case 1: _bus.WriteHalfWord(wAddr, (ushort)data); break;
                default: _bus.WriteWord(wAddr, data); break;
            }

            if (incrRead)  rAddr += stride;
            if (incrWrite) wAddr += stride;
        }

        _readAddr[ch]   = rAddr;
        _writeAddr[ch]  = wAddr;
        _transCount[ch] = 0;
        _ctrl[ch] &= ~(CTRL_BUSY | CTRL_EN);   // clear EN and BUSY when done

        // Signal completion
        _intr |= 1u << ch;

        // Fire CPU interrupt if unmasked
        if ((_inte0 & (1u << ch)) != 0)
            _cpu.SetInterrupt(11 + ch, true);  // DMA IRQ0/1 → hardware IRQs 11-12

        // Chain to another channel if configured
        var chainTo = (int)((_ctrl[ch] & CTRL_CHAIN_TO) >> 11);
        if (chainTo != ch && (_ctrl[chainTo] & CTRL_EN) != 0)
            ExecuteChannel(chainTo);
    }
}
