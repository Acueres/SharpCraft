using System;

namespace SharpCraft.Utility
{
    public readonly struct Vector3I(int x, int y, int z)
    {
        public int X => x;
        public int Y => y;
        public int Z => z;

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
