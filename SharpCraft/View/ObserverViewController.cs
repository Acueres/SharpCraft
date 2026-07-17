using SharpCraft.Input;
using SharpCraft.Time;
using SharpCraft.SharpMath;

using System.Numerics;

namespace SharpCraft.View;

internal class ObserverViewController(in Viewpoint viewpoint) : ICameraController
{
    private Vector3 position = viewpoint.Position;
    private Vector3 direction = viewpoint.Direction;
    private Vector3 horizontalDirection;

    private Vec3<int> index;

    private const float MovementSpeed = 5f;
    private const float RotationSpeed = 1.5f;

    public bool Update(InputHandler input, FrameTime time)
    {
        Vector3 previousPosition = position;
        Vector3 previousDirection = direction;

        UpdateLook(input);
        UpdateMovement(input, time);

        return position != previousPosition || direction != previousDirection;
    }
    
    public Vector3 GetPosition() => position;

    public Viewpoint GetViewpoint()
    {
        return new Viewpoint(
            position,
            direction,
            MathUtilities.Vector3Up
        );
    }
    
    public Vec3<int> GetIndex() => index;

    public void SetIndex(Vec3<int> idx)
    {
        index = idx;
    }

    private void UpdateLook(InputHandler input)
    {
        var ms = input.Mouse;

        var cameraDelta = new Vector2(ms.DeltaX, ms.DeltaY);
        cameraDelta = Vector2.Clamp(cameraDelta, new Vector2(-20, -20), new Vector2(20, 20));
        cameraDelta *= RotationSpeed;

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

        horizontalDirection = direction with { Y = 0f };
        if (horizontalDirection != Vector3.Zero)
            horizontalDirection = Vector3.Normalize(horizontalDirection);
    }

    private void UpdateMovement(InputHandler input, FrameTime time)
    {
        var ks = input.Keyboard;
        
        Vector3 right = Vector3.Cross(direction, MathUtilities.Vector3Up);

        if (right != Vector3.Zero)
        {
            right = Vector3.Normalize(right);
        }

        if (ks.IsDown(Keys.W))
            position += horizontalDirection * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.S))
            position -= horizontalDirection * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.A))
            position -= right * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.D))
            position += right * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.Space))
            position += MathUtilities.Vector3Up * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.LeftShift))
            position -= MathUtilities.Vector3Up * MovementSpeed * time.DeltaSeconds;
    }
}