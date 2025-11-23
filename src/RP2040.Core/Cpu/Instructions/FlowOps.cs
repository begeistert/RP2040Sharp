using System.Runtime.CompilerServices;

namespace RP2040.Core.Cpu.Instructions;

public static class FlowOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Branch(ushort opcode, CortexM0Plus cpu)
    {
        // B <label>
        // Encoding: 1110 0iii iiii iiii (0xE000)
        // El offset es de 11 bits con SIGNO.
        // Rango: -2048 a +2047 bytes (multiplicado por 2)
        
        // 1. Extraemos los 11 bits
        // uint rawOffset = BitUtils.Extract(opcode, 0, 11);
        var rawOffset = (uint)(opcode & 0x7FF);

        // 2. Sign Extension (Extensión de Signo)
        // El truco maestro: Si el bit 10 (signo) es 1, debemos rellenar 
        // los bits superiores con 1s para que C# entienda que es negativo.
        // Manera rápida en C#: (x ^ m) - m  donde m es la máscara del bit de signo.
        // O simplemente castear de forma inteligente.
        
        int offset = (int)rawOffset;
        if ((offset & 0x400) != 0) // Si el bit 10 está activo
        {
            offset |= ~0x7FF; // Llenar todo lo de arriba con 1s (Complemento a 2)
        }

        // 3. Multiplicar por 2 (instrucciones alineadas a 2 bytes)
        offset <<= 1;

        // 4. Calcular target. 
        // IMPORTANTE: En ARM, PC lee 4 bytes adelante de la instrucción actual 
        // debido al pipeline. Pero en emulación paso a paso, 
        // usualmente ya incrementaste PC += 2 en el 'Step()'.
        // Ajuste: Target = PC_actual + 4 + offset. 
        // (Dependerá de cómo incrementes PC en tu bucle principal, ajustemos esto abajo).
        
        // Asumiremos que en Step() ya hiciste PC += 2 después de leer el opcode.
        // El pipeline real de ARM PC es +4 respecto a la instrucción ejecutándose.
        // Así que PC efectivo = (PC_actual - 2) + 4 + offset = PC_actual + 2 + offset.
        
        // Nota: Esta lógica de PC pipeline es la fuente #1 de bugs.
        // En Cortex-M0, PC es "instruction address + 4".
        
        cpu.Registers.PC = (uint)((int)cpu.Registers.PC + 2 + offset);
        
        // Flush Pipeline? En M0 no hace falta simularlo, solo cambiamos PC.
    }
}