using System.Numerics;
using System.Runtime.InteropServices;

namespace SharpCraft.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct Vertex
{
    public Vector3 Position;
    public Vector2 TexCoord;

    public Vertex(float x, float y, float z, float u, float v)
    {
        Position = new Vector3(x, y, z);
        TexCoord = new Vector2(u, v);
    }
}