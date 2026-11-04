using SharpCraft.Graphics.Resources;

namespace Tests;

public class BufferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroCapacityIsRejectedBeforeNativeCreation(bool slots)
    {
        var device = new FakeBufferDevice();
        Assert.Throws<ArgumentException>(() => Create(slots, 0, device));
        Assert.Empty(device.Created);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ByteSizeOverflowIsRejectedBeforeNativeCreation(bool slots)
    {
        var device = new FakeBufferDevice();
        Assert.Throws<OverflowException>(() => Create(slots, 1u << 28, device));
        Assert.Empty(device.Created);
    }

    private static IDisposable Create(bool slots, uint capacity, FakeBufferDevice device) => slots
        ? new SlotRecordBuffer(capacity, device)
        : new BlockFaceBuffer(capacity, device);
}
