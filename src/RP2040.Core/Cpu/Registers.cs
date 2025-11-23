using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace RP2040.Core.Cpu;

// Garantizamos que R0 a R15 estén contiguos en memoria.
[StructLayout(LayoutKind.Sequential)]
public struct Registers
{
    // --- Registros de Propósito General (Low Registers) ---
    public uint R0;
    public uint R1;
    public uint R2;
    public uint R3;
    public uint R4;
    public uint R5;
    public uint R6;
    public uint R7;

    // --- Registros Altos (High Registers) ---
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
    /// Permite acceso indexado rápido (0-15) sin switch/case y sin arrays en Heap.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<uint> AsSpan()
    {
        // Creamos un Span sobre la memoria de ESTE struct.
        // Como es Sequential, R0...PC son contiguos.
        // Longitud 16 (R0 a R15).
        return MemoryMarshal.CreateSpan(ref R0, 16);
    }
    
    /// <summary>
    /// Helper para obtener el valor indexado (sugar syntax para el Span)
    /// </summary>
    public uint this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            return AsSpan ()[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            AsSpan ()[index] = value;
        }
    }
}