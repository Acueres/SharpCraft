using Microsoft.Xna.Framework;

namespace SharpCraft.MathUtilities;

public ref struct Raycaster(Vector3 origin, Vector3 direction, float step)
{
    float totalDistance = 0;

    public Vector3 Step()
    {
        totalDistance += step;
        return origin + totalDistance * direction;
    }

    public readonly float Length(Vector3 pos) => (pos - origin).Length();
}
