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

	private const uint EXC_RETURN_HANDLER = 0xFFFFFFF1; // Return to Handler mode, using MSP
	private const uint EXC_RETURN_THREAD_MSP = 0xFFFFFFF9; // Return to Thread mode, using MSP
	private const uint EXC_RETURN_THREAD_PSP = 0xFFFFFFFD; // Return to Thread mode, using PSP

	public CortexM0Plus (BusInterconnect bus)
	{
		Bus = bus;
		_decoder = InstructionDecoder.Instance;
		Reset ();
	}

	public void Reset ()
	{
		Registers.SP = Bus.ReadWord (0x00000000);
		Registers.PC = Bus.ReadWord (0x00000004);

		// Inicializar caché
		UpdateFetchCache (Registers.PC);

		Registers.N = false;
		Registers.Z = false;
		Registers.C = false;
		Registers.V = false;

		Cycles = 0;
	}

	/// <summary>
	/// Actualiza los punteros de caché cuando el PC salta a una región de memoria diferente.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private void UpdateFetchCache (uint pc)
	{
		_currentRegionId = pc >> 28;

		switch (_currentRegionId) {
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

	[MethodImpl (MethodImplOptions.AggressiveOptimization)]
	public void Run (int instructions)
	{
		var decoder = _decoder;

		// 1. Cargar caché a registros del CPU Host (Stack)
		// Esto elimina la indirección 'this._fetchPtr' dentro del while
		var fetchPtr = _fetchPtr;
		var fetchMask = _fetchMask;
		var regionId = _currentRegionId;

		while (instructions-- > 0) {
			var pc = Registers.PC;

			// 2. FAST GUARD: ¿Seguimos en la misma región de memoria?
			// (pc >> 28) es una operación de un solo ciclo.
			if ((pc >> 28) != regionId) {
				// FALLBACK: Cambio de región detectado.
				UpdateFetchCache (pc);

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
			var opcode = Unsafe.ReadUnaligned<ushort> (fetchPtr + (pc & fetchMask));

			// 4. PRE-UPDATE PC (Speculative)
			Registers.PC = pc + 2;

			Cycles++;

			// 5. DISPATCH
			decoder.Dispatch (opcode, this);
		}

		_currentRegionId = regionId;
		_fetchPtr = fetchPtr;
		_fetchMask = fetchMask;
	}

	// Step simple para Debugging
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void Step ()
	{
		var pc = Registers.PC;
		var opcode = Bus.ReadHalfWord (pc);
		Registers.PC = pc + 2;
		Cycles++;
		_decoder.Dispatch (opcode, this);
	}

	[MethodImpl (MethodImplOptions.NoInlining)] // NoInlining (it is not used commonly)
	public void UpdateStackPointerSource ()
	{
		if (Registers.IPSR != 0) return;

		var switchToPsp = (Registers.CONTROL & 2) != 0;

		if (switchToPsp) {
			Registers.MSP_Storage = Registers.SP;
			Registers.SP = Registers.PSP_Storage;
		}
		else {
			Registers.PSP_Storage = Registers.SP;
			Registers.SP = Registers.MSP_Storage;
		}
	}

	[MethodImpl (MethodImplOptions.NoInlining)]
	public void ExceptionEntry (uint exceptionNumber)
	{
		var framePtr = Registers.SP;

		var needsAlign = (framePtr & 4) != 0;
		var framePtrAlign = needsAlign ? 1u : 0u;

		var stackAdjust = 0x20u + (needsAlign ? 4u : 0u);
		var finalSp = framePtr - stackAdjust;

		var frameBase = finalSp;

		Bus.WriteWord (frameBase + 0x00, Registers.R0);
		Bus.WriteWord (frameBase + 0x04, Registers.R1);
		Bus.WriteWord (frameBase + 0x08, Registers.R2);
		Bus.WriteWord (frameBase + 0x0C, Registers.R3);
		Bus.WriteWord (frameBase + 0x10, Registers.R12);
		Bus.WriteWord (frameBase + 0x14, Registers.LR);
		Bus.WriteWord (frameBase + 0x18, Registers.PC & 0xFFFFFFFE); // Return Address

		var xpsr = Registers.GetxPsr () | (framePtrAlign << 9);
		Bus.WriteWord (frameBase + 0x1C, xpsr);

		if (Registers.IPSR > 0) {
			Registers.LR = EXC_RETURN_HANDLER;
		}
		else {
			Registers.LR = (Registers.CONTROL & 2) != 0 ? EXC_RETURN_THREAD_PSP : EXC_RETURN_THREAD_MSP;
		}

		if ((Registers.CONTROL & 2) != 0) {
			Registers.PSP_Storage = finalSp;
			Registers.SP = Registers.MSP_Storage;
		}
		else {
			Registers.SP = finalSp;
		}

		Registers.IPSR = exceptionNumber;
		Registers.CONTROL &= ~2u;

		uint vtor = 0; // TODO: Read from Registers.VTOR or PPB
		var vectorAddress = vtor + (exceptionNumber * 4);

		var targetPc = Bus.ReadWord (vectorAddress);
		Registers.PC = targetPc & 0xFFFFFFFE;

		Cycles += 12; // Exception Entry cost (aprox 12-15 ciclos)
	}

	[MethodImpl (MethodImplOptions.NoInlining)]
	public void ExceptionReturn (uint excReturn)
	{
		var returnToThread = (excReturn & 8) != 0;
		var usePsp = (excReturn & 4) != 0;

		if (!returnToThread && usePsp) {
			usePsp = false;
		}

		if (returnToThread) {
			Registers.IPSR = 0;

			if (usePsp) {
				Registers.MSP_Storage = Registers.SP;
				Registers.SP = Registers.PSP_Storage;
				Registers.CONTROL |= 2;
			}
			else {
				Registers.CONTROL &= ~2u;
			}
		}

		var framePtr = Registers.SP;

		Registers.R0 = Bus.ReadWord (framePtr + 0x00);
		Registers.R1 = Bus.ReadWord (framePtr + 0x04);
		Registers.R2 = Bus.ReadWord (framePtr + 0x08);
		Registers.R3 = Bus.ReadWord (framePtr + 0x0C);
		Registers.R12 = Bus.ReadWord (framePtr + 0x10);
		Registers.LR = Bus.ReadWord (framePtr + 0x14);
		var retPC = Bus.ReadWord (framePtr + 0x18);
		var xpsr = Bus.ReadWord (framePtr + 0x1C);

		Registers.N = (xpsr & 0x80000000) != 0;
		Registers.Z = (xpsr & 0x40000000) != 0;
		Registers.C = (xpsr & 0x20000000) != 0;
		Registers.V = (xpsr & 0x10000000) != 0;

		var alignAdjust = (xpsr & (1 << 9)) != 0;
		var stackFree = 0x20u + (alignAdjust ? 4u : 0u);

		Registers.SP += stackFree;
		Registers.PC = retPC & 0xFFFFFFFE;

		Cycles += 10;
	}
}
