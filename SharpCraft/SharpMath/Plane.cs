using System.Numerics;

namespace SharpCraft.SharpMath;

internal readonly struct Plane
{
    public readonly Vector3 Normal;
    
    private readonly float distance;

    public Plane(float x, float y, float z, float distance)
        : this(new Vector3(x, y, z), distance) { }

    private Plane(in Vector3 normal, float distance)
    {
        Normal = normal;
        this.distance = distance;
    }

    public Plane Normalize()
    {
        float length = Normal.Length();

        if (length == 0)
        {
            throw new InvalidOperationException("Cannot normalize a plane with zero-length normal");
        }

        float invLength = 1.0f / length;

        return new Plane(
            Normal * invLength,
            distance * invLength
        );
    }

    public float DistanceToPoint(in Vector3 point)
    {
        return Vector3.Dot(Normal, point) + distance;
    }
}