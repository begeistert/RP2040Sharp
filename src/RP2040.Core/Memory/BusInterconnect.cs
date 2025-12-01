using System;
using System.Runtime.CompilerServices;

namespace RP2040.Core.Memory;

public unsafe class BusInterconnect : IMemoryBus, IDisposable
{
    public const uint REGION_BOOTROM = 0x0;
    public const uint REGION_FLASH   = 0x1;
    public const uint REGION_SRAM    = 0x2;
    
    public const uint MASK_SRAM    = 0x7FFFF;  // 512KB (covers 264KB + mirrors)
    public const uint MASK_FLASH   = 0x1FFFFF; // 2MB
    public const uint MASK_BOOTROM = 0x3FFF;   // 16KB
    
    public const uint SRAM_START_ADDRESS = 0x20000000;
    public const uint FLASH_START_ADDRESS = 0x10000000;

    public readonly byte* PtrSram;
    public readonly byte* PtrFlash;
    public readonly byte* PtrBootRom;
    
    private readonly byte*[] _fastBasePtrs = new byte*[16];
    private readonly uint[] _fastMasks = new uint[16];

    private readonly IMemoryMappedDevice[] _memoryMap = new IMemoryMappedDevice[16];
    
    private readonly RandomAccessMemory _sram;
    private readonly RandomAccessMemory _bootRom;
    private readonly RandomAccessMemory _flash;

    private bool _disposed;

    public BusInterconnect()
    {
        _sram    = new RandomAccessMemory(512 * 1024); 
        _flash   = new RandomAccessMemory(2 * 1024 * 1024);
        _bootRom = new RandomAccessMemory(16 * 1024);

        PtrSram    = _sram.BasePtr;
        PtrFlash   = _flash.BasePtr;
        PtrBootRom = _bootRom.BasePtr;
        
        _fastBasePtrs[REGION_BOOTROM] = PtrBootRom;
        _fastMasks[REGION_BOOTROM]    = MASK_BOOTROM;
        
        _fastBasePtrs[REGION_FLASH]   = PtrFlash;
        _fastMasks[REGION_FLASH]      = MASK_FLASH;
        
        _fastBasePtrs[REGION_SRAM]    = PtrSram;
        _fastMasks[REGION_SRAM]       = MASK_SRAM;
        
        MapDevice((int)REGION_BOOTROM, _bootRom);
        MapDevice((int)REGION_FLASH, _flash);
        MapDevice((int)REGION_SRAM, _sram);
    }
    
    public void MapDevice(int regionIndex, IMemoryMappedDevice device)
    {
        if (regionIndex is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(regionIndex));
        _memoryMap[regionIndex] = device;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte(uint address)
    {
        var region = address >> 28;
        var basePtr = _fastBasePtrs[region];

        return basePtr != null ? basePtr[address & _fastMasks[region]] : ReadByteDispatch(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadHalfWord(uint address)
    {
        var region = address >> 28;
        var basePtr = _fastBasePtrs[region];
        
        return basePtr != null ? Unsafe.ReadUnaligned<ushort>(basePtr + (address & _fastMasks[region])) : ReadHalfWordDispatch(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadWord(uint address)
    {
        var region = address >> 28;
        var basePtr = _fastBasePtrs[region];
        
        return basePtr != null ? Unsafe.ReadUnaligned<uint>(basePtr + (address & _fastMasks[region])) : ReadWordDispatch(address);

    }

    // --- SLOW PATH DISPATCHERS ---
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte ReadByteDispatch(uint address) 
        => _memoryMap[address >> 28]?.ReadByte(address & 0x0FFFFFFF) ?? 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ushort ReadHalfWordDispatch(uint address) 
        => _memoryMap[address >> 28]?.ReadHalfWord(address & 0x0FFFFFFF) ?? 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private uint ReadWordDispatch(uint address) 
        => _memoryMap[address >> 28]?.ReadWord(address & 0x0FFFFFFF) ?? 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteWord(uint address, uint value)
    {
        if ((address >> 28) == REGION_SRAM)
        {
            Unsafe.WriteUnaligned(PtrSram + (address & MASK_SRAM), value);
            return;
        }
        var region = address >> 28;
        if (region == REGION_FLASH || region == REGION_BOOTROM) return;

        WriteWordDispatch(region, address, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(uint address, byte value)
    {
        if ((address >> 28) == REGION_SRAM) { PtrSram[address & MASK_SRAM] = value; return; }
        if ((address >> 28) <= REGION_FLASH) return; // ROM(0) o FLASH(1)
        _memoryMap[address >> 28]?.WriteByte(address & 0x0FFFFFFF, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteHalfWord(uint address, ushort value)
    {
        if ((address >> 28) == REGION_SRAM) { Unsafe.WriteUnaligned(PtrSram + (address & MASK_SRAM), value); return; }
        if ((address >> 28) <= REGION_FLASH) return;
        _memoryMap[address >> 28]?.WriteHalfWord(address & 0x0FFFFFFF, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteWordDispatch(uint region, uint address, uint value) => _memoryMap[region]?.WriteWord(address & 0x0FFFFFFF, value);

    public void Dispose()
    {
        if (_disposed) return;
        _sram.Dispose();
        _flash.Dispose();
        _bootRom.Dispose();
        _disposed = true;
    }
}