namespace SharpCraft.MathUtilities;

public static class MathUtils
{
    public static float SmoothStep(float t) => t * t * (3f - 2f * t);

    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public static float InverseLerp(float a, float b, float value)
    {
        return (value - a) / (b - a);
    }

    public static float Catmull(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}
