using System;
using System.Numerics;

namespace SharpCraft.MathUtilities;

public readonly struct Vec3<N>(N x, N y, N z) : IComparable<Vec3<N>>
    where N : INumber<N>, IComparable
{
    public N X { get; } = x;
    public N Y { get; } = y;
    public N Z { get; } = z;

    public static Vec3<N> Zero => new(default, default, default);

    public static Vec3<N> One => new(N.CreateChecked(1), N.CreateChecked(1), N.CreateChecked(1));

    public double Length => Math.Sqrt(double.CreateChecked(X * X + Y * Y + Z * Z));

    public int ManhattanDistance => int.CreateChecked(N.Abs(X) + N.Abs(Y) + N.Abs(Z));

    public static Vec3<N> operator -(Vec3<N> a, Vec3<N> b)
    => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3<N> operator +(Vec3<N> a, Vec3<N> b)
    => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static bool operator ==(Vec3<N> a, Vec3<N> b)
    {
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }

    public static bool operator !=(Vec3<N> a, Vec3<N> b)
    {
        return a.X != b.X || a.Y != b.Y || a.Z != b.Z;
    }

    public Vec3<O> Into<O>() where O : INumber<O>
    {
        return new Vec3<O>(O.CreateChecked(X), O.CreateChecked(Y), O.CreateChecked(Z));
    }
    public int CompareTo(Vec3<N> other)
    {
        int cmp = X.CompareTo(other.X);
        if (cmp != 0)
            return cmp;
        cmp = Y.CompareTo(other.Y);
        if (cmp != 0)
            return cmp;
        return Z.CompareTo(other.Z);
    }


    public override bool Equals(object obj)
    {
        Vec3<N> other = (Vec3<N>)obj;
        return other == this;
    }

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}, Z: {Z}";
    }

    public override int GetHashCode()
    {
        const int prime = 397;
        unchecked
        {
            int hashCode = X.GetHashCode();
            hashCode = hashCode * prime ^ Y.GetHashCode();
            hashCode = hashCode * prime ^ Z.GetHashCode();
            return hashCode;
        }
    }
}
