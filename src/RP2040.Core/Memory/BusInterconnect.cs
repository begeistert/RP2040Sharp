using System;
using System.Runtime.CompilerServices;

namespace RP2040.Core.Memory;

public unsafe class BusInterconnect : IMemoryBus
{
    public const uint REGION_BOOTROM = 0x0;
    public const uint REGION_FLASH   = 0x1;
    public const uint REGION_SRAM    = 0x2;
    
    // BACKING STORES
    private readonly IMemoryMappedDevice[] _memoryMap = new IMemoryMappedDevice[16];
    
    private readonly RandomAccessMemory _sram;
    private readonly RandomAccessMemory _bootRom;
    private readonly RandomAccessMemory _flash;

    private const uint SRAM_ALLOC_SIZE = 512 * 1024; // The RP2040 has actually 264KB of SRAM, but for performance reasons we will use 512KB because it is a power of 2.
    private const uint FLASH_ALLOC_SIZE = 2 * 1024 * 1024; // 2MB
    private const uint BOOTROM_ALLOC_SIZE = 16 * 1024;
    
    public const uint MASK_SRAM = 0x7FFFF; // Máscara para ~264KB (simplificado para mirroring)
    public const uint MASK_FLASH = 0x1FFFFF; // 2MB
    public const uint MASK_BOOTROM = 0x3FFF;
    
    public readonly byte* PtrSram;
    public readonly byte* PtrFlash;
    public readonly byte* PtrBootRom;
    
    internal uint MaskSram = MASK_SRAM;
    internal uint MaskFlash = MASK_FLASH;
    internal uint MaskBootRom = MASK_BOOTROM;

    public BusInterconnect ()
    {
        _sram = new RandomAccessMemory ((int)SRAM_ALLOC_SIZE);
        _bootRom = new RandomAccessMemory ((int)BOOTROM_ALLOC_SIZE);
        _flash = new RandomAccessMemory ((int)FLASH_ALLOC_SIZE);

        PtrSram = _sram.BasePtr;
        PtrFlash = _flash.BasePtr;
        PtrBootRom = _bootRom.BasePtr;
        
        MapDevice((int)REGION_BOOTROM, _bootRom);
        MapDevice((int)REGION_FLASH, _flash);
        MapDevice((int)REGION_SRAM, _sram);
    }
    
    public void MapDevice(int regionIndex, IMemoryMappedDevice device)
    {
        if (regionIndex is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(regionIndex), "Region index must be between 0 and 15");
        _memoryMap[regionIndex] = device;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte(uint address)
    {
        return (address >> 28) switch {
            // Tier 1: SRAM (Datos más comunes)
            REGION_SRAM => PtrSram[address & MASK_SRAM],
            // Tier 2: Flash (Lectura de constantes con LDR) - Opcional añadir fastpath aquí también
            REGION_FLASH => PtrFlash[address & MASK_FLASH],
            _ => ReadByteDispatch (address)
        };

    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte ReadByteDispatch(uint address)
    {
        var device = _memoryMap[address >> 28];
        return device != null ? device.ReadByte(address & 0x0FFFFFFF) : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadHalfWord(uint address)
    {
        return (address >> 28) switch {
            REGION_SRAM => Unsafe.ReadUnaligned<ushort> (PtrSram + (address & MASK_SRAM)),
            // Añadimos Flash aquí porque a veces se leen constantes de 16-bit de flash
            REGION_FLASH => Unsafe.ReadUnaligned<ushort> (PtrFlash + (address & MASK_FLASH)),
            _ => ReadHalfWordDispatch (address)
        };
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ushort ReadHalfWordDispatch(uint address)
    {
        var device = _memoryMap[address >> 28];
        return device != null ? device.ReadHalfWord(address & 0x0FFFFFFF) : (ushort)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadWord(uint address)
    {
        return (address >> 28) switch {
            // Fast Path SRAM
            REGION_SRAM => Unsafe.ReadUnaligned<uint> (PtrSram + (address & MASK_SRAM)),
            // Fast Path Flash (Datos constantes)
            REGION_FLASH => Unsafe.ReadUnaligned<uint> (PtrFlash + (address & MASK_FLASH)),
            _ => ReadWordDispatch (address)
        };

    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private uint ReadWordDispatch(uint address)
    {
        var device = _memoryMap[address >> 28];
        return device != null ? device.ReadWord(address & 0x0FFFFFFF) : 0u;
    }

    // =============================================================
    // ESCRITURAS (Routing)
    // =============================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteWord(uint address, uint value)
    {
        // Solo optimizamos SRAM para escritura. 
        // Escribir en Flash requiere comandos especiales, no escritura directa.
        // Escribir en ROM es imposible.
        if ((address >> 28) == REGION_SRAM)
        {
            Unsafe.WriteUnaligned(PtrSram + (address & MASK_SRAM), value);
            return;
        }

        WriteWordDispatch(address, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteWordDispatch(uint address, uint value)
    {
        _memoryMap[address >> 28]?.WriteWord(address & 0x0FFFFFFF, value);
    }

    // Implementa WriteByte / WriteHalfWord similarmente...
    public void WriteByte(uint address, byte value) {
        if ((address >> 28) == REGION_SRAM) { PtrSram[address & MASK_SRAM] = value; return; }
        _memoryMap[address >> 28]?.WriteByte(address & 0x0FFFFFFF, value);
    }
    public void WriteHalfWord(uint address, ushort value) {
        if ((address >> 28) == REGION_SRAM) { Unsafe.WriteUnaligned(PtrSram + (address & MASK_SRAM), value); return; }
        _memoryMap[address >> 28]?.WriteHalfWord(address & 0x0FFFFFFF, value);
    }
}