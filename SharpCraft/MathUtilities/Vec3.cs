using System.Numerics;

namespace SharpCraft.MathUtilities;

public readonly struct Vec3<N>(N x, N y, N z) where N : INumber<N>
{
    public N X { get; } = x;
    public N Y { get; } = y;
    public N Z { get; } = z;

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
