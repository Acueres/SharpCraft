using System.Numerics;
using System.Runtime.InteropServices;

namespace SharpCraft.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct SpriteVertex
{
    public readonly Vector2 Position;
    public readonly Vector2 TexCoord;
    public readonly Vector4 Color;

    public SpriteVertex(
        Vector2 position,
        Vector2 texCoord,
        Vector4 color)
    {
        Position = position;
        TexCoord = texCoord;
        Color = color;
    }
}