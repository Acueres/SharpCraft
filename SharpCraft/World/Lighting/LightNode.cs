using SharpCraft.World.Chunks;

namespace SharpCraft.World.Lighting;

readonly struct LightNode
{
    public Chunk Chunk { get; }
    public sbyte X { get; }
    public sbyte Y { get; }
    public sbyte Z { get; }

    public bool IsEmpty => X == -1;

    public LightNode(Chunk chunk, int x, int y, int z)
    {
        Chunk = chunk;
        X = (sbyte)x;
        Y = (sbyte)y;
        Z = (sbyte)z;
    }

    public LightNode(Chunk chunk)
    {
        Chunk = chunk;
        X = -1;
    }

    public readonly LightValue GetLight()
    {
        return Chunk.GetLight(X, Y, Z);
    }

    public readonly void SetLight(LightValue value)
    {
        Chunk.SetLight(X, Y, Z, value);
    }
}
