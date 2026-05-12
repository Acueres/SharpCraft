using System.Numerics;
using System.Runtime.InteropServices;

namespace SharpCraft.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct Vertex
{
    public Vector3 Position;

    public Vertex(float x, float y, float z)
    {
        Position = new Vector3(x, y, z);
    }
}