using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

namespace SharpCraft.Models
{
    public struct BlockIndex
    {
        public int X, Y, Z;

        public BlockIndex(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static BlockIndex operator +(BlockIndex val1, BlockIndex val2)
        {
            return new(val1.X + val2.X, val1.Y + val2.Y, val1.Z + val2.Z);
        }

        public static BlockIndex operator -(BlockIndex val1, BlockIndex val2)
        {
            return new(val1.X - val2.X, val1.Y - val2.Y, val1.Z - val2.Z);
        }

        public override string ToString()
        {
            return new Vector3(X, Y, Z).ToString();
        }
    }
}
