using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using System;

namespace SharpCraft.World.Light
{
    readonly struct LightNode(IChunk chunk, int x, int y, int z)
    {
        public IChunk Chunk { get; } = chunk;
        public int X { get; } = x;
        public int Y { get; } = y;
        public int Z { get; } = z;

        public readonly Block GetBlock()
        {
            return Chunk[X, Y, Z];
        }

        public readonly LightValue GetLight()
        {
            return Chunk.GetLight(X, Y, Z);
        }

        public readonly void SetLight(LightValue value)
        {
            Chunk.SetLight(X, Y, Z, value);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Chunk.Index.X, Chunk.Index.Y, Chunk.Index.Z, X, Y, Z);
        }
    }
}
