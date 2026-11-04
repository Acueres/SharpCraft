using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering;

namespace Tests;

public unsafe class ArenaTests
{
    [Fact]
    public void SmallStreamsSharePagesGrowWhenFullAndReuseReleasedRangesAndSlots()
    {
        var device = new FakeBufferDevice();
        using var arena = new Arena(8, 8, device);

        Assert.Empty(arena.Allocate([]));
        arena.ReclaimMemory();
        Assert.Equal(0, arena.PageCount);
        Assert.Equal(0, arena.ActiveAllocations);
        Assert.Equal(0L, arena.TotalCapacityBytes);
        Assert.Empty(device.Created);

        var first = Assert.Single(arena.Allocate(Faces(3)));
        var second = Assert.Single(arena.Allocate(Faces(3)));
        Assert.Same(first.Page, second.Page);
        Assert.NotEqual(first.SlotId, second.SlotId);
        Assert.Equal(1, arena.PageCount);

        var third = Assert.Single(arena.Allocate(Faces(1)));
        Assert.NotSame(first.Page, third.Page);
        Assert.Equal(first.SlotId, third.SlotId); // IDs are page-local.
        Assert.Equal(2, arena.PageCount);
        Assert.Equal(3, arena.ActiveAllocations);
        Assert.Equal(2L * (8 * 16 + 8 * 48), arena.TotalCapacityBytes);
        Assert.Equal(arena.TotalCapacityBytes, device.LiveBytes);

        arena.Release(first);
        var replacement = Assert.Single(arena.Allocate(Faces(3)));
        Assert.Same(first.Page, replacement.Page);
        Assert.Equal(first.CpuAllocation.First, replacement.CpuAllocation.First);
        Assert.Equal(first.SlotId, replacement.SlotId);
        Assert.Equal(3, arena.ActiveAllocations);
        Assert.Equal(4, device.Created.Count);

        arena.Release(second);
        arena.Release(third);
        arena.Release(replacement);
        Assert.Equal(0, arena.ActiveAllocations);
        Assert.Equal(2, arena.PageCount);
    }

    [Fact]
    public void SlotExhaustionGrowsArenaDespiteAvailableFaceCapacity()
    {
        var device = new FakeBufferDevice();
        using var arena = new Arena(8, 1, device);
        var first = Assert.Single(arena.Allocate(Faces(1)));
        var second = Assert.Single(arena.Allocate(Faces(1)));

        Assert.NotSame(first.Page, second.Page);
        Assert.Equal(2, arena.PageCount);
        Assert.Equal(0u, first.SlotId);
        Assert.Equal(0u, second.SlotId);

        arena.Release(first);
        var replacement = Assert.Single(arena.Allocate(Faces(1)));
        Assert.Same(first.Page, replacement.Page);
        Assert.Equal(first.SlotId, replacement.SlotId);
        Assert.Equal(2, arena.ActiveAllocations);
        Assert.Equal(4, device.Created.Count);
    }

    [Fact]
    public void OversizedStreamReturnsOrderedCountsAndPlacesRemainderInExistingSpace()
    {
        var device = new FakeBufferDevice();
        using var arena = new Arena(8, 4, device);
        var neighbor = Assert.Single(arena.Allocate(Faces(3)));

        var allocations = arena.Allocate(Faces(19));

        Assert.Equal(new uint[] { 8, 8, 3 }, allocations.Select(a => a.CpuAllocation.Count).ToArray());
        Assert.Equal(new uint[] { 8, 8, 4 }, allocations.Select(a => a.CpuAllocation.Capacity).ToArray());
        Assert.Equal(new uint[] { 0, 0, 4 }, allocations.Select(a => a.CpuAllocation.First).ToArray());
        Assert.NotSame(allocations[0].Page, allocations[1].Page);
        Assert.Same(neighbor.Page, allocations[2].Page);
        Assert.NotEqual(neighbor.SlotId, allocations[2].SlotId);
        Assert.Equal(19, allocations.Sum(a => (int)a.CpuAllocation.Count));
        Assert.Equal(3, arena.PageCount);
        Assert.Equal(4, arena.ActiveAllocations);
        Assert.Equal(arena.TotalCapacityBytes, device.LiveBytes);

        foreach (var allocation in allocations) arena.Release(allocation);
        Assert.Equal(1, arena.ActiveAllocations);
        arena.Release(neighbor);
        Assert.Equal(0, arena.ActiveAllocations);
    }

    [Fact]
    public void InvalidReleasesPreserveOwnershipCountsAndLiveRanges()
    {
        var device = new FakeBufferDevice();
        using var arena = new Arena(8, 4, device);
        using var foreignArena = new Arena(8, 4, device);
        var original = Assert.Single(arena.Allocate(Faces(3)));
        var foreign = Assert.Single(foreignArena.Allocate(Faces(3)));
        var fabricated = new GpuAllocation(original.Page, original.SlotId, original.CpuAllocation);

        Assert.Throws<ArgumentNullException>(() => arena.Release(null!));
        Assert.Throws<InvalidOperationException>(() => arena.Release(foreign));
        Assert.Throws<InvalidOperationException>(() => arena.Release(fabricated));
        Assert.Equal(1, arena.ActiveAllocations);
        Assert.Equal(1, foreignArena.ActiveAllocations);

        arena.Release(original);
        Assert.Throws<InvalidOperationException>(() => arena.Release(original));
        var replacement = Assert.Single(arena.Allocate(Faces(3)));
        Assert.Same(original.Page, replacement.Page);
        Assert.Equal(original.CpuAllocation.First, replacement.CpuAllocation.First);
        Assert.Equal(original.SlotId, replacement.SlotId);
        Assert.Throws<InvalidOperationException>(() => arena.Release(original));
        Assert.Equal(1, arena.ActiveAllocations);
    }

    [Fact]
    public void ReclamationDisposesExcessEmptyPagesAndKeepsLiveHandlesAndOneWarmPage()
    {
        var device = new FakeBufferDevice();
        using var arena = new Arena(8, 4, device);
        var allocations = arena.Allocate(Faces(40));
        var warmPage = allocations[0].Page;
        var discardedPages = new[] { allocations[2].Page, allocations[4].Page };

        arena.Release(allocations[0]);
        arena.Release(allocations[2]);
        arena.Release(allocations[4]);
        Assert.Equal(5, arena.PageCount);
        Assert.Equal(2, arena.ActiveAllocations);
        Assert.Empty(device.Released);

        arena.ReclaimMemory();
        arena.ReclaimMemory();

        Assert.Equal(3, arena.PageCount);
        Assert.Equal(2, arena.ActiveAllocations);
        Assert.Equal(4, device.Released.Count);
        Assert.Equal(arena.TotalCapacityBytes, device.LiveBytes);
        foreach (var page in discardedPages)
        {
            Assert.Contains((nint)page.FaceBuffer.Handle, device.Released);
            Assert.Contains((nint)page.SlotBuffer.Handle, device.Released);
        }
        Assert.True(device.IsLive(allocations[1].Page.FaceBuffer.Handle));
        Assert.True(device.IsLive(allocations[3].Page.SlotBuffer.Handle));
        Assert.True(device.IsLive(warmPage.FaceBuffer.Handle));

        var replacement = Assert.Single(arena.Allocate(Faces(8)));
        Assert.Same(warmPage, replacement.Page);
        Assert.Equal(10, device.Created.Count);

        // Releasing retained handles also verifies that reclamation did not invalidate them.
        arena.Release(allocations[1]);
        arena.Release(allocations[3]);
        arena.Release(replacement);
        arena.ReclaimMemory();

        Assert.Equal(1, arena.PageCount);
        Assert.Equal(0, arena.ActiveAllocations);
        Assert.Equal(2, device.LiveCount);
        Assert.Equal(8L * 16 + 4L * 48, arena.TotalCapacityBytes);
        Assert.Equal(arena.TotalCapacityBytes, device.LiveBytes);
        var warmAllocation = Assert.Single(arena.Allocate(Faces(8)));
        Assert.Same(warmPage, warmAllocation.Page);
        Assert.Equal(10, device.Created.Count);
    }

    [Fact]
    public void DisposalReleasesAllRetainedPageBuffersExactlyOnce()
    {
        var device = new FakeBufferDevice();
        var arena = new Arena(8, 4, device);
        arena.Allocate(Faces(19));
        Assert.Equal(6, device.LiveCount);

        arena.Dispose();
        arena.Dispose();

        Assert.Equal(device.Created.Count, device.Released.Count);
        Assert.Equal(0, device.LiveCount);
        Assert.Equal(0L, device.LiveBytes);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(12u)]
    [InlineData(1u << 28)]
    [InlineData(1u << 31)]
    public void InvalidFaceCapacitiesAreRejectedBeforePageCreation(uint capacity)
    {
        var device = new FakeBufferDevice();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Arena(capacity, 1, device));
        Assert.Equal("faceCapacity", exception.ParamName);
        Assert.Empty(device.Created);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(65_537u)]
    [InlineData(uint.MaxValue)]
    public void InvalidSlotCapacitiesAreRejectedBeforePageCreation(uint capacity)
    {
        var device = new FakeBufferDevice();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Arena(8, capacity, device));
        Assert.Equal("slotCapacity", exception.ParamName);
        Assert.Empty(device.Created);
    }

    [Fact]
    public void CapacityAccountingUsesWideArithmeticForLargePages()
    {
        var device = new FakeBufferDevice();
        using var arena = new Arena(1u << 27, 1, device);
        var first = Assert.Single(arena.Allocate(Faces(1)));
        var second = Assert.Single(arena.Allocate(Faces(1)));

        Assert.NotSame(first.Page, second.Page);
        Assert.Equal(2L * ((1L << 27) * 16 + 48), arena.TotalCapacityBytes);
        Assert.True(arena.TotalCapacityBytes > uint.MaxValue);
        Assert.Equal(arena.TotalCapacityBytes, device.LiveBytes);
    }

    private static VoxelFace[] Faces(int count) => new VoxelFace[count];
}
