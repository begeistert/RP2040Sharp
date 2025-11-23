using System;
using System.Runtime.CompilerServices;

namespace RP2040.Core.Memory;

public unsafe class BusInterconnect : IMemoryBus
{
    private readonly IMemoryMappedDevice[] _memoryMap = new IMemoryMappedDevice[16];
    
    // Backing stores (Los chips reales)
    private readonly RandomAccessMemory _sram;
    // Opcional: Una pequeña BootROM para los vectores de reset en 0x0000
    private readonly RandomAccessMemory _bootRom; // 16KB BootROM
    
    private readonly byte* _fastSramPtr;
    private const uint SRAM_ALLOC_SIZE = 512 * 1024; // The RP2040 has actually 264KB of SRAM, but for performance reasons we will use 512KB because it is a power of 2.
    private const uint SRAM_REGION = 0x2; // 0x20000000 >> 28
    private const uint SRAM_MASK = 0x7FFFF; // Máscara para ~264KB (simplificado para mirroring)

    public BusInterconnect ()
    {
        _sram = new RandomAccessMemory ((int)SRAM_ALLOC_SIZE);
        _bootRom = new RandomAccessMemory (16 * 1024);

        _fastSramPtr = _sram.BasePtr;
        
        MapDevice(0x0, _bootRom);
        
        MapDevice(0x2, _sram);
    }
    
    public void MapDevice(int regionIndex, IMemoryMappedDevice device)
    {
        if (regionIndex is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(regionIndex), "Region index must be between 0 and 15");
        _memoryMap[regionIndex] = device;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte(uint address)
    {
        return (address >> 28) == SRAM_REGION ? _fastSramPtr[address & SRAM_MASK] : ReadByteDispatch(address);
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
        return (address >> 28) == SRAM_REGION ? Unsafe.ReadUnaligned<ushort>(_fastSramPtr + (address & SRAM_MASK)) : ReadHalfWordDispatch(address);
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
        // --------------------------------------------------------
        // TIER 1: RAM FAST PATH (Optimización Extrema)
        // --------------------------------------------------------
        // Comprobación bitwise barata. Si es RAM, leemos memoria directa.
        // Sin llamadas a funciones, sin interfaces, sin bounds check.
        if ((address >> 28) == SRAM_REGION)
        {
            return Unsafe.ReadUnaligned<uint>(_fastSramPtr + (address & SRAM_MASK));
        }

        // --------------------------------------------------------
        // TIER 2: SLOW PATH (Dispositivos Mapeados)
        // --------------------------------------------------------
        return ReadWordDispatch(address);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)] // Sacamos lo lento del hot-path
    private uint ReadWordDispatch(uint address)
    {
        // 1. Obtener índice de región (0-15)
        var index = address >> 28;
        
        // 2. Obtener dispositivo (Array lookup es muy rápido)
        var device = _memoryMap[index];

        // 3. Delegar
        if (device != null)
        {
            // TRUCO DE OFFSET:
            // Le pasamos al dispositivo los 28 bits inferiores.
            // El dispositivo recibe "offset desde mi base".
            return device.ReadWord(address & 0x0FFFFFFF);
        }

        // 4. Open Bus (Memoria no mapeada)
        return 0; // O log.Warning("Read from unmapped memory: " + address);
    }

    // =============================================================
    // ESCRITURAS (Routing)
    // =============================================================

    public void WriteByte(uint address, byte value)
    {
        if ((address >> 28) == SRAM_REGION) {
            _fastSramPtr[address & SRAM_MASK] = value;
            return;
        }
        _memoryMap[address >> 28]?.WriteByte(address & 0x0FFFFFFF, value);
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        if ((address >> 28) == SRAM_REGION) {
            Unsafe.WriteUnaligned(_fastSramPtr + (address & SRAM_MASK), value);
            return;
        }
        _memoryMap[address >> 28]?.WriteHalfWord(address & 0x0FFFFFFF, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteWord(uint address, uint value)
    {
        // RAM Fast Path
        if ((address >> 28) == SRAM_REGION)
        {
            Unsafe.WriteUnaligned(_fastSramPtr + (address & SRAM_MASK), value);
            return;
        }

        WriteWordDispatch(address, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteWordDispatch(uint address, uint value)
    {
        var device = _memoryMap[address >> 28];
        // Nota: Si es ROM, el dispositivo debe ignorar la escritura o lanzar excepción,
        // el Bus no decide política de ReadOnly, solo rutea.
        device?.WriteWord(address & 0x0FFFFFFF, value);
    }
}