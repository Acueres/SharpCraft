using System.Diagnostics.CodeAnalysis;
using SharpCraft.SharpMath;

namespace SharpCraft.Graphics.Resources;

internal class CpuAllocator
{
    public int AllocationCount => allocations.Count;
    
    private readonly record struct FreeBlock(uint FirstRecord);
    
    private readonly Page page;
    private readonly uint totalCapacity;

    private readonly Dictionary<uint, HashSet<FreeBlock>> freeBlocksByCapacity;

    private readonly HashSet<CpuAllocation> allocations = [];

    public CpuAllocator(Page page, uint totalCapacity)
    {
        ArgumentNullException.ThrowIfNull(page);

        this.page = page;

        if (!MathUtilities.IsPowerOfTwo(totalCapacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalCapacity),
                totalCapacity,
                "Allocator capacity must be a positive power of two.");
        }

        this.totalCapacity = totalCapacity;

        freeBlocksByCapacity = new Dictionary<uint, HashSet<FreeBlock>>
        {
            [totalCapacity] = [new FreeBlock(0u)]
        };
    }

    public bool IsActive(CpuAllocation allocation)
    {
        return allocations.Contains(allocation);
    }

    public bool TryAllocate(uint count, [MaybeNullWhen(false)] out CpuAllocation allocation)
    {
        allocation = null;

        if (!TryFindAvailableCapacity(count, out uint freeCapacity, out uint targetCapacity))
        {
            return false;
        }

        var freeBlocks = freeBlocksByCapacity[freeCapacity];
        var freeBlock = freeBlocks.First();
        freeBlocks.Remove(freeBlock);

        var (block, fittingCapacity) = SplitBlockToCapacity(freeCapacity, targetCapacity, freeBlock);

        allocation = new CpuAllocation(page, fittingCapacity, block.FirstRecord, count);
        allocations.Add(allocation);

        return true;
    }

    public void Release(CpuAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);

        bool valid = allocations.Remove(allocation);

        if (!valid)
        {
            throw new InvalidOperationException("Allocation is not active in this CPU allocator.");
        }

        CoalesceBlocks(allocation.First, allocation.Capacity);
    }

    private bool TryFindAvailableCapacity(uint count, out uint freeCapacity, out uint targetCapacity)
    {
        freeCapacity = 0;
        targetCapacity = 0;

        if (count == 0 || count > totalCapacity) return false;

        targetCapacity = MathUtilities.RoundToNextPowerOfTwo(count);

        freeCapacity = targetCapacity;

        while (!freeBlocksByCapacity.TryGetValue(freeCapacity, out var b) || b.Count == 0)
        {
            if (freeCapacity == totalCapacity) return false;
            freeCapacity *= 2;
            if (freeCapacity > totalCapacity) return false;
        }

        return true;
    }

    private (FreeBlock, uint) SplitBlockToCapacity(uint freeCapacity, uint targetCapacity, FreeBlock block)
    {
        while (true)
        {
            if (targetCapacity == freeCapacity)
            {
                return (block, freeCapacity);
            }

            uint halved = freeCapacity / 2;

            // Block parameter is the first half already
            var secondHalf = new FreeBlock(halved + block.FirstRecord);

            if (freeBlocksByCapacity.TryGetValue(halved, out var halvedBlocks))
            {
                halvedBlocks.Add(secondHalf);
            }
            else
            {
                freeBlocksByCapacity.Add(halved, [secondHalf]);
            }

            freeCapacity = halved;
        }
    }
    
    private void CoalesceBlocks(uint first, uint capacity)
    {
        uint targetCapacity = capacity;
        uint buddyFirst = first;

        while (targetCapacity < totalCapacity)
        {
            var offset = buddyFirst ^ targetCapacity;
            
            if (!freeBlocksByCapacity.TryGetValue(targetCapacity, out var buddyBlocks)
                || !buddyBlocks.Remove(new FreeBlock(offset))) break;
            
            buddyFirst = Math.Min(buddyFirst, offset);
            targetCapacity *= 2;
        }

        var coalescedBlock = new FreeBlock(buddyFirst);
        if (freeBlocksByCapacity.TryGetValue(targetCapacity, out var targetBlocks))
        {
            targetBlocks.Add(coalescedBlock);
        }
        else
        {
            freeBlocksByCapacity.Add(targetCapacity, [coalescedBlock]);
        }
    }
}