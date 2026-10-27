using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering;

namespace Tests;

public class ArenaTests
{
    [Fact]
    public void SmallStreamsSharePagesGrowWhenFullAndReuseReleasedRanges()
    {
        var arena = new Arena(8);

        Assert.Empty(arena.Allocate([]));
        arena.ReclaimMemory();
        Assert.Equal(0, arena.PageCount);
        Assert.Equal(0, arena.ActiveAllocations);

        var first = Assert.Single(arena.Allocate(CreateFaces(3, 10)));
        var secondFaces = CreateFaces(3, 20);
        var second = Assert.Single(arena.Allocate(secondFaces));
        Assert.Same(first.Page, second.Page);
        Assert.Equal(1, arena.PageCount);
        Assert.Equal(2, arena.ActiveAllocations);

        var thirdFaces = CreateFaces(1, 30);
        var third = Assert.Single(arena.Allocate(thirdFaces));
        Assert.NotSame(first.Page, third.Page);
        arena.ReclaimMemory();
        Assert.Equal(2, arena.PageCount);
        Assert.Equal(3, arena.ActiveAllocations);

        arena.Release(first);
        Assert.Equal(2, arena.ActiveAllocations);
        var replacementFaces = CreateFaces(3, 40);
        var replacement = Assert.Single(arena.Allocate(replacementFaces));
        Assert.Same(first.Page, replacement.Page);
        Assert.Equal(first.First, replacement.First);
        Assert.Equal(2, arena.PageCount);
        Assert.Equal(3, arena.ActiveAllocations);
        AssertFacesEqual(secondFaces, [second]);
        AssertFacesEqual(thirdFaces, [third]);
        AssertFacesEqual(replacementFaces, [replacement]);

        arena.Release(second);
        arena.Release(third);
        arena.Release(replacement);
        Assert.Equal(0, arena.ActiveAllocations);
        Assert.Equal(2, arena.PageCount);
    }

    [Fact]
    public void OversizedStreamPreservesFaceOrderAndUsesExistingSpaceForItsRemainder()
    {
        var arena = new Arena(8);
        var neighborFaces = CreateFaces(3, 10);
        var neighbor = Assert.Single(arena.Allocate(neighborFaces));
        var source = CreateFaces(19, 100);
        var expected = source.ToArray();

        var allocations = arena.Allocate(source);

        Assert.Equal(new uint[] { 8, 8, 3 }, allocations.Select(allocation => allocation.Count).ToArray());
        Assert.Equal(3, arena.PageCount);
        Assert.Equal(4, arena.ActiveAllocations);
        Assert.Same(neighbor.Page, allocations[2].Page);
        AssertFacesEqual(expected, allocations);
        AssertFacesEqual(neighborFaces, [neighbor]);

        source[0] = default;
        source[8] = default;
        source[^1] = default;
        AssertFacesEqual(expected, allocations);

        foreach (var allocation in allocations) arena.Release(allocation);
        Assert.Equal(1, arena.ActiveAllocations);
        AssertFacesEqual(neighborFaces, [neighbor]);
        arena.Release(neighbor);
        Assert.Equal(0, arena.ActiveAllocations);
    }

    [Fact]
    public void InvalidReleasesPreserveOwnershipCountsAndLiveData()
    {
        var arena = new Arena(8);
        var foreignArena = new Arena(8);
        var faces = CreateFaces(3, 10);
        var original = Assert.Single(arena.Allocate(faces));
        var foreignFaces = CreateFaces(3, 20);
        var foreign = Assert.Single(foreignArena.Allocate(foreignFaces));
        var fabricated = new CpuAllocation(original.Page, original.Capacity, original.First, original.Count);

        Assert.Throws<ArgumentNullException>(() => arena.Release(null!));
        Assert.Throws<InvalidOperationException>(() => arena.Release(foreign));
        Assert.Throws<InvalidOperationException>(() => arena.Release(fabricated));
        Assert.Equal(1, arena.ActiveAllocations);
        Assert.Equal(1, foreignArena.ActiveAllocations);
        AssertFacesEqual(faces, [original]);
        AssertFacesEqual(foreignFaces, [foreign]);

        arena.Release(original);
        Assert.Throws<InvalidOperationException>(() => arena.Release(original));
        Assert.Equal(0, arena.ActiveAllocations);

        var replacementFaces = CreateFaces(3, 30);
        var replacement = Assert.Single(arena.Allocate(replacementFaces));
        Assert.Same(original.Page, replacement.Page);
        Assert.Equal(original.First, replacement.First);
        Assert.Throws<InvalidOperationException>(() => arena.Release(original));
        Assert.Equal(1, arena.ActiveAllocations);
        AssertFacesEqual(replacementFaces, [replacement]);
    }

    [Fact]
    public void ReclamationPreservesLiveHandlesAndKeepsOneReusableEmptyPage()
    {
        var arena = new Arena(8);
        var faces = CreateFaces(40, 10);
        var allocations = arena.Allocate(faces);
        var emptyPages = new[] { allocations[0].Page, allocations[2].Page, allocations[4].Page };

        arena.Release(allocations[0]);
        arena.Release(allocations[2]);
        arena.Release(allocations[4]);
        Assert.Equal(5, arena.PageCount);
        Assert.Equal(2, arena.ActiveAllocations);

        arena.ReclaimMemory();
        arena.ReclaimMemory();
        Assert.Equal(3, arena.PageCount);
        Assert.Equal(2, arena.ActiveAllocations);
        AssertFacesEqual(faces.AsSpan(8, 8), [allocations[1]]);
        AssertFacesEqual(faces.AsSpan(24, 8), [allocations[3]]);

        var replacementFaces = CreateFaces(8, 100);
        var replacement = Assert.Single(arena.Allocate(replacementFaces));
        Assert.Contains(replacement.Page, emptyPages);
        Assert.Equal(3, arena.PageCount);
        Assert.Equal(3, arena.ActiveAllocations);
        AssertFacesEqual(replacementFaces, [replacement]);
        AssertFacesEqual(faces.AsSpan(8, 8), [allocations[1]]);
        AssertFacesEqual(faces.AsSpan(24, 8), [allocations[3]]);

        arena.Release(allocations[1]);
        arena.Release(allocations[3]);
        arena.Release(replacement);
        arena.ReclaimMemory();
        arena.ReclaimMemory();
        Assert.Equal(1, arena.PageCount);
        Assert.Equal(0, arena.ActiveAllocations);

        var warmPageFaces = CreateFaces(8, 200);
        var warmAllocation = Assert.Single(arena.Allocate(warmPageFaces));
        arena.ReclaimMemory();
        Assert.Equal(1, arena.PageCount);
        Assert.Equal(1, arena.ActiveAllocations);
        AssertFacesEqual(warmPageFaces, [warmAllocation]);
    }

    [Fact]
    public void ConstructorRejectsInvalidAndUnrepresentablePageCapacities()
    {
        foreach (uint capacity in new uint[] { 0, 12, 1u << 31 })
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Arena(capacity));
            Assert.Equal("capacity", exception.ParamName);
            Assert.Equal(capacity, Assert.IsType<uint>(exception.ActualValue));
        }
    }

    private static VoxelFace[] CreateFaces(int count, uint seed)
    {
        return Enumerable.Range(0, count).Select(index => new VoxelFace
        {
            Uint0 = 0x12340000u + seed + (uint)index,
            Uint1 = 0x89AB0000u + seed + (uint)index,
            Uint2 = 0xFEDC0000u + seed + (uint)index,
            Uint3 = 0x80000000u + seed + (uint)index
        }).ToArray();
    }

    private static void AssertFacesEqual(ReadOnlySpan<VoxelFace> expected, IReadOnlyList<CpuAllocation> allocations)
    {
        Assert.Equal(expected.Length, allocations.Sum(allocation => (int)allocation.Count));
        int index = 0;
        foreach (var allocation in allocations)
        {
            foreach (var actual in allocation.Page.GetFaces(allocation))
            {
                Assert.Equal(expected[index].Uint0, actual.Uint0);
                Assert.Equal(expected[index].Uint1, actual.Uint1);
                Assert.Equal(expected[index].Uint2, actual.Uint2);
                Assert.Equal(expected[index].Uint3, actual.Uint3);
                index++;
            }
        }
        Assert.Equal(expected.Length, index);
    }
}
