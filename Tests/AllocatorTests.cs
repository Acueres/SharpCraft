using SharpCraft.Graphics.Resources;

namespace Tests;

public class AllocatorTests
{
    [Fact]
    public void AllocationPreservesPageAndCountAndRoundsCapacityUp()
    {
        const uint capacity = 16;
        
        Page page = new(capacity);
        var allocator = new CpuAllocator(page, capacity);

        var allocation = Allocate(allocator, 3);

        Assert.Same(page, allocation.Page);
        Assert.Equal(3u, allocation.Count);
        Assert.Equal(4u, allocation.Capacity);
        Assert.Equal(0u, allocation.First % allocation.Capacity);
        Assert.InRange(allocation.First, 0u, capacity - allocation.Capacity);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(12u)]
    public void ConstructorRejectsCapacityThatIsNotAPositivePowerOfTwo(uint capacity)
    {
        var validPage = new Page(2);
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CpuAllocator(validPage, capacity));

        Assert.Equal("totalCapacity", exception.ParamName);
    }

    [Fact]
    public void FailedRequestsDoNotConsumeOrChangeAvailableStorage()
    {
        const uint capacity = 16;
        
        var allocator = new CpuAllocator(new Page(capacity), capacity);

        AssertUnavailable(allocator, 0);
        AssertUnavailable(allocator, 17);
        AssertUnavailable(allocator, uint.MaxValue);

        var wholePage = Allocate(allocator, capacity);
        Assert.Equal(0u, wholePage.First);
        AssertUnavailable(allocator, 1);

        allocator.Release(wholePage);

        var replacement = Allocate(allocator, capacity);
        Assert.Equal(0u, replacement.First);
        Assert.Equal(16u, replacement.Capacity);
    }

    [Fact]
    public void ReleaseCoalescesBuddiesRecursivelyWithoutCrossingLiveAllocations()
    {
        const uint capacity = 16;
        
        var allocator = new CpuAllocator(new Page(capacity), capacity);
        var first = Allocate(allocator, 3);
        var second = Allocate(allocator, 3);
        var otherHalf = Allocate(allocator, 8);

        allocator.Release(first);
        AssertUnavailable(allocator, 8);

        allocator.Release(second);
        var mergedHalf = Allocate(allocator, 8);
        Assert.Equal(Math.Min(first.First, second.First), mergedHalf.First);

        allocator.Release(otherHalf);
        allocator.Release(mergedHalf);

        var wholePage = Allocate(allocator, capacity);
        Assert.Equal(0u, wholePage.First);
        Assert.Equal(16u, wholePage.Capacity);
    }

    [Fact]
    public void ReleaseDoesNotMergeAdjacentBlocksThatAreNotBuddies()
    {
        const uint capacity = 16;
        
        var allocator = new CpuAllocator(new Page(capacity), capacity);
        var blocks = Enumerable.Range(0, 4)
            .Select(_ => Allocate(allocator, 4))
            .OrderBy(allocation => allocation.First)
            .ToArray();

        allocator.Release(blocks[1]);
        allocator.Release(blocks[2]);

        AssertUnavailable(allocator, 8);

        allocator.Release(blocks[0]);
        allocator.Release(blocks[3]);
        Assert.Equal(0u, Allocate(allocator, capacity).First);
    }

    [Fact]
    public void InvalidReleasesDoNotFreeLiveStorage()
    {
        const uint capacity = 16;
        
        Page page = new(capacity);
        var allocator = new CpuAllocator(page, capacity);
        var foreignAllocator = new CpuAllocator(page, capacity);
        var original = Allocate(allocator, capacity);
        var foreign = Allocate(foreignAllocator, capacity);
        var fabricated = new CpuAllocation(page, original.Capacity, original.First, original.Count);

        Assert.Throws<ArgumentNullException>(() => allocator.Release(null!));
        Assert.Throws<InvalidOperationException>(() => allocator.Release(foreign));
        Assert.Throws<InvalidOperationException>(() => allocator.Release(fabricated));
        AssertUnavailable(allocator, 1);

        allocator.Release(original);
        Assert.Throws<InvalidOperationException>(() => allocator.Release(original));

        var replacement = Allocate(allocator, capacity);
        Assert.NotSame(original, replacement);
        Assert.Equal(original.First, replacement.First);
        Assert.Throws<InvalidOperationException>(() => allocator.Release(original));
        AssertUnavailable(allocator, 1);

        allocator.Release(replacement);
        Assert.Equal(0u, Allocate(allocator, capacity).First);
    }

    [Fact]
    public void MixedAllocationAndReleasePreservesRangesAndRecoversWholePage()
    {
        const int capacity = 128;
        Page page = new(capacity);
        var allocator = new CpuAllocator(page, capacity);
        var random = new Random(1337);
        List<CpuAllocation> active = [];
        bool[] occupied = new bool[capacity];

        for (int step = 0; step < 1_000; step++)
        {
            if (active.Count > 0 && random.Next(2) == 0)
            {
                int index = random.Next(active.Count);
                var allocation = active[index];
                allocator.Release(allocation);
                MarkRange(allocation, occupied, false);
                active.RemoveAt(index);
                continue;
            }

            uint count = (uint)random.Next(1, 33);
            int reserved = 1;
            while (reserved < count) reserved *= 2;

            bool canFit = Enumerable.Range(0, capacity / reserved).Any(block =>
                !occupied.AsSpan(block * reserved, reserved).Contains(true));

            bool success = allocator.TryAllocate(count, out var allocationResult);
            Assert.Equal(canFit, success);

            if (!success)
            {
                Assert.Null(allocationResult);
                continue;
            }

            var allocated = Assert.IsType<CpuAllocation>(allocationResult);
            Assert.Same(page, allocated.Page);
            Assert.Equal(count, allocated.Count);
            Assert.Equal((uint)reserved, allocated.Capacity);
            Assert.Equal(0u, allocated.First % allocated.Capacity);
            Assert.InRange(allocated.First, 0u, capacity - allocated.Capacity);
            MarkRange(allocated, occupied, true);
            active.Add(allocated);
        }

        var remaining = active.ToArray();
        random.Shuffle(remaining);
        foreach (var allocation in remaining)
        {
            allocator.Release(allocation);
            MarkRange(allocation, occupied, false);
        }

        Assert.DoesNotContain(true, occupied);
        var wholePage = Allocate(allocator, capacity);
        Assert.Equal(0u, wholePage.First);
        Assert.Equal((uint)capacity, wholePage.Capacity);
        AssertUnavailable(allocator, 1);
    }

    private static CpuAllocation Allocate(CpuAllocator allocator, uint count)
    {
        Assert.True(allocator.TryAllocate(count, out var allocation));
        return Assert.IsType<CpuAllocation>(allocation);
    }

    private static void AssertUnavailable(CpuAllocator allocator, uint count)
    {
        Assert.False(allocator.TryAllocate(count, out var allocation));
        Assert.Null(allocation);
    }

    private static void MarkRange(CpuAllocation allocation, bool[] occupied, bool value)
    {
        for (uint record = allocation.First; record < allocation.First + allocation.Capacity; record++)
        {
            Assert.Equal(!value, occupied[record]);
            occupied[record] = value;
        }
    }
}
