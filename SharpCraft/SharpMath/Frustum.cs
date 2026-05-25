using System.Numerics;

namespace SharpCraft.SharpMath;

internal readonly struct Frustum
{
    private readonly Plane left;
    private readonly Plane right;
    private readonly Plane bottom;
    private readonly Plane top;
    private readonly Plane near;
    private readonly Plane far;
    
    public Frustum(in Matrix4x4 matrix)
    {
        left = new Plane(
            matrix.M11 + matrix.M14,
            matrix.M21 + matrix.M24,
            matrix.M31 + matrix.M34,
            matrix.M41 + matrix.M44).Normalize();

        right = new Plane(
            matrix.M14 - matrix.M11,
            matrix.M24 - matrix.M21,
            matrix.M34 - matrix.M31,
            matrix.M44 - matrix.M41).Normalize();

        bottom = new Plane(
            matrix.M12 + matrix.M14,
            matrix.M22 + matrix.M24,
            matrix.M32 + matrix.M34,
            matrix.M42 + matrix.M44).Normalize();

        top = new Plane(
            matrix.M14 - matrix.M12,
            matrix.M24 - matrix.M22,
            matrix.M34 - matrix.M32,
            matrix.M44 - matrix.M42).Normalize();

        near = new Plane(
            matrix.M13,
            matrix.M23,
            matrix.M33,
            matrix.M43).Normalize();

        far = new Plane(
            matrix.M14 - matrix.M13,
            matrix.M24 - matrix.M23,
            matrix.M34 - matrix.M33,
            matrix.M44 - matrix.M43).Normalize();
    }

    public bool Intersects(in CubeBound cube)
    {
        return
            IntersectsPlane(left, cube) &&
            IntersectsPlane(right, cube) &&
            IntersectsPlane(top, cube) &&
            IntersectsPlane(bottom, cube) &&
            IntersectsPlane(near, cube) &&
            IntersectsPlane(far, cube);
    }

    private static bool IntersectsPlane(in Plane plane, in CubeBound cube)
    {
        float distance = plane.DistanceToPoint(cube.Center);

        float radius = cube.HalfSize *
            (
                MathF.Abs(plane.Normal.X) +
                MathF.Abs(plane.Normal.Y) +
                MathF.Abs(plane.Normal.Z)
            );

        return distance + radius >= 0;
    }
}