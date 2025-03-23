namespace SharpCraft.MathUtilities;

public readonly struct Vector3I(int x, int y, int z)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;

    public static Vector3I operator -(Vector3I a, Vector3I b)
    => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3I operator +(Vector3I a, Vector3I b)
    => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static bool operator ==(Vector3I a, Vector3I b)
    {
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }

    public static bool operator !=(Vector3I a, Vector3I b)
    {
        return a.X != b.X || a.Y != b.Y || a.Z != b.Z;
    }

    public override bool Equals(object obj)
    {
        Vector3I other = (Vector3I)obj;
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
