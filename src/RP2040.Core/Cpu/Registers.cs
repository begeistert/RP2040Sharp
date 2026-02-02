using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    // --- Backing Stores for Stack Pointers ---
    public uint MSP_Storage;
    public uint PSP_Storage;

    // --- System Registers ---
    public uint PRIMASK; // Bit 0: PM
    public uint CONTROL; // Bit 1: SPSEL, Bit 0: nPRIV
    public uint IPSR; // Exception Number (0 = Thread Mode)

    // --- Program Status Register (xPSR) ---
    public bool N; // Negative
    public bool Z; // Zero
    public bool C; // Carry
    public bool V; // Overflow

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetC() => Unsafe.As<bool, byte>(ref C);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetxPsr()
    {
        uint apsr = 0;
        if (N)
            apsr |= 0x80000000;
        if (Z)
            apsr |= 0x40000000;
        if (C)
            apsr |= 0x20000000;
        if (V)
            apsr |= 0x10000000;

        // xPSR combina APSR, EPSR (Thumb bit siempre 1) e IPSR
        return apsr | 0x01000000 | (IPSR & 0x3F);
    }

    // Interrupt Status Register (IPSR) y Execution (EPSR) se pueden manejar aparte o implícitamente.

    /// <summary>
    /// Helper para obtener el valor indexado (sugar syntax para el Span)
    /// </summary>
    public ref uint this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnscopedRef]
        get { return ref Unsafe.Add(ref R0, index); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe uint* GetBasePointer()
    {
        return (uint*)Unsafe.AsPointer(ref R0);
    }
}
