namespace SharpCraft.MathUtilities;

public readonly struct Vector2I(int x, int z)
{
    public int X { get; } = x;
    public int Z { get; } = z;

    public static Vector2I operator -(Vector2I a, Vector2I b)
    => new(a.X - b.X, a.Z - b.Z);

    public static Vector2I operator +(Vector2I a, Vector2I b)
    => new(a.X + b.X, a.Z + b.Z);

    public static bool operator ==(Vector2I a, Vector2I b)
    {
        return a.X == b.X && a.Z == b.Z;
    }

    public static bool operator !=(Vector2I a, Vector2I b)
    {
        return a.X != b.X || a.Z != b.Z;
    }

    public override bool Equals(object obj)
    {
        Vector2I other = (Vector2I)obj;
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
