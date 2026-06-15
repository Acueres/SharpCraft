using SharpCraft.Input;
using SharpCraft.Time;

namespace SharpCraft.Rendering.View;

internal interface IViewController
{
    bool Update(InputHandler input, FrameTime time);
    Viewpoint GetViewpoint();
}