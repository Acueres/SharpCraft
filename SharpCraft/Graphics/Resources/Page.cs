using SharpCraft.Rendering;
using SharpCraft.SharpMath;

using System.Diagnostics.CodeAnalysis;

namespace SharpCraft.Graphics.Resources;

internal unsafe class Page : IDisposable
{
    public uint FaceCapacity { get; }
    public uint SlotCapacity { get; }
    public bool IsEmpty => allocator.AllocationCount == 0;
    public int AllocationCount => allocator.AllocationCount;

    public uint UsedSlots => currentSlotId - (uint)freeSlotIds.Count;
    public uint RequestedFaces => allocator.RequestedRecords;
    public uint ReservedFaces => allocator.ReservedRecords;
    public uint FreeFaces => allocator.FreeRecords;
    public uint LargestFreeBlock => allocator.LargestFreeBlock;

    public BlockFaceBuffer FaceBuffer => faceBuffer;
    public SlotRecordBuffer SlotBuffer => slotBuffer;
    
    private readonly CpuAllocator allocator;
    
    private readonly BlockFaceBuffer faceBuffer;
    private readonly SlotRecordBuffer slotBuffer;
    
    private readonly HashSet<GpuAllocation> allocations = [];

    private uint currentSlotId;
    private readonly Stack<uint> freeSlotIds = [];

    public Page(uint faceCapacity, uint slotCapacity, IGpuBufferDevice device)
    {
        if (!MathUtilities.IsPowerOfTwo(faceCapacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(faceCapacity),
                faceCapacity,
                "Page face capacity must be a positive power of two.");
        }

        FaceCapacity = faceCapacity;

        long faceBufferCapacity = checked(faceCapacity * sizeof(VoxelFace));
        if (faceBufferCapacity > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(faceCapacity),
                faceCapacity, "Page face capacity must fit into uint.");
        }
        
        SlotCapacity = slotCapacity;
        
        if (slotCapacity is > ushort.MaxValue + 1 or < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCapacity),
                slotCapacity, "Page slot capacity must fit into ushort.");
        }
        
        faceBuffer = new BlockFaceBuffer(faceCapacity, device);

        try
        {
            slotBuffer = new SlotRecordBuffer(slotCapacity, device);
        }
        catch
        {
            faceBuffer.Dispose();
            throw;
        }
        
        allocator = new CpuAllocator(faceCapacity);
    }

    public void Release(GpuAllocation allocation)
    {
        ValidateAllocation(allocation);
        allocator.Release(allocation.CpuAllocation);
        allocations.Remove(allocation);
        freeSlotIds.Push(allocation.SlotId);
    }

    public bool TryAllocate(ReadOnlySpan<VoxelFace> faces, [MaybeNullWhen(false)] out GpuAllocation allocation)
    {
        allocation = null;
        var count = (uint)faces.Length;
        if (!TryGetSlotId(out var slotId))
        {
            return false;
        }
        
        if (!allocator.TryAllocate(count, out var cpuAllocation))
        {
            freeSlotIds.Push(slotId);
            return false;
        }
        
        allocation = new GpuAllocation(this, slotId, cpuAllocation);
        allocations.Add(allocation);
        
        return true;
    }

    private void ValidateAllocation(GpuAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);

        if (!allocations.Contains(allocation))
        {
            throw new InvalidOperationException("Allocation is not active.");
        }
        
        if (allocation.Page != this)
        {
            throw new InvalidOperationException("Allocation does not belong to page.");
        }

        if (!allocator.IsActive(allocation.CpuAllocation))
        {
            throw new InvalidOperationException("Allocation range is not active.");
        }
    }

    private bool TryGetSlotId(out uint slotId)
    {
        if (freeSlotIds.TryPop(out slotId))
        {
            return true;
        }

        if (currentSlotId >= SlotCapacity)
        {
            return false;
        }
        
        slotId = currentSlotId++;
        return true;
    }
    
    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        faceBuffer.Dispose();
        slotBuffer.Dispose();
        
        disposed = true;
    }
}
