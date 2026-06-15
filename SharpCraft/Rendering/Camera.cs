using SharpCraft.SharpMath;
using SharpCraft.Rendering.View;

using System.Numerics;

namespace SharpCraft.Rendering;

internal class Camera
{
    public bool UpdateOccurred { get; private set; }
    
    public Matrix4x4 View { get; private set; }
    public Matrix4x4 Projection { get; private set; }
    public Frustum Frustum { get; private set; }
    
    public Vector3 Position  { get; private set; }
    public Vector3 Direction  { get; private set; }
    
    private uint viewportWidth;
    private uint viewportHeight;

    public Camera(in Viewpoint viewpoint, uint viewportWidth, uint viewportHeight)
    {
        View = Matrix4x4.CreateLookAt(
            viewpoint.Position,
            viewpoint.Position + viewpoint.Direction,
            viewpoint.Up
        );
        
        SetViewport(viewportWidth, viewportHeight);
        SetViewpoint(viewpoint);
    }
    
    public void SetViewpoint(in Viewpoint viewpoint)
    {
        if (Position == viewpoint.Position && Direction == viewpoint.Direction)
        {
            UpdateOccurred = false;
            return;
        }

        Position = viewpoint.Position;
        Direction = Vector3.Normalize(viewpoint.Direction);
        
        View = Matrix4x4.CreateLookAt(
            Position,
            Position + Direction,
            viewpoint.Up
        );

        Frustum = new Frustum(View * Projection);
        UpdateOccurred = true;
    }


    public void SetViewport(uint width, uint height)
    {
        if (width == 0 || height == 0)
            return;
        
        if (width == viewportWidth && height == viewportHeight)
            return;
        
        viewportWidth = width;
        viewportHeight = height;

        Projection = Matrix4x4.CreatePerspectiveFieldOfView(
            float.DegreesToRadians(70),
            (float)width / height,
            0.1f,
            200f
        );
        
        Frustum = new Frustum(View * Projection);
        UpdateOccurred = true;
    }
}
