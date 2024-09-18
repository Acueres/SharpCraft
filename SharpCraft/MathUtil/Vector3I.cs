using System;

using Microsoft.Xna.Framework;

namespace SharpCraft.MathUtil
{
    public readonly struct Vector3I
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public Vector3I(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3I operator -(Vector3I a, Vector3I b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3I operator +(Vector3I a, Vector3I b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
}
