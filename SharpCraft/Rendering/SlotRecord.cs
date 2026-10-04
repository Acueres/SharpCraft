using System.Runtime.InteropServices;
using SharpCraft.SharpMath;

namespace SharpCraft.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct SlotRecord
{
    // Bytes 0–15
    public int ChunkX;
    public int ChunkY;
    public int ChunkZ;
    public uint Flags;

    // Bytes 16–31
    public uint Aux0;
    public uint Aux1;
    public uint Aux2;
    public uint Aux3;

    // Bytes 32–47
    public float BoundsX;
    public float BoundsY;
    public float BoundsZ;
    public float BoundsRadius;

    public SlotRecord(
        Vec3<int> chunkIndex)
    {
       ChunkX = chunkIndex.X;
       ChunkY  = chunkIndex.Y;
       ChunkZ = chunkIndex.Z;
    }
}