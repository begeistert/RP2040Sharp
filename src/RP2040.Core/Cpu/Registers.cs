using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace RP2040.Core.Cpu;

[StructLayout(LayoutKind.Sequential)]
public struct Registers
{
    // --- Low Registers (R0-R7) ---
    public uint R0;
    public uint R1;
    public uint R2;
    public uint R3;
    public uint R4;
    public uint R5;
    public uint R6;
    public uint R7;

    // --- High Registers (R8-R12) ---
    public uint R8;
    public uint R9;
    public uint R10;
    public uint R11;
    public uint R12;

    // --- Registros Especiales ---
    
    // R13: Stack Pointer (SP)
    // Nota: El M0+ tiene MSP (Main) y PSP (Process). 
    // El emulador debe gestionar cuál de los dos está activo en 'SP' según el modo.
    public uint SP; 

    // R14: Link Register (LR)
    public uint LR;

    // R15: Program Counter (PC)
    public uint PC;

    // --- Program Status Register (xPSR) ---
    // OPTIMIZACIÓN CRÍTICA:
    // En lugar de empaquetar los flags (N, Z, C, V) en un solo uint y usar máscaras
    // en cada instrucción (lento), los guardamos como bools separados.
    // Solo los empaquetamos cuando el software lee el registro xPSR explícitamente.
    public bool N; // Negative
    public bool Z; // Zero
    public bool C; // Carry
    public bool V; // Overflow
    
    // Interrupt Status Register (IPSR) y Execution (EPSR) se pueden manejar aparte o implícitamente.
    
    /// <summary>
    /// Helper para obtener el valor indexado (sugar syntax para el Span)
    /// </summary>
    public uint this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return Unsafe.Add(ref R0, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            Unsafe.Add(ref R0, index) = value;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe uint* GetBasePointer()
    {
        return (uint*)Unsafe.AsPointer(ref R0);
    }
}