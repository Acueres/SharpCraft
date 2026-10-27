using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering;

namespace Tests;

public class PageTests
{
    [Fact]
    public void AllocationCopiesAllFaceWordsAndExposesOnlyTheRequestedCount()
    {
        var page = new Page(8);
        var source = CreateFaces(3, 10);
        var expected = source.ToArray();

        Assert.True(page.IsEmpty);
        var allocation = Allocate(page, source);

        Assert.Same(page, allocation.Page);
        Assert.Equal(8u, page.Capacity);
        Assert.Equal(3u, allocation.Count);
        Assert.Equal(4u, allocation.Capacity);
        Assert.False(page.IsEmpty);
        AssertFacesEqual(expected, source);
        AssertFacesEqual(expected, page.GetFaces(allocation));

        source[0] = default;
        source[1].Uint2 = 0;
        AssertFacesEqual(expected, page.GetFaces(allocation));
    }

    [Fact]
    public void FailedRequestsAndRangeReusePreserveNeighborsAndEmptyState()
    {
        var page = new Page(8);

        Assert.False(page.TryAllocate([], out var empty));
        Assert.Null(empty);
        Assert.False(page.TryAllocate(CreateFaces(9, 10), out var oversized));
        Assert.Null(oversized);
        Assert.True(page.IsEmpty);

        var firstFaces = CreateFaces(3, 20);
        var neighborFaces = CreateFaces(3, 40);
        var first = Allocate(page, firstFaces);
        var neighbor = Allocate(page, neighborFaces);

        Assert.False(page.TryAllocate(CreateFaces(1, 60), out var exhausted));
        Assert.Null(exhausted);
        AssertFacesEqual(firstFaces, page.GetFaces(first));
        AssertFacesEqual(neighborFaces, page.GetFaces(neighbor));

        page.Release(first);
        Assert.False(page.IsEmpty);
        var replacementFaces = CreateFaces(3, 80);
        var replacement = Allocate(page, replacementFaces);

        Assert.Equal(first.First, replacement.First);
        AssertFacesEqual(replacementFaces, page.GetFaces(replacement));
        AssertFacesEqual(neighborFaces, page.GetFaces(neighbor));

        page.Release(neighbor);
        Assert.False(page.IsEmpty);
        page.Release(replacement);
        Assert.True(page.IsEmpty);

        var wholePageFaces = CreateFaces(8, 100);
        var wholePage = Allocate(page, wholePageFaces);
        Assert.Equal(0u, wholePage.First);
        AssertFacesEqual(wholePageFaces, page.GetFaces(wholePage));
        page.Release(wholePage);
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public void ReadsAndReleasesRejectInvalidHandlesWithoutChangingLiveStorage()
    {
        var page = new Page(8);
        var faces = CreateFaces(3, 10);
        var original = Allocate(page, faces);
        var otherPage = new Page(8);
        var foreign = Allocate(otherPage, CreateFaces(3, 20));
        var fabricated = new CpuAllocation(page, original.Capacity, original.First, original.Count);

        Assert.Throws<ArgumentNullException>(() => { page.GetFaces(null!); });
        Assert.Throws<ArgumentNullException>(() => page.Release(null!));
        AssertRejected(page, foreign);
        AssertRejected(page, fabricated);
        AssertFacesEqual(faces, page.GetFaces(original));
        AssertFacesEqual(CreateFaces(3, 20), otherPage.GetFaces(foreign));
        Assert.False(page.IsEmpty);

        page.Release(original);
        Assert.True(page.IsEmpty);
        AssertRejected(page, original);

        var replacementFaces = CreateFaces(3, 40);
        var replacement = Allocate(page, replacementFaces);
        Assert.Equal(original.First, replacement.First);
        AssertRejected(page, original);
        Assert.False(page.IsEmpty);
        AssertFacesEqual(replacementFaces, page.GetFaces(replacement));
        page.Release(replacement);
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public void ConstructorRejectsInvalidAndUnrepresentableCapacities()
    {
        foreach (uint capacity in new uint[] { 0, 12, 1u << 31 })
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Page(capacity));
            Assert.Equal("capacity", exception.ParamName);
            Assert.Equal(capacity, Assert.IsType<uint>(exception.ActualValue));
        }
    }

    private static CpuAllocation Allocate(Page page, ReadOnlySpan<VoxelFace> faces)
    {
        Assert.True(page.TryAllocate(faces, out var allocation));
        return Assert.IsType<CpuAllocation>(allocation);
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

    private static void AssertFacesEqual(ReadOnlySpan<VoxelFace> expected, ReadOnlySpan<VoxelFace> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Uint0, actual[index].Uint0);
            Assert.Equal(expected[index].Uint1, actual[index].Uint1);
            Assert.Equal(expected[index].Uint2, actual[index].Uint2);
            Assert.Equal(expected[index].Uint3, actual[index].Uint3);
        }
    }

    private static void AssertRejected(Page page, CpuAllocation allocation)
    {
        Assert.Throws<InvalidOperationException>(() => { page.GetFaces(allocation); });
        Assert.Throws<InvalidOperationException>(() => page.Release(allocation));
    }
}
