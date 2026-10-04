using SharpCraft.SharpMath;

using System.Runtime.InteropServices;

namespace SharpCraft.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct VoxelFace
{
    public Vec3<int> ChunkIndex;
    public uint PackedBlockIndex;
    public uint Direction;
    public uint TextureLayer;
    public uint PackedLight;

    public VoxelFace(
        Vec3<int> chunkIndex,
        Vec3<byte> blockIndex,
        uint direction,
        uint textureLayer,
        uint packedLight)
    {
        ChunkIndex = chunkIndex;
        PackedBlockIndex = ((uint)blockIndex.X << 16) | ((uint)blockIndex.Y << 8) | blockIndex.Z;
        Direction = direction;
        TextureLayer = textureLayer;
        PackedLight = packedLight;
    }
}