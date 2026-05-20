using System.Numerics;
using System.Runtime.InteropServices;

namespace SharpCraft.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct BlockFace
{
    public Vector3 Center;
    public uint Direction;
    public uint TextureLayer;

    public BlockFace(
        float x,
        float y,
        float z,
        uint direction,
        uint textureLayer)
    {
        Center = new Vector3(x, y, z);
        Direction = direction;
        TextureLayer = textureLayer;
    }
}