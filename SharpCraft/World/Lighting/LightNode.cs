using SharpCraft.World.Chunks;

namespace SharpCraft.World.Lighting;

readonly struct LightNode(Chunk chunk, int x, int y, int z)
{
    public Chunk Chunk { get; } = chunk;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;

    public readonly LightValue GetLight()
    {
        return Chunk.GetLight(X, Y, Z);
    }

    public readonly void SetLight(LightValue value)
    {
        Chunk.SetLight(X, Y, Z, value);
    }
}
