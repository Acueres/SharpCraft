using System.Numerics;

namespace SharpCraft.Rendering.View;

internal readonly record struct Viewpoint(
    Vector3 Position,
    Vector3 Direction,
    Vector3 Up
)
{
    public static Viewpoint LookAt(Vector3 position, Vector3 target, Vector3 up)
    {
        return new Viewpoint(
            position,
            Vector3.Normalize(target - position),
            up
        );
    }
};