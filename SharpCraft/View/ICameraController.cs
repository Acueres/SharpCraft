using SharpCraft.Input;
using SharpCraft.Time;
using SharpCraft.SharpMath;

using System.Numerics;

namespace SharpCraft.View;

internal interface ICameraController
{
    bool Update(InputHandler input, FrameTime time);
    Vector3 GetPosition();
    Viewpoint GetViewpoint();
    Vec3<int> GetIndex();
    void SetIndex(Vec3<int> index);
}