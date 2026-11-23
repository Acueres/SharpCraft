using SharpCraft.Rendering;
using SharpCraft.SharpMath;

namespace SharpCraft.Graphics.Resources;

internal unsafe class Arena : IDisposable
{
    public int PageCount => pages.Count;
    public int ActiveAllocations => pages.Sum(p => p.AllocationCount);
    public long TotalCapacityBytes => pages.Count * PageBytes;
    public long PageBytes => (long)pageFaceCapacity * sizeof(VoxelFace) + (long)pageSlotCapacity * sizeof(SlotRecord);

    private readonly uint pageFaceCapacity;
    private readonly uint pageSlotCapacity;
    
    private readonly List<Page> pages = [];

    private readonly IGpuBufferDevice device;

    public Arena(uint faceCapacity, uint slotCapacity, IGpuBufferDevice device)
    {
        if (!MathUtilities.IsPowerOfTwo(faceCapacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(faceCapacity),
                faceCapacity,
                "Page face capacity must be a positive power of two.");
        }
        
        long faceBufferCapacity = checked(faceCapacity * sizeof(VoxelFace));
        if (faceBufferCapacity > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(faceCapacity),
                faceCapacity, "Page face capacity must fit into uint.");
        }
        
        if (slotCapacity is > ushort.MaxValue + 1 or < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCapacity),
                slotCapacity, "Page slot capacity must fit into ushort.");
        }

        pageFaceCapacity = faceCapacity;
        pageSlotCapacity = slotCapacity;
        this.device = device;
    }

    public List<GpuAllocation> Allocate(ReadOnlySpan<VoxelFace> faces)
    {
        List<GpuAllocation> allocations = [];

        int start = 0;
        try
        {
            while (start < faces.Length)
            {
                // Prioritize allocating large contiguous chunks
                var slice = faces.Slice(start, Math.Min(faces.Length - start, (int)pageFaceCapacity));

                start += slice.Length;

                // Check if there are any pages with enough free space.
                bool allocated = false;
                foreach (var page in pages)
                {
                    allocated = page.TryAllocate(slice, out var allocation1);
                    if (!allocated) continue;

                    allocations.Add(allocation1!);
                    break;
                }

                if (allocated) continue;

                // Allocate a new page.
                var newPage = new Page(pageFaceCapacity, pageSlotCapacity, device);
                if (newPage.TryAllocate(slice, out var allocation2))
                {
                    pages.Add(newPage);
                    allocations.Add(allocation2);
                    continue;
                }

                throw new InvalidOperationException("Failed to allocate page.");
            }
        }
        catch
        {
            foreach (var allocation in allocations)
            {
                Release(allocation);
            }
            throw;
        }

        return allocations;
    }

    public void Release(GpuAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        
        var allocationPage = allocation.Page;

        if (!pages.Contains(allocationPage))
        {
            throw new InvalidOperationException("Allocation does not belong to this arena.");
        }
        
        allocationPage.Release(allocation);
    }

    public ArenaStatistics GetStatistics()
    {
        long usedSlots = 0, requested = 0, reserved = 0, free = 0;
        uint largest = 0;
        int allocations = 0;
        foreach (var page in pages)
        {
            usedSlots += page.UsedSlots;
            requested += page.RequestedFaces;
            reserved += page.ReservedFaces;
            free += page.FreeFaces;
            largest = Math.Max(largest, page.LargestFreeBlock);
            allocations += page.AllocationCount;
        }

        return new ArenaStatistics(pages.Count, TotalCapacityBytes, allocations,
            usedSlots, (long)pages.Count * pageSlotCapacity, requested, reserved, free, largest);
    }

    public void ReclaimMemory()
    {
        if (pages.Count <= 1) return;

        // Remove all empty pages except for one.
        bool emptyPagePreserved = false;
        pages.RemoveAll(p =>
        {
            if (!p.IsEmpty) return false;

            if (emptyPagePreserved)
            {
                p.Dispose();
                return true;
            }
            
            emptyPagePreserved = true;
            return false;
        });
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        foreach (var page in pages)
        {
            page.Dispose();
        }
        
        disposed = true;
    }
}

// Face counts are records; requested vs. reserved shows power-of-two rounding loss
internal readonly record struct ArenaStatistics(
    int Pages,
    long CapacityBytes,
    int Allocations,
    long UsedSlots,
    long SlotCapacity,
    long RequestedFaces,
    long ReservedFaces,
    long FreeFaces,
    long LargestFreeBlock);
