using System.Numerics;

namespace SharpCraft.SharpMath;

internal readonly record struct Vec2<TN>(TN X, TN Z) : IComparable<Vec2<TN>>
    where TN : INumber<TN>, IComparable<TN>
{
    public static Vec2<TN> Zero => new(TN.Zero, TN.Zero);
    public static Vec2<TN> One  => new(TN.One, TN.One);

    public TN LengthSquared => X * X + Z * Z;
    public double Length => Math.Sqrt(double.CreateChecked(LengthSquared));

    public TN ManhattanDistance => Abs(X) + Abs(Z);

    public static Vec2<TN> operator -(Vec2<TN> a, Vec2<TN> b) => new(a.X - b.X, a.Z - b.Z);
    public static Vec2<TN> operator +(Vec2<TN> a, Vec2<TN> b) => new(a.X + b.X, a.Z + b.Z);
    public static Vec2<TN> operator -(Vec2<TN> a) => new(-a.X, -a.Z);
    public static Vec2<TN> operator *(Vec2<TN> a, TN s) => new(a.X * s, a.Z * s);

    public TN Dot(Vec2<TN> o) => X * o.X + Z * o.Z;

    public Vec2<TO> Into<TO>() where TO : INumber<TO>
        => new(TO.CreateChecked(X), TO.CreateChecked(Z));

    public int CompareTo(Vec2<TN> other)
    {
        int cmp = X.CompareTo(other.X);
        if (cmp != 0) return cmp;
        return Z.CompareTo(other.Z);
    }

    private static TN Abs(TN v) => TN.IsNegative(v) ? -v : v;

    public override string ToString()
    {
        return $"X: {X}, Z: {Z}";
    }
}