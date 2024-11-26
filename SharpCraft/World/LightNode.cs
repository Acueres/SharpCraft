using System;

namespace SharpCraft.World
{
    readonly struct LightNode
    {
        public Chunk Chunk {  get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }


        public LightNode(Chunk chunk, int x, int y, int z)
        {
            Chunk = chunk;
            X = x;
            Y = y;
            Z = z;
        }

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
