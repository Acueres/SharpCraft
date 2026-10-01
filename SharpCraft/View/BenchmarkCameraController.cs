using SharpCraft.Diagnostics;
using SharpCraft.Input;
using SharpCraft.Time;
using SharpCraft.SharpMath;

using System.Numerics;

namespace SharpCraft.View;

internal sealed class BenchmarkCameraController(BenchmarkOptions options, BenchmarkRun run) : ICameraController
{
    private Vec3<int> index;
    private Viewpoint viewpoint = Pose(options.Path, 0);

    public static Viewpoint Pose(BenchmarkPath path, double seconds)
    {
        Vector3 position = path == BenchmarkPath.Travel
            ? new((float)(seconds * 8), 40, 48)
            : new(0, 40, 48);
        Vector3 target = path == BenchmarkPath.Rotate
            ? position + new Vector3((float)Math.Sin(seconds * .25), -.35f, -(float)Math.Cos(seconds * .25)) * 48
            : position + new Vector3(0, -24, -48);
        return Viewpoint.LookAt(position, target, Vector3.UnitY);
    }

    public bool Update(InputHandler input, FrameTime time)
    {
        Viewpoint next = Pose(options.Path, run.RouteSeconds);
        bool changed = next != viewpoint;
        viewpoint = next;
        return changed;
    }

    public Vector3 GetPosition() => viewpoint.Position;
    public Viewpoint GetViewpoint() => viewpoint;
    public Vec3<int> GetIndex() => index;
    public void SetIndex(Vec3<int> value) => index = value;
}
