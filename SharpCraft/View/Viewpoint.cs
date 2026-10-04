using SharpCraft.SharpMath;

using System.Numerics;
using SharpCraft.World.Chunks;

namespace SharpCraft.View;

internal readonly record struct Viewpoint(
    Vec3<int> Index,
    Vector3 LocalPosition,
    Vector3 Direction,
    Vector3 Up
)
{
    public static Viewpoint LookAt(Vec3<int> index, Vector3 localPosition, Vec3<int> targetIndex,
        Vector3 targetLocalPosition, Vector3 up)
    {
        var indexOffset = targetIndex - index;
        var localOffset = targetLocalPosition - localPosition;

        return new Viewpoint(
            index,
            localPosition,
            Vector3.Normalize(new Vector3(indexOffset.X, indexOffset.Y, indexOffset.Z) * Chunk.Size + localOffset),
            up
        );
    }
};