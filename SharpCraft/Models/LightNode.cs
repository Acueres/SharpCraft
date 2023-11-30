using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpCraft.World;

namespace SharpCraft.Models
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

        public ushort? GetTexture()
        {
            return Chunk[X, Y, Z];
        }

        public byte GetLight(bool channel)
        {
            return Chunk.GetLight(Y, X, Z, channel);
        }

        public void SetLight(byte value, bool channel)
        {
            Chunk.SetLight(Y, X, Z, value, channel);
        }
    }
}
