using System.Runtime.InteropServices;
using SharpCraft.Rendering;

namespace Tests;

public unsafe class GpuLayoutTests
{
    [Fact]
    public void FaceRecordRemainsSixteenBytesWithFourScalarWords()
    {
        Assert.Equal(16, sizeof(VoxelFace));
        Assert.Equal(16, Marshal.SizeOf<VoxelFace>());
        AssertOffset<VoxelFace>(nameof(VoxelFace.Uint0), 0);
        AssertOffset<VoxelFace>(nameof(VoxelFace.Uint1), 4);
        AssertOffset<VoxelFace>(nameof(VoxelFace.Uint2), 8);
        AssertOffset<VoxelFace>(nameof(VoxelFace.Uint3), 12);
    }

    [Fact]
    public void SlotRecordRemainsFortyEightBytesWithShaderFieldOffsets()
    {
        Assert.Equal(48, sizeof(SlotRecord));
        Assert.Equal(48, Marshal.SizeOf<SlotRecord>());
        AssertOffset<SlotRecord>(nameof(SlotRecord.ChunkX), 0);
        AssertOffset<SlotRecord>(nameof(SlotRecord.ChunkY), 4);
        AssertOffset<SlotRecord>(nameof(SlotRecord.ChunkZ), 8);
        AssertOffset<SlotRecord>(nameof(SlotRecord.Flags), 12);
        AssertOffset<SlotRecord>(nameof(SlotRecord.Aux0), 16);
        AssertOffset<SlotRecord>(nameof(SlotRecord.Aux1), 20);
        AssertOffset<SlotRecord>(nameof(SlotRecord.Aux2), 24);
        AssertOffset<SlotRecord>(nameof(SlotRecord.Aux3), 28);
        AssertOffset<SlotRecord>(nameof(SlotRecord.BoundsX), 32);
        AssertOffset<SlotRecord>(nameof(SlotRecord.BoundsY), 36);
        AssertOffset<SlotRecord>(nameof(SlotRecord.BoundsZ), 40);
        AssertOffset<SlotRecord>(nameof(SlotRecord.BoundsRadius), 44);
    }

    [Fact]
    public void CameraUniformRemainsNinetySixBytesWithShaderFieldOffsets()
    {
        Assert.Equal(96, sizeof(CameraUniformGpu));
        Assert.Equal(96, Marshal.SizeOf<CameraUniformGpu>());
        AssertOffset<CameraUniformGpu>(nameof(CameraUniformGpu.Mvp), 0);
        AssertOffset<CameraUniformGpu>(nameof(CameraUniformGpu.ChunkX), 64);
        AssertOffset<CameraUniformGpu>(nameof(CameraUniformGpu.ChunkY), 68);
        AssertOffset<CameraUniformGpu>(nameof(CameraUniformGpu.ChunkZ), 72);
        AssertOffset<CameraUniformGpu>(nameof(CameraUniformGpu.Padding0), 76);
        AssertOffset<CameraUniformGpu>(nameof(CameraUniformGpu.LocalPosition), 80);
        AssertOffset<CameraUniformGpu>(nameof(CameraUniformGpu.Padding1), 92);
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)7)]
    [InlineData(ushort.MaxValue)]
    public void SlotStampingReplacesPreviousIdWithoutChangingOtherFaceData(ushort slotId)
    {
        var face = new VoxelFace
        {
            Uint0 = 0x12345678u,
            Uint1 = 0x89ABCDEFu,
            Uint2 = 0xABCD4321u,
            Uint3 = 0x76543210u
        };

        face.SetSlotId(slotId);

        Assert.Equal(0x12345678u, face.Uint0);
        Assert.Equal(0x89ABCDEFu, face.Uint1);
        Assert.Equal(((uint)slotId << 16) | 0x4321u, face.Uint2);
        Assert.Equal(0x76543210u, face.Uint3);
    }

    private static void AssertOffset<T>(string field, int expected) where T : struct =>
        Assert.Equal(expected, Marshal.OffsetOf<T>(field).ToInt32());
}
