using SharpCraft.Rendering;
using SharpCraft.SharpMath;

using System.Diagnostics.CodeAnalysis;

namespace SharpCraft.Graphics.Resources;

internal class Page
{
    public uint Capacity { get; }
    public bool IsEmpty => allocator.AllocationCount == 0;
    public int AllocationCount => allocator.AllocationCount;
    
    private readonly VoxelFace[] storage;
    private readonly CpuAllocator allocator;

    public Page(uint capacity)
    {
        if (!MathUtilities.IsPowerOfTwo(capacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Page capacity must be a positive power of two.");
        }

        Capacity = capacity;

        if (capacity > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity),
                capacity, "Page capacity must be less than array length limit.");
        }

        storage = new VoxelFace[capacity];
        allocator = new CpuAllocator(this, capacity);
    }

    public ReadOnlySpan<VoxelFace> GetFaces(CpuAllocation allocation)
    {
        ValidateAllocation(allocation);
        
        return storage.AsSpan((int)allocation.First, (int)allocation.Count);
    }

    public void Release(CpuAllocation allocation)
    {
        ValidateAllocation(allocation);
        
        allocator.Release(allocation);
    }

    public bool TryAllocate(ReadOnlySpan<VoxelFace> faces, [MaybeNullWhen(false)] out CpuAllocation allocation)
    {
        var count = (uint)faces.Length;
        bool success = allocator.TryAllocate(count, out allocation);

        if (!success) return false;
        
        WriteFaces(allocation!.First, allocation.Count, faces);

        return true;
    }

    private void WriteFaces(uint first, uint count, ReadOnlySpan<VoxelFace> faces)
    {
        if (first >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(first), first, "First allocation index is greater than capacity.");
        }

        if (first + count > Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Number of faces is greater than capacity.");
        }
        
        uint end = first + count;
        for (int targetIndex = (int)first, sourceIndex = 0; targetIndex < end; targetIndex++, sourceIndex++)
        {
            VoxelFace face = faces[sourceIndex];
            storage[targetIndex] = face;
        }
    }

    private void ValidateAllocation(CpuAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        
        if (allocation.Page != this)
        {
            throw new InvalidOperationException("Allocation does not belong to page.");
        }

        if (!allocator.IsActive(allocation))
        {
            throw new InvalidOperationException("Allocation is not active.");
        }
    }
}