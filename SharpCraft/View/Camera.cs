using SharpCraft.SharpMath;

using System.Numerics;

namespace SharpCraft.View;

internal class Camera
{
    public bool UpdateOccurred { get; private set; }
    
    public Matrix4x4 View { get; private set; }
    public Matrix4x4 Projection { get; private set; }
    public Frustum Frustum { get; private set; }
    
    public Vec3<int> Index  { get; private set; }
    public Vector3 LocalPosition  { get; private set; }
    public Vector3 Direction  { get; private set; }
    
    private uint viewportWidth;
    private uint viewportHeight;

    public Camera(in Viewpoint viewpoint, uint viewportWidth, uint viewportHeight)
    {
        View = Matrix4x4.CreateLookAt(
            Vector3.Zero,
            viewpoint.Direction,
            viewpoint.Up
        );
        
        SetViewport(viewportWidth, viewportHeight);
        SetViewpoint(viewpoint);
    }
    
    public void SetViewpoint(in Viewpoint viewpoint)
    {
        if (Index == viewpoint.Index && Direction == viewpoint.Direction && LocalPosition == viewpoint.LocalPosition)
        {
            UpdateOccurred = false;
            return;
        }

        Index = viewpoint.Index;
        LocalPosition = viewpoint.LocalPosition;
        Direction = Vector3.Normalize(viewpoint.Direction);
        
        View = Matrix4x4.CreateLookAt(
            Vector3.Zero,
            Direction,
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
