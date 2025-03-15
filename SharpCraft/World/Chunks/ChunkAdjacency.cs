using SharpCraft.MathUtilities;
using System.Collections.Generic;

namespace SharpCraft.World.Chunks;
public class ChunkAdjacency
{
    public Chunk Root { get; set; }
    public ChunkAdjacency XNeg { get; set; }
    public ChunkAdjacency XPos { get; set; }
    public ChunkAdjacency YNeg { get; set; }
    public ChunkAdjacency YPos { get; set; }
    public ChunkAdjacency ZNeg { get; set; }
    public ChunkAdjacency ZPos { get; set; }

    public bool All()
    {
        return XNeg is not null && XPos is not null && ZNeg is not null && ZPos is not null
            && YNeg is not null && YPos is not null;
    }

    public IEnumerable<Vector3I> GetNullChunksIndexes()
    {
        if (XNeg is null)
            yield return Root.Index - new Vector3I(1, 0, 0);
        if (XPos is null)
            yield return Root.Index + new Vector3I(1, 0, 0);
        if (YNeg is null)
            yield return Root.Index - new Vector3I(0, 1, 0);
        if (YPos is null)
            yield return Root.Index + new Vector3I(0, 1, 0);
        if (ZNeg is null)
            yield return Root.Index - new Vector3I(0, 0, 1);
        if (ZPos is null)
            yield return Root.Index + new Vector3I(0, 0, 1);
    }
}
