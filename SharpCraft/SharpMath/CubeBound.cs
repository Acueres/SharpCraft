using System.Numerics;

namespace SharpCraft.SharpMath;

internal readonly struct CubeBound
{
    public Vector3 Center { get; }
    public float HalfSize { get; }

    public CubeBound(Vector3 center, float halfSize)
    {
        if (halfSize < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(halfSize),
                "Half size cannot be negative."
            );
        }

        Center = center;
        HalfSize = halfSize;
    }

    public Vector3 Min => Center - new Vector3(HalfSize);
    public Vector3 Max => Center + new Vector3(HalfSize);
}