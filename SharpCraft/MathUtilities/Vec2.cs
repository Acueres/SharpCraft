using System.Numerics;

namespace SharpCraft.MathUtilities;

public readonly struct Vec2<N>(N x, N z) where N: INumber<N>
{
    public N X { get; } = x;
    public N Z { get; } = z;

    public static Vec2<N> operator -(Vec2<N> a, Vec2<N> b)
    => new(a.X - b.X, a.Z - b.Z);

    public static Vec2<N> operator +(Vec2<N> a, Vec2<N> b)
    => new(a.X + b.X, a.Z + b.Z);

    public static bool operator ==(Vec2<N> a, Vec2<N> b)
    {
        return a.X == b.X && a.Z == b.Z;
    }

    public static bool operator !=(Vec2<N> a, Vec2<N> b)
    {
        return a.X != b.X || a.Z != b.Z;
    }

    public override bool Equals(object obj)
    {
        Vec2<N> other = (Vec2<N>)obj;
        return other == this;
    }

    public override string ToString()
    {
        return $"X: {X}, Z: {Z}";
    }

    public override int GetHashCode()
    {
        const int prime = 397;
        unchecked
        {
            int hashCode = X.GetHashCode();
            hashCode = hashCode * prime ^ Z.GetHashCode();
            return hashCode;
        }
    }
}
