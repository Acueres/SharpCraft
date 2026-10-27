using SharpCraft.SharpMath;

namespace SharpCraft.Graphics.Resources;

internal class CpuAllocation
{
    public Page Page { get; }
    public uint Capacity { get; }
    public uint Count { get; }
    public uint First { get; }

    public CpuAllocation(Page page, uint capacity, uint first, uint count)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!MathUtilities.IsPowerOfTwo(capacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Allocation capacity must be a positive power of two.");
        }

        if (first % capacity != 0)
        {
            throw new ArgumentException(
                $"First record index {first} must be a multiple of " +
                $"allocation capacity {capacity}.",
                nameof(first));
        }

        if (count == 0 || count > capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"Record count must be between 1 and {capacity}, inclusive.");
        }

        Page = page;
        Capacity = capacity;
        First = first;
        Count = count;
    }
}