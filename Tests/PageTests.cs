using SDL;
using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering;

namespace Tests;

public unsafe class PageTests
{
    [Fact]
    public void PageCreatesFixedBuffersInRecordUnitsAndAllocationsShareThem()
    {
        var device = new FakeBufferDevice();
        using var page = new Page(8, 4, device);
        var source = CreateFaces(3);
        var expected = source.ToArray();

        Assert.True(page.IsEmpty);
        Assert.Equal(8u, page.FaceCapacity);
        Assert.Equal(4u, page.SlotCapacity);
        Assert.Collection(device.Created,
            buffer =>
            {
                Assert.Equal(8u * 16, buffer.Bytes);
                Assert.Equal(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, buffer.Usage);
            },
            buffer =>
            {
                Assert.Equal(4u * 48, buffer.Bytes);
                Assert.Equal(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, buffer.Usage);
            });

        var first = Allocate(page, source);
        var second = Allocate(page, source);

        Assert.Same(page, first.Page);
        Assert.Equal(3u, first.CpuAllocation.Count);
        Assert.Equal(4u, first.CpuAllocation.Capacity);
        Assert.Equal(0u, first.CpuAllocation.First);
        Assert.Equal(4u, second.CpuAllocation.First);
        Assert.NotEqual(first.SlotId, second.SlotId);
        Assert.Equal(2, page.AllocationCount);
        Assert.False(page.IsEmpty);
        Assert.Equal(expected, source);
        Assert.Equal(2, device.Created.Count);
        Assert.Equal(8L * 16, page.FaceBuffer.CapacityBytes);
        Assert.Equal(4L * 48, page.SlotBuffer.CapacityBytes);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(4u)]
    public void SlotExhaustionStopsAtPageLimitDespiteFreeFaceSpaceAndReleasedIdsAreReused(uint slots)
    {
        var device = new FakeBufferDevice();
        using var page = new Page(8, slots, device);
        var allocations = new List<GpuAllocation>();

        for (uint id = 0; id < slots; id++)
        {
            var allocation = Allocate(page, CreateFaces(1));
            Assert.Equal(id, allocation.SlotId);
            allocations.Add(allocation);
        }

        Assert.False(page.TryAllocate(CreateFaces(1), out var unavailable));
        Assert.Null(unavailable);
        Assert.Equal((int)slots, page.AllocationCount);

        var original = allocations[0];
        page.Release(original);
        var replacement = Allocate(page, CreateFaces(1));
        Assert.Equal(original.SlotId, replacement.SlotId);
        Assert.Equal(original.CpuAllocation.First, replacement.CpuAllocation.First);
        Assert.Throws<InvalidOperationException>(() => page.Release(original));
        Assert.Equal((int)slots, page.AllocationCount);
    }

    [Fact]
    public void EntireSixteenBitSlotIdRangeIsAvailable()
    {
        var device = new FakeBufferDevice();
        using var page = new Page(1u << 16, 1u << 16, device);
        var face = CreateFaces(1);

        for (uint id = 0; id <= ushort.MaxValue; id++)
        {
            Assert.True(page.TryAllocate(face, out var allocation));
            Assert.Equal(id, allocation.SlotId);
        }

        Assert.Equal(65_536, page.AllocationCount);
        Assert.False(page.TryAllocate(face, out var exhausted));
        Assert.Null(exhausted);
        Assert.Equal(2, device.Created.Count);
    }

    [Fact]
    public void FailedRangeRequestsReturnTheirSlotIdsAndPreserveLiveRanges()
    {
        var device = new FakeBufferDevice();
        using var page = new Page(8, 2, device);

        for (int attempt = 0; attempt < 4; attempt++)
        {
            Assert.False(page.TryAllocate([], out var empty));
            Assert.Null(empty);
            Assert.False(page.TryAllocate(CreateFaces(9), out var oversized));
            Assert.Null(oversized);
        }

        Assert.True(page.IsEmpty);
        var wholePage = Allocate(page, CreateFaces(8));
        Assert.Equal(0u, wholePage.SlotId);

        // A second slot is available, but the face allocator is full.
        Assert.False(page.TryAllocate(CreateFaces(1), out var full));
        Assert.Null(full);
        Assert.Equal(1, page.AllocationCount);

        page.Release(wholePage);
        Assert.True(page.IsEmpty);
        var first = Allocate(page, CreateFaces(1));
        var second = Allocate(page, CreateFaces(1));
        Assert.Equal(new uint[] { 0, 1 }, new[] { first.SlotId, second.SlotId }.Order().ToArray());
        Assert.Equal(2, page.AllocationCount);
    }

    [Fact]
    public void InvalidWrappersCannotReleaseAnotherAllocationOrDuplicateItsSlot()
    {
        var device = new FakeBufferDevice();
        using var page = new Page(8, 2, device);
        using var otherPage = new Page(8, 2, device);
        var original = Allocate(page, CreateFaces(3));
        var foreign = Allocate(otherPage, CreateFaces(3));
        var fabricated = new GpuAllocation(page, original.SlotId, original.CpuAllocation);
        var wrongSlot = new GpuAllocation(page, 1, original.CpuAllocation);

        Assert.Throws<ArgumentNullException>(() => page.Release(null!));
        Assert.Throws<InvalidOperationException>(() => page.Release(foreign));
        Assert.Throws<InvalidOperationException>(() => page.Release(fabricated));
        Assert.Throws<InvalidOperationException>(() => page.Release(wrongSlot));
        Assert.Equal(1, page.AllocationCount);
        Assert.Equal(1, otherPage.AllocationCount);

        page.Release(original);
        Assert.True(page.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => page.Release(original));
        var replacement = Allocate(page, CreateFaces(3));
        Assert.Equal(original.SlotId, replacement.SlotId);
        Assert.Equal(original.CpuAllocation.First, replacement.CpuAllocation.First);
        Assert.Throws<InvalidOperationException>(() => page.Release(original));

        var neighbor = Allocate(page, CreateFaces(3));
        Assert.NotEqual(replacement.SlotId, neighbor.SlotId);
        Assert.Equal(2, page.AllocationCount);
        page.Release(replacement);
        page.Release(neighbor);
        Assert.True(page.IsEmpty);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(12u)]
    [InlineData(1u << 28)]
    [InlineData(1u << 31)]
    public void InvalidFaceCapacitiesAreRejectedBeforeCreatingBuffers(uint capacity)
    {
        var device = new FakeBufferDevice();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Page(capacity, 1, device));
        Assert.Equal("faceCapacity", exception.ParamName);
        Assert.Empty(device.Created);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(65_537u)]
    [InlineData(uint.MaxValue)]
    public void InvalidSlotCapacitiesAreRejectedBeforeCreatingBuffers(uint capacity)
    {
        var device = new FakeBufferDevice();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Page(8, capacity, device));
        Assert.Equal("slotCapacity", exception.ParamName);
        Assert.Empty(device.Created);
    }

    [Fact]
    public void SlotBufferCreationFailureReleasesFaceBufferAndPropagates()
    {
        var device = new FakeBufferDevice { FailOnCreation = 2 };
        var exception = Assert.Throws<InvalidOperationException>(() => new Page(8, 2, device));
        Assert.Equal("Injected buffer creation failure.", exception.Message);
        var faceBuffer = Assert.Single(device.Created);
        Assert.Contains(faceBuffer.Handle, device.Released);
        Assert.Equal(0, device.LiveCount);
    }

    [Fact]
    public void DisposalReleasesBothBuffersExactlyOnceEvenWithOutstandingAllocations()
    {
        var device = new FakeBufferDevice();
        var page = new Page(8, 2, device);
        Allocate(page, CreateFaces(3));

        page.Dispose();
        page.Dispose();

        Assert.Equal(2, device.Released.Count);
        Assert.Equal(0, device.LiveCount);
        Assert.Equal(0L, device.LiveBytes);
    }

    private static GpuAllocation Allocate(Page page, ReadOnlySpan<VoxelFace> faces)
    {
        Assert.True(page.TryAllocate(faces, out var allocation));
        return Assert.IsType<GpuAllocation>(allocation);
    }

    private static VoxelFace[] CreateFaces(int count) => Enumerable.Range(0, count).Select(index => new VoxelFace
    {
        Uint0 = 0x12340000u + (uint)index,
        Uint1 = 0x89AB0000u + (uint)index,
        Uint2 = 0xFEDC0000u + (uint)index,
        Uint3 = 0x80000000u + (uint)index
    }).ToArray();
}
