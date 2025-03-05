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

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}, Z: {Z}";
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = X.GetHashCode();
            hashCode = hashCode * 397 ^ Y.GetHashCode();
            hashCode = hashCode * 397 ^ Z.GetHashCode();
            return hashCode;
        }
    }
}
