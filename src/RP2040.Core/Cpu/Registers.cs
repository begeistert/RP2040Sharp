using System.Diagnostics.CodeAnalysis;
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

    // R13: Stack Pointer (SP)
    public uint SP; 

    // R14: Link Register (LR)
    public uint LR;

    // R15: Program Counter (PC)
    public uint PC;

    // --- Program Status Register (xPSR) ---
    public bool N; // Negative
    public bool Z; // Zero
    public bool C; // Carry
    public bool V; // Overflow
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetC() => Unsafe.As<bool, byte>(ref C);
    
    // Interrupt Status Register (IPSR) y Execution (EPSR) se pueden manejar aparte o implícitamente.
    
    /// <summary>
    /// Helper para obtener el valor indexado (sugar syntax para el Span)
    /// </summary>
    public ref uint this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnscopedRef]
        get
        {
            return ref Unsafe.Add(ref R0, index);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe uint* GetBasePointer()
    {
        return (uint*)Unsafe.AsPointer(ref R0);
    }
}