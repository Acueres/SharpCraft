using SharpCraft.Input;
using SharpCraft.Time;
using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;

using System.Numerics;

namespace SharpCraft.View;

internal class ObserverViewController(in Viewpoint viewpoint) : ICameraController
{
    private Vec3<int> index = viewpoint.Index;
    private Vector3 localPosition = viewpoint.LocalPosition;
    private Vector3 direction = viewpoint.Direction;
    private Vector3 horizontalDirection;
    
    private const float MovementSpeed = 5f;
    private const float RotationSpeed = 1.5f;

    public bool Update(InputHandler input, FrameTime time)
    {
        Vec3<int> previousIndex = index;
        Vector3 previousLocalPosition = localPosition;
        Vector3 previousDirection = direction;

        UpdateLook(input);
        UpdateMovement(input, time);
        NormalizeLocalPosition();

        return index != previousIndex
               || localPosition != previousLocalPosition
               || direction != previousDirection;
    }
    
    public Vector3 GetLocalPosition() => localPosition;

    public Viewpoint GetViewpoint()
    {
        return new Viewpoint(
            index,
            localPosition,
            direction,
            MathUtilities.Vector3Up
        );
    }
    
    public Vec3<int> GetIndex() => index;
    
    private void NormalizeLocalPosition()
    {
        var x = NormalizeAxis(index.X, localPosition.X);
        var y = NormalizeAxis(index.Y, localPosition.Y);
        var z = NormalizeAxis(index.Z, localPosition.Z);

        index = new Vec3<int>(x.Index, y.Index, z.Index);
        localPosition = new Vector3(x.Local, y.Local, z.Local);
    }

    private static (int Index, float Local) NormalizeAxis(
        int chunkIndex,
        float localPosition)
    {
        int carry = checked(
            (int)Math.Floor((double)localPosition / Chunk.Size)
        );

        float local = (float)(
            localPosition - (double)carry * Chunk.Size
        );

        if (local >= Chunk.Size)
        {
            local = 0f;
            carry = checked(carry + 1);
        }

        return (checked(chunkIndex + carry), local);
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
            localPosition += horizontalDirection * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.S))
            localPosition -= horizontalDirection * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.A))
            localPosition -= right * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.D))
            localPosition += right * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.Space))
            localPosition += MathUtilities.Vector3Up * MovementSpeed * time.DeltaSeconds;

        if (ks.IsDown(Keys.LeftShift))
            localPosition -= MathUtilities.Vector3Up * MovementSpeed * time.DeltaSeconds;
    }
}