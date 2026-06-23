using SharpCraft.World.Chunks;

namespace SharpCraft.World.Lighting;

internal readonly struct LightNode(in LightValue value, int x, int y, int z)
{
    public LightValue Value { get; } = value;
    public sbyte X { get; } = (sbyte)x;
    public sbyte Y { get; } = (sbyte)y;
    public sbyte Z { get; } = (sbyte)z;
}
