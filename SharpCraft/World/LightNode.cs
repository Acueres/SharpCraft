namespace SharpCraft.World
{
    struct LightNode
    {
        public Chunk Chunk;
        public int X;
        public int Y;
        public int Z;


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

        public readonly byte GetLight(bool channel)
        {
            return Chunk.GetLight(Y, X, Z, channel);
        }

        public readonly void SetLight(byte value, bool channel)
        {
            Chunk.SetLight(Y, X, Z, value, channel);
        }
    }
}
