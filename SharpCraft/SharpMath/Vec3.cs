using System.Numerics;

namespace SharpCraft.SharpMath;

internal readonly record struct Vec3<TN>(TN X, TN Y, TN Z) : IComparable<Vec3<TN>>
    where TN : INumber<TN>, IComparable<TN>
{
    public static Vec3<TN> Zero => new(TN.Zero, TN.Zero, TN.Zero);
    public static Vec3<TN> One  => new(TN.One, TN.One, TN.One);

    public TN LengthSquared => X * X + Y * Y + Z * Z;
    public double Length => Math.Sqrt(double.CreateChecked(LengthSquared));

    public TN ManhattanDistance => Abs(X) + Abs(Y) + Abs(Z);

    public static Vec3<TN> operator -(Vec3<TN> a, Vec3<TN> b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3<TN> operator +(Vec3<TN> a, Vec3<TN> b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3<TN> operator -(Vec3<TN> a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3<TN> operator *(Vec3<TN> a, TN s) => new(a.X * s, a.Y * s, a.Z * s);

    public TN Dot(Vec3<TN> o) => X * o.X + Y * o.Y + Z * o.Z;

    public Vec3<TO> Into<TO>() where TO : INumber<TO>
        => new(TO.CreateChecked(X), TO.CreateChecked(Y), TO.CreateChecked(Z));

    public int CompareTo(Vec3<TN> other)
    {
        int cmp = X.CompareTo(other.X);
        if (cmp != 0) return cmp;
        cmp = Y.CompareTo(other.Y);
        return cmp != 0 ? cmp : Z.CompareTo(other.Z);
    }

    private static TN Abs(TN v) => TN.IsNegative(v) ? -v : v;

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}, Z: {Z}";
    }
}