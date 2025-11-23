using System.Runtime.CompilerServices;
using System.Numerics; // Asegúrate de tener este using para BitOperations

namespace RP2040.Core.Internals;

public static class BitUtils
{
	// Extrae un valor de 'len' bits comenzando en la posición 'start'
	// Ejemplo: Extract(0b11010, 1, 3) -> extrae '101' -> devuelve 5
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint Extract(uint value, int start, int length)
	{
		return (value >> start) & ((1u << length) - 1);
	}

	// Versión para ushort (instrucciones Thumb son 16-bit)
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint Extract(ushort value, int start, int length)
	{
		return (uint)((value >> start) & ((1u << length) - 1));
	}

	// Verifica si un bit específico está encendido
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBitSet(uint value, int bit)
	{
		return (value & (1u << bit)) != 0;
	}
    
	// Rotación a la derecha (ROR) - Crítico para ARM
	// Usamos System.Numerics.BitOperations que mapea a instrucciones nativas del CPU (ROR en x86/ARM)
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint RotateRight(uint value, int count)
	{
		return BitOperations.RotateRight(value, count);
	}
}
