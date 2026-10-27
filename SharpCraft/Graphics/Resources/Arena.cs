using SharpCraft.Rendering;
using SharpCraft.SharpMath;

namespace SharpCraft.Graphics.Resources;

internal class Arena
{
    public int PageCount => pages.Count;
    public int ActiveAllocations => pages.Sum(p => p.AllocationCount);

    private readonly uint pageCapacity;
    private readonly List<Page> pages = [];

    public Arena(uint capacity)
    {
        if (!MathUtilities.IsPowerOfTwo(capacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Page capacity must be a positive power of two.");
        }
        
        if (capacity > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity),
                capacity, "Page capacity must be less than array length limit.");
        }

        pageCapacity = capacity;
    }

    public List<CpuAllocation> Allocate(ReadOnlySpan<VoxelFace> faces)
    {
        List<CpuAllocation> allocations = [];

        int start = 0;
        try
        {
            while (start < faces.Length)
            {
                // Prioritize allocating large contiguous chunks
                var slice = faces.Slice(start, Math.Min(faces.Length - start, (int)pageCapacity));

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
                var newPage = new Page(pageCapacity);
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

    public void Release(CpuAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        
        var allocationPage = allocation.Page;

        if (!pages.Contains(allocationPage))
        {
            throw new InvalidOperationException("Allocation does not belong to this arena.");
        }
        
        allocationPage.Release(allocation);
    }

    public void ReclaimMemory()
    {
        if (pages.Count <= 1) return;

        // Remove all empty pages except for one.
        bool emptyPagePreserved = false;
        pages.RemoveAll(p =>
        {
            if (!p.IsEmpty) return false;

            if (emptyPagePreserved) return true;
            
            emptyPagePreserved = true;
            return false;
        });
    }
}