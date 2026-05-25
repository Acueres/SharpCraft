using SharpCraft.Input;
using SharpCraft.SharpMath;
using SharpCraft.Time;

using System.Numerics;

namespace SharpCraft.Rendering;

internal class Camera
{
    public bool UpdateOccurred { get; private set; }
    
    public Matrix4x4 View { get; private set; }
    public Matrix4x4 Projection { get; private set; }
    public Frustum Frustum { get; private set; }

    private Vector3 direction;
    private Vector3 position;
    private Vector3 horizontalDirection;
    private Vector3 target;
    private Vector2 cameraDelta;
    
    private uint viewportWidth;
    private uint viewportHeight;
    
    private readonly float rotationSpeed;

    public Camera(Vector3 position, Vector3 target, uint viewportWidth, uint viewportHeight)
    {
        this.target = target;
        this.position = position;
        
        direction = Vector3.Normalize(target - position);

        horizontalDirection = new Vector3(direction.X, 0f, direction.Z);
        horizontalDirection = Vector3.Normalize(horizontalDirection);

        rotationSpeed = 1.5f;

        View = Matrix4x4.CreateLookAt(position, target, MathUtilities.Vector3Up);

        SetViewport(viewportWidth, viewportHeight);
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

    public void Update(InputHandler input, FrameTime time)
    {
        UpdateOccurred = false;
        
        Vector3 previousPosition = position;
        Vector3 previousDirection = direction;
        
        var ms = input.Mouse;

        cameraDelta = new Vector2(ms.DeltaX, ms.DeltaY);
        cameraDelta = Vector2.Clamp(cameraDelta, new Vector2(-20, -20), new Vector2(20, 20));
        cameraDelta *= rotationSpeed;

        if (Math.Abs(direction.Y) > 0.99f &&
            Math.Sign(cameraDelta.Y) != Math.Sign(direction.Y))
        {
            cameraDelta.Y = 0;
        }

        direction = Vector3.Transform(
            direction,
            Matrix4x4.CreateFromAxisAngle(
                MathUtilities.Vector3Up,
                (-MathUtilities.PiOver4 / 150) * cameraDelta.X
            )
        );

        Vector3 pitchAxis = Vector3.Cross(MathUtilities.Vector3Up, direction);

        if (pitchAxis != Vector3.Zero)
        {
            pitchAxis = Vector3.Normalize(pitchAxis);

            direction = Vector3.Transform(
                direction,
                Matrix4x4.CreateFromAxisAngle(
                    pitchAxis,
                    (MathUtilities.PiOver4 / 100) * cameraDelta.Y
                )
            );
        }

        direction = Vector3.Normalize(direction);

        horizontalDirection = new Vector3(direction.X, 0f, direction.Z);
        if (horizontalDirection != Vector3.Zero)
            horizontalDirection = Vector3.Normalize(horizontalDirection);

        // Movement control
        var ks = input.Keyboard;

        const float movementSpeed = 5f;

        Vector3 right = Vector3.Normalize(
            Vector3.Cross(direction, MathUtilities.Vector3Up)
        );

        if (ks.IsDown(Keys.W))
            position += horizontalDirection * movementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.S))
            position -= horizontalDirection * movementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.A))
            position -= right * movementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.D))
            position += right * movementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.Space))
            position += MathUtilities.Vector3Up * movementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.LeftShift))
            position -= MathUtilities.Vector3Up * movementSpeed * time.DeltaSeconds;
        
        bool moved = position != previousPosition;
        bool directionChanged = direction != previousDirection;

        if (!moved && !directionChanged)
        {
            return;
        }

        target = direction + position;
        View = Matrix4x4.CreateLookAt(position, target, MathUtilities.Vector3Up);
        
        Frustum = new Frustum(View * Projection);
        
        UpdateOccurred = true;
    }
}
