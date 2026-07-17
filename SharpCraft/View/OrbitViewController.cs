using SharpCraft.Input;
using SharpCraft.SharpMath;
using SharpCraft.Time;

using System.Numerics;

namespace SharpCraft.View;

internal sealed class OrbitViewController : ICameraController
{
    private const float RotationSpeed = MathF.PI / 3f;
    private const float ZoomSensitivity = 0.12f;
    private const float MouseRotationSensitivity = 0.003f;
    private const float PitchLimit = MathF.PI / 2f - 0.01f;

    private Vector3 target;

    private float yaw;
    private float pitch;
    private float distance;

    private readonly float minimumDistance;
    private readonly float maximumDistance;

    private Vec3<int> index;
    private bool updatePending = true;

    public OrbitViewController(
        Vector3 target,
        in Viewpoint initialViewpoint,
        float minimumDistance = 1f,
        float maximumDistance = 500f)
    {
        if (minimumDistance <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(minimumDistance),
                "The minimum orbit distance must be greater than zero"
            );

        if (maximumDistance < minimumDistance)
            throw new ArgumentOutOfRangeException(
                nameof(maximumDistance),
                "The maximum orbit distance must not be smaller than the minimum distance"
            );

        this.target = target;
        this.minimumDistance = minimumDistance;
        this.maximumDistance = maximumDistance;

        SetOrbitFromPosition(initialViewpoint.Position);
    }

    public bool Update(InputHandler input, FrameTime time)
    {
        bool updated = updatePending;
        updatePending = false;

        updated |= UpdateRotation(input, time);
        updated |= UpdateZoom(input);

        return updated;
    }

    public Vector3 GetPosition()
    {
        float cosPitch = MathF.Cos(pitch);

        Vector3 offset = new(
            MathF.Sin(yaw) * cosPitch,
            MathF.Sin(pitch),
            MathF.Cos(yaw) * cosPitch
        );

        return target + offset * distance;
    }

    public Viewpoint GetViewpoint()
    {
        return Viewpoint.LookAt(
            GetPosition(),
            target,
            MathUtilities.Vector3Up
        );
    }

    public Vec3<int> GetIndex() => index;

    public void SetIndex(Vec3<int> index)
    {
        this.index = index;
    }

    public void SetTarget(Vector3 target)
    {
        if (this.target == target)
            return;

        this.target = target;
        updatePending = true;
    }

    public void Reset(Vector3 target, in Viewpoint viewpoint)
    {
        this.target = target;
        SetOrbitFromPosition(viewpoint.Position);
        updatePending = true;
    }

    private bool UpdateRotation(InputHandler input, FrameTime time)
    {
        bool updated = false;

        var keyboard = input.Keyboard;

        float yawInput = 0f;
        float pitchInput = 0f;

        if (keyboard.IsDown(Keys.A))
            yawInput -= 1f;

        if (keyboard.IsDown(Keys.D))
            yawInput += 1f;

        if (keyboard.IsDown(Keys.W))
            pitchInput += 1f;

        if (keyboard.IsDown(Keys.S))
            pitchInput -= 1f;

        if (yawInput != 0f || pitchInput != 0f)
        {
            // Keep diagonal orbiting from being faster than movement along one axis
            Vector2 rotationInput = Vector2.Normalize(
                new Vector2(yawInput, pitchInput)
            );

            float rotationDelta = RotationSpeed * time.DeltaSeconds;

            yaw += rotationInput.X * rotationDelta;
            pitch += rotationInput.Y * rotationDelta;

            updated = true;
        }

        var mouse = input.Mouse;

        if (mouse.IsDown(MouseButton.Left) &&
            (mouse.DeltaX != 0f || mouse.DeltaY != 0f))
        {
            yaw -= mouse.DeltaX * MouseRotationSensitivity;
            pitch += mouse.DeltaY * MouseRotationSensitivity;

            updated = true;
        }

        if (!updated)
            return false;

        yaw = MathF.IEEERemainder(yaw, MathF.Tau);
        pitch = Math.Clamp(pitch, -PitchLimit, PitchLimit);

        return true;
    }

    private bool UpdateZoom(InputHandler input)
    {
        float wheelDelta = input.Mouse.ScrollY;

        if (wheelDelta == 0f)
            return false;

        // Multiplicative zoom
        float zoomFactor = MathF.Exp(-wheelDelta * ZoomSensitivity);
        float newDistance = Math.Clamp(
            distance * zoomFactor,
            minimumDistance,
            maximumDistance
        );

        if (newDistance == distance)
            return false;

        distance = newDistance;
        return true;
    }

    private void SetOrbitFromPosition(Vector3 position)
    {
        Vector3 offset = position - target;
        float offsetLength = offset.Length();

        if (offsetLength <= float.Epsilon)
        {
            yaw = 0f;
            pitch = 0f;
            distance = minimumDistance;
            return;
        }

        distance = Math.Clamp(offsetLength, minimumDistance, maximumDistance);

        float horizontalDistance = MathF.Sqrt(
            offset.X * offset.X + offset.Z * offset.Z
        );

        yaw = MathF.Atan2(offset.X, offset.Z);
        pitch = Math.Clamp(
            MathF.Atan2(offset.Y, horizontalDistance),
            -PitchLimit,
            PitchLimit
        );
    }
}
