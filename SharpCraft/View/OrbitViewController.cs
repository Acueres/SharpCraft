using SharpCraft.Input;
using SharpCraft.SharpMath;
using SharpCraft.Time;
using SharpCraft.World.Chunks;

using System.Numerics;

namespace SharpCraft.View;

internal sealed class OrbitViewController : ICameraController
{
    private const float RotationSpeed = MathF.PI / 3f;
    private const float ZoomSensitivity = 0.12f;
    private const float MouseRotationSensitivity = 0.003f;
    private const float PitchLimit = MathF.PI / 2f - 0.01f;

    private Vec3<int> targetIndex;
    private Vector3 targetLocalPosition;

    private float yaw;
    private float pitch;
    private float distance;

    private readonly float minimumDistance;
    private readonly float maximumDistance;

    private Viewpoint viewpoint;
    private bool updatePending = true;

    public OrbitViewController(
        Vec3<int> targetIndex,
        Vector3 targetLocalPosition,
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

        (this.targetIndex, this.targetLocalPosition) = NormalizePosition(targetIndex, targetLocalPosition);
        this.minimumDistance = minimumDistance;
        this.maximumDistance = maximumDistance;

        SetOrbitFromPosition(initialViewpoint);
        UpdateViewpoint();
    }

    public bool Update(InputHandler input, FrameTime time)
    {
        bool updated = updatePending;
        updatePending = false;

        updated |= UpdateRotation(input, time);
        updated |= UpdateZoom(input);

        if (updated) UpdateViewpoint();

        return updated;
    }

    public Vector3 GetLocalPosition() => viewpoint.LocalPosition;

    public Viewpoint GetViewpoint() => viewpoint;

    public Vec3<int> GetIndex() => viewpoint.Index;

    private void UpdateViewpoint()
    {
        float cosPitch = MathF.Cos(pitch);

        Vector3 offset = new(
            MathF.Sin(yaw) * cosPitch,
            MathF.Sin(pitch),
            MathF.Cos(yaw) * cosPitch
        );

        var position = NormalizePosition(targetIndex, targetLocalPosition + offset * distance);
        viewpoint = Viewpoint.LookAt(
            position.Index,
            position.LocalPosition,
            targetIndex,
            targetLocalPosition,
            MathUtilities.Vector3Up
        );
    }

    public void SetTarget(Vec3<int> targetIndex, Vector3 targetLocalPosition)
    {
        var target = NormalizePosition(targetIndex, targetLocalPosition);
        if (this.targetIndex == target.Index && this.targetLocalPosition == target.LocalPosition)
            return;

        this.targetIndex = target.Index;
        this.targetLocalPosition = target.LocalPosition;
        UpdateViewpoint();
        updatePending = true;
    }

    public void Reset(Vec3<int> targetIndex, Vector3 targetLocalPosition, in Viewpoint viewpoint)
    {
        (this.targetIndex, this.targetLocalPosition) = NormalizePosition(targetIndex, targetLocalPosition);
        SetOrbitFromPosition(viewpoint);
        UpdateViewpoint();
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

    private void SetOrbitFromPosition(in Viewpoint position)
    {
        Vec3<long> chunkOffset = position.Index.Into<long>() - targetIndex.Into<long>();
        Vector3 offset = new Vector3(chunkOffset.X, chunkOffset.Y, chunkOffset.Z) * Chunk.Size
                         + (position.LocalPosition - targetLocalPosition);
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

    private static (Vec3<int> Index, Vector3 LocalPosition) NormalizePosition(
        Vec3<int> index, Vector3 localPosition)
    {
        var x = NormalizeAxis(index.X, localPosition.X);
        var y = NormalizeAxis(index.Y, localPosition.Y);
        var z = NormalizeAxis(index.Z, localPosition.Z);
        return (new Vec3<int>(x.Index, y.Index, z.Index), new Vector3(x.Local, y.Local, z.Local));
    }

    private static (int Index, float Local) NormalizeAxis(int chunkIndex, float localPosition)
    {
        int carry = checked((int)Math.Floor((double)localPosition / Chunk.Size));
        float local = (float)(localPosition - (double)carry * Chunk.Size);
        if (local >= Chunk.Size)
        {
            local = 0;
            carry = checked(carry + 1);
        }

        return (checked(chunkIndex + carry), local);
    }
}
