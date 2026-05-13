using System.Numerics;
using SharpCraft.Input;
using SharpCraft.SharpMath;

namespace SharpCraft.Rendering;

internal class Camera
{
    public Matrix4x4 View { get; set; }
    public Matrix4x4 Projection { get; set; }

    public Vector3 Direction { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 HorizontalDirection { get; set; }

    //public BoundingFrustum Frustum { get; set; }

    Vector3 target;

    readonly float rotationSpeed;


    public Camera(Vector3 position, Vector3 target, uint viewportWidth, uint viewportHeight)
    {
        this.target = target;

        Position = position;
        Direction = Vector3.Normalize(target - position);

        HorizontalDirection = new Vector3(Direction.X, 0f, Direction.Z);
        HorizontalDirection = Vector3.Normalize(HorizontalDirection);

        rotationSpeed = 2.5f;

        View = Matrix4x4.CreateLookAt(position, target, MathUtilities.Vector3Up);

        SetViewport(viewportWidth, viewportHeight);

        //Frustum = new BoundingFrustum(View * Projection);
    }

    public void SetViewport(uint width, uint height)
    {
        if (width == 0 || height == 0)
            return;

        Projection = Matrix4x4.CreatePerspectiveFieldOfView(
            float.DegreesToRadians(70),
            (float)width / height,
            0.1f,
            200f
        );
    }

    public void Update(InputHandler input)
    {
        var ms = input.Mouse;

        Vector2 cameraDelta = new(ms.DeltaX, ms.DeltaY);
        cameraDelta = Vector2.Clamp(cameraDelta, new(-20, -20), new(20, 20));
        cameraDelta *= rotationSpeed;

        if (Math.Abs(Direction.Y) > 0.99f &&
            Math.Sign(cameraDelta.Y) != Math.Sign(Direction.Y))
        {
            cameraDelta.Y = 0;
        }

        Direction = Vector3.Transform(
            Direction,
            Matrix4x4.CreateFromAxisAngle(
                MathUtilities.Vector3Up,
                (-MathUtilities.PiOver4 / 150) * cameraDelta.X
            )
        );

        Vector3 pitchAxis = Vector3.Cross(MathUtilities.Vector3Up, Direction);

        if (pitchAxis != Vector3.Zero)
        {
            pitchAxis = Vector3.Normalize(pitchAxis);

            Direction = Vector3.Transform(
                Direction,
                Matrix4x4.CreateFromAxisAngle(
                    pitchAxis,
                    (MathUtilities.PiOver4 / 100) * cameraDelta.Y
                )
            );
        }

        Direction = Vector3.Normalize(Direction);

        HorizontalDirection = new Vector3(Direction.X, 0f, Direction.Z);
        if (HorizontalDirection != Vector3.Zero)
            HorizontalDirection = Vector3.Normalize(HorizontalDirection);

        // Movement control
        var ks = input.Keyboard;

        const float movementSpeed = 0.08f;

        Vector3 right = Vector3.Normalize(
            Vector3.Cross(Direction, MathUtilities.Vector3Up)
        );

        if (ks.IsDown(Keys.W))
        {
            Position += HorizontalDirection * movementSpeed;
        }

        if (ks.IsDown(Keys.S))
        {
            Position -= HorizontalDirection * movementSpeed;
        }

        if (ks.IsDown(Keys.A))
        {
            Position -= right * movementSpeed;
        }

        if (ks.IsDown(Keys.D))
        {
            Position += right * movementSpeed;
        }

        target = Direction + Position;
        View = Matrix4x4.CreateLookAt(Position, target, MathUtilities.Vector3Up);

        //Frustum.Matrix = View * Projection;
    }
}
