using System;

namespace RP2040.Core.Memory;

public class BusInterconnect : IMemoryBus
{
    // Definición de regiones del RP2040
    private const uint SRAM_BASE = 0x20000000;
    private const uint SRAM_SIZE = 264 * 1024; // 264KB totales en RP2040
    private const uint SRAM_END = SRAM_BASE + SRAM_SIZE;

    // Backing stores (Los chips reales)
    private readonly RandomAccessMemory _sram = new RandomAccessMemory((int)SRAM_SIZE);
    
    // Opcional: Una pequeña BootROM para los vectores de reset en 0x0000
    private readonly RandomAccessMemory _bootRom = new RandomAccessMemory(16 * 1024); // 16KB BootROM

    // =============================================================
    // LECTURAS (Routing)
    // =============================================================

    public byte ReadByte(uint address)
    {
        if (address >= SRAM_BASE && address < SRAM_END)
        {
            // IMPORTANTE: Restamos la base para obtener el índice 0-based del array
            return _sram.ReadByte(address - SRAM_BASE);
        }
        
        if (address < 0x4000) // BootROM space (simplificado)
        {
            return _bootRom.ReadByte(address);
        }

        // Si lee memoria no mapeada (Open Bus), retornamos 0 o lanzamos excepción
        return 0; 
    }

    public ushort ReadHalfWord(uint address)
    {
        if (address >= SRAM_BASE && address < SRAM_END)
        {
            return _sram.ReadHalfWord(address - SRAM_BASE);
        }
        
        if (address < 0x4000)
        {
            return _bootRom.ReadHalfWord(address);
        }

        return 0;
    }

    public uint ReadWord(uint address)
    {
        if (address >= SRAM_BASE && address < SRAM_END)
        {
            return _sram.ReadWord(address - SRAM_BASE);
        }
        
        if (address < 0x4000)
        {
            return _bootRom.ReadWord(address);
        }

        return 0;
    }

    // =============================================================
    // ESCRITURAS (Routing)
    // =============================================================

    public void WriteByte(uint address, byte value)
    {
        if (address >= SRAM_BASE && address < SRAM_END)
        {
            _sram.WriteByte(address - SRAM_BASE, value);
            return;
        }
        
        // La BootROM es de solo lectura (ROM), ignoramos escrituras
        // Periféricos irían aquí...
    }

    public void WriteHalfWord(uint address, ushort value)
    {
        if (address >= SRAM_BASE && address < SRAM_END)
        {
            _sram.WriteHalfWord(address - SRAM_BASE, value);
            return;
        }
    }

    public void WriteWord(uint address, uint value)
    {
        if (address >= SRAM_BASE && address < SRAM_END)
        {
            _sram.WriteWord(address - SRAM_BASE, value);
            return;
        }
    }
}