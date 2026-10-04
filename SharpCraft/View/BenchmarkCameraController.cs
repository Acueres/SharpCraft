using SharpCraft.Diagnostics;
using SharpCraft.Input;
using SharpCraft.Time;
using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;

using System.Numerics;

namespace SharpCraft.View;

internal sealed class BenchmarkCameraController(BenchmarkOptions options, BenchmarkRun run) : ICameraController
{
    private Viewpoint viewpoint = Pose(options.Path, 0);

    public static Viewpoint Pose(BenchmarkPath path, double seconds)
    {
        double travelX = path == BenchmarkPath.Travel ? seconds * 8 : 0;
        int chunkX = checked((int)Math.Floor(travelX / Chunk.Size));
        float localX = (float)(travelX - (double)chunkX * Chunk.Size);
        if (localX >= Chunk.Size)
        {
            localX = 0;
            chunkX = checked(chunkX + 1);
        }

        Vector3 direction = path == BenchmarkPath.Rotate
            ? new((float)Math.Sin(seconds * .25), -.35f, -(float)Math.Cos(seconds * .25))
            : new(0, -24, -48);

        return new Viewpoint(
            new Vec3<int>(chunkX, 2, 3),
            new Vector3(localX, 8, 0),
            Vector3.Normalize(direction),
            Vector3.UnitY
        );
    }

    public bool Update(InputHandler input, FrameTime time)
    {
        Viewpoint next = Pose(options.Path, run.RouteSeconds);
        bool changed = next != viewpoint;
        viewpoint = next;
        return changed;
    }

    public Vector3 GetLocalPosition() => viewpoint.LocalPosition;
    public Viewpoint GetViewpoint() => viewpoint;
    public Vec3<int> GetIndex() => viewpoint.Index;
}
