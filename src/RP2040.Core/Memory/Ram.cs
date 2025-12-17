using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace RP2040.Core.Memory;

public unsafe class RandomAccessMemory : IMemoryMappedDevice, IDisposable
{
	readonly byte[] _memory;
	GCHandle _pinnedHandle;

	public readonly byte* BasePtr;
	public uint Size {
		get;
	}

	public RandomAccessMemory (int size)
	{
		_memory = new byte[size];
		_pinnedHandle = GCHandle.Alloc (_memory, GCHandleType.Pinned);
		BasePtr = (byte*)_pinnedHandle.AddrOfPinnedObject ();
		Size = (uint)size;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public byte ReadByte (uint address)
	{
		return BasePtr[address];
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public ushort ReadHalfWord (uint address)
	{
		return Unsafe.ReadUnaligned<ushort> (BasePtr + address);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public uint ReadWord (uint address)
	{
		return Unsafe.ReadUnaligned<uint> (BasePtr + address);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void WriteByte (uint address, byte value) => BasePtr[address] = value;

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void WriteHalfWord (uint address, ushort value) => Unsafe.WriteUnaligned (BasePtr + address, value);

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void WriteWord (uint address, uint value) => Unsafe.WriteUnaligned (BasePtr + address, value);

	public void Dispose ()
	{
		if (_pinnedHandle.IsAllocated) {
			_pinnedHandle.Free ();
		}
	}
}
