using System.Numerics;
using System.Runtime.InteropServices;

namespace SharpCraft.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct CameraUniformGpu
{
    public Matrix4x4 Mvp;          // 0..63

    public int ChunkX;            // 64..67
    public int ChunkY;            // 68..71
    public int ChunkZ;            // 72..75
    public uint Padding0;         // 76..79

    public Vector3 LocalPosition; // 80..91
    public uint Padding1;         // 92..95
}