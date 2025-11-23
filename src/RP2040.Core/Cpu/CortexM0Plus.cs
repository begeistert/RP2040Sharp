using System.Runtime.CompilerServices;
using RP2040.Core.Memory;

[module: SkipLocalsInit]
namespace RP2040.Core.Cpu;

public unsafe class CortexM0Plus
{
    public readonly BusInterconnect Bus;
    public Registers Registers;
    public long Cycles;
    
    private readonly InstructionDecoder _decoder;
    
    // CACHÉ DE FETCH LOCAL
    // Apunta directamente al buffer interno de RandomAccessMemory
    private byte* _fetchPtr;
    private uint _fetchMask;
    private uint _currentRegionId;

    public CortexM0Plus(BusInterconnect bus)
    {
       Bus = bus;
       _decoder = new InstructionDecoder();
       Reset();
    }

    public void Reset()
    {
       Registers.SP = Bus.ReadWord(0x00000000);
       Registers.PC = Bus.ReadWord(0x00000004);
       
       // Inicializar caché
       UpdateFetchCache(Registers.PC);
        
       Registers.N = false;
       Registers.Z = false;
       Registers.C = false;
       Registers.V = false;
       
       Cycles = 0;
    }
    
    /// <summary>
    /// Actualiza los punteros de caché cuando el PC salta a una región de memoria diferente.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateFetchCache(uint pc)
    {
       _currentRegionId = pc >> 28;

       switch (_currentRegionId)
       {
          case BusInterconnect.REGION_FLASH:
             _fetchPtr = Bus.PtrFlash;
             _fetchMask = BusInterconnect.MASK_FLASH & ~1u;
             break;
          case BusInterconnect.REGION_SRAM:
             _fetchPtr = Bus.PtrSram;
             _fetchMask = BusInterconnect.MASK_SRAM & ~1u;
             break;
          case BusInterconnect.REGION_BOOTROM:
             _fetchPtr = Bus.PtrBootRom;
             _fetchMask = BusInterconnect.MASK_BOOTROM & ~1u;
             break;
          default:
             _fetchPtr = null; // Detiene el fast-fetch
             break;
       }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveOptimization)] 
    public void Run(int instructions)
    {
       var decoder = _decoder;
       
       // 1. Cargar caché a registros del CPU Host (Stack)
       // Esto elimina la indirección 'this._fetchPtr' dentro del while
       var fetchPtr = _fetchPtr;
       var fetchMask = _fetchMask;
       var regionId = _currentRegionId;
       
       while (instructions-- > 0)
       {
          var pc = Registers.PC;

          // 2. FAST GUARD: ¿Seguimos en la misma región de memoria?
          // (pc >> 28) es una operación de un solo ciclo.
          if ((pc >> 28) != regionId) 
          {
             // FALLBACK: Cambio de región detectado.
             UpdateFetchCache(pc);
             
             // Recargar caché local
             fetchPtr = _fetchPtr;
             fetchMask = _fetchMask;
             regionId = _currentRegionId;
             
             // Si saltamos a memoria no ejecutable, abortamos el batch
             if (fetchPtr == null) break; 
          }

          // 3. FETCH ULTRA-RÁPIDO
          // (pc & ~1u): Alineación obligatoria en ARM Thumb (elimina bit 0).
          // & fetchMask: Mantiene el acceso dentro del buffer Pinned (seguridad).
          var opcode = Unsafe.ReadUnaligned<ushort>(fetchPtr + (pc & fetchMask));
          
          // 4. PRE-UPDATE PC (Speculative)
          Registers.PC = pc + 2;
          
          Cycles++;

          // 5. DISPATCH
          decoder.Dispatch(opcode, this);
       }
       
       // 6. Guardar estado de caché al salir (por si Run se llama de nuevo)
       _currentRegionId = regionId;
       _fetchPtr = fetchPtr;
       _fetchMask = fetchMask;
    }

    // Step simple para Debugging
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Step()
    {
       var pc = Registers.PC;
       // Usamos el Bus normal que maneja lógica segura y unaligned
       var opcode = Bus.ReadHalfWord(pc); 
       Registers.PC = pc + 2;
       Cycles++;
       _decoder.Dispatch(opcode, this);
    }
}