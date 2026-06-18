namespace SharpCraft.World.Blocks;

internal struct FacesData<T>
{
    public T XPos { get; set; }
    public T XNeg { get; set; }
    public T YPos { get; set; }
    public T YNeg { get; set; }
    public T ZPos { get; set; }
    public T ZNeg { get; set; }

    public readonly T GetValue(FaceDirection face)
    {
        return face switch
        {
            FaceDirection.XPos => XPos,
            FaceDirection.XNeg => XNeg,
            FaceDirection.YPos => YPos,
            FaceDirection.YNeg => YNeg,
            FaceDirection.ZPos => ZPos,
            FaceDirection.ZNeg => ZNeg,
            _ => throw new ArgumentOutOfRangeException(nameof(face)),
        };
    }

    public readonly IEnumerable<T> GetValues()
    {
        yield return ZPos;
        yield return ZNeg;
        yield return YPos;
        yield return YNeg;
        yield return XPos;
        yield return XNeg;
    }
}
