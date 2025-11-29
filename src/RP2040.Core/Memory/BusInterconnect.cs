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
        if ((address >> 28) == REGION_SRAM) // Probability #1: SRAM (Variables, Stack) ~80%
            return PtrSram[address & MASK_SRAM];

        if ((address >> 28) == REGION_FLASH) // Probability #2: Flash (Constants, Strings) ~15%
            return PtrFlash[address & MASK_FLASH];

        if ((address >> 28) == REGION_BOOTROM) // Probability #3: BootROM ~1%
            return PtrBootRom[address & MASK_BOOTROM];

        // Fallback
        return ReadByteDispatch(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadHalfWord(uint address)
    {
        if ((address >> 28) == REGION_SRAM)
            return Unsafe.ReadUnaligned<ushort>(PtrSram + (address & MASK_SRAM));

        if ((address >> 28) == REGION_FLASH)
            return Unsafe.ReadUnaligned<ushort>(PtrFlash + (address & MASK_FLASH));
            
        if ((address >> 28) == REGION_BOOTROM)
            return Unsafe.ReadUnaligned<ushort>(PtrBootRom + (address & MASK_BOOTROM));

        return ReadHalfWordDispatch(address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadWord(uint address)
    {
        if ((address >> 28) == REGION_SRAM)
            return Unsafe.ReadUnaligned<uint>(PtrSram + (address & MASK_SRAM));

        if ((address >> 28) == REGION_FLASH)
            return Unsafe.ReadUnaligned<uint>(PtrFlash + (address & MASK_FLASH));

        if ((address >> 28) == REGION_BOOTROM)
            return Unsafe.ReadUnaligned<uint>(PtrBootRom + (address & MASK_BOOTROM));

        return ReadWordDispatch(address);
    }

    // --- SLOW PATH DISPATCHERS ---
    // Marcados como NoInlining para mantener pequeño el código caliente de arriba.
    
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
    public void WriteHalfWord(uint address, ushort value)
    {
        if ((address >> 28) == REGION_SRAM)
        {
            Unsafe.WriteUnaligned(PtrSram + (address & MASK_SRAM), value);
            return;
        }

        var region = address >> 28;
        if (region == REGION_FLASH || region == REGION_BOOTROM) return;

        _memoryMap[region]?.WriteHalfWord(address & 0x0FFFFFFF, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(uint address, byte value)
    {
        if ((address >> 28) == REGION_SRAM)
        {
            PtrSram[address & MASK_SRAM] = value;
            return;
        }

        var region = address >> 28;
        if (region == REGION_FLASH || region == REGION_BOOTROM) return;

        _memoryMap[region]?.WriteByte(address & 0x0FFFFFFF, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteWordDispatch(uint region, uint address, uint value)
    {
        _memoryMap[region]?.WriteWord(address & 0x0FFFFFFF, value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sram.Dispose();
        _flash.Dispose();
        _bootRom.Dispose();
        _disposed = true;
    }
}