using System.Numerics;

namespace SharpCraft.SharpMath;

internal static class MathUtilities
{
    public const float PiOver4 = 0.785398163397448f;
    public const float SquareRootOfThree = 1.7320508075f;
    public static Vector3 Vector3Up { get; } = new(0, 1, 0);
    
    public static bool IsPowerOfTwo(uint n) => BitOperations.IsPow2(n);

    public static uint RoundToNextPowerOfTwo(uint n) => BitOperations.RoundUpToPowerOf2(n);

    public static Matrix4x4 ReverseZPerspective(float fovY, float aspect, float near, float far)
    {
        float y = 1f / MathF.Tan(fovY * 0.5f), x = y / aspect;
        return new Matrix4x4(
            x, 0, 0, 0,
            0, y, 0, 0,
            0, 0, near / (far - near), -1,
            0, 0, far * near / (far - near), 0);
    }
}
