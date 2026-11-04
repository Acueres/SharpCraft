using SharpCraft.Graphics.Resources;

namespace Tests;

public class CpuAllocationTests
{
    [Fact]
    public void HandlePreservesRequestedCountAndReservedRange()
    {
        var allocation = new CpuAllocation(8, 16, 5);
        Assert.Equal(8u, allocation.Capacity);
        Assert.Equal(16u, allocation.First);
        Assert.Equal(5u, allocation.Count);
        Assert.NotEqual(allocation, new CpuAllocation(8, 16, 5));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    public void CapacityMustBeAPositivePowerOfTwo(uint capacity)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new CpuAllocation(capacity, 0, 1));
        Assert.Equal("capacity", exception.ParamName);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(9u)]
    public void RequestedCountMustFitReservedCapacity(uint count)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new CpuAllocation(8, 0, count));
        Assert.Equal("count", exception.ParamName);
    }

    [Fact]
    public void RangeStartMustBeAlignedToReservedCapacity()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CpuAllocation(8, 3, 4));
        Assert.Equal("first", exception.ParamName);
    }

    [Fact]
    public void MaximumCapacityExhaustionDoesNotWrapTheSizeClassSearch()
    {
        const uint capacity = 1u << 31;
        var allocator = new CpuAllocator(capacity);

        Assert.True(allocator.TryAllocate(capacity, out var wholeRange));
        Assert.False(allocator.TryAllocate(1, out var exhausted));
        Assert.Null(exhausted);
        allocator.Release(wholeRange);
        Assert.True(allocator.TryAllocate(capacity, out var replacement));
        Assert.Equal(0u, replacement.First);
        Assert.Equal(capacity, replacement.Capacity);
        Assert.False(allocator.IsActive(wholeRange));
    }
}
