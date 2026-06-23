namespace SharpCraft.World.Chunks;

internal readonly struct NeighborSet
{
    public Chunk? ZPos { get; init; }
    public Chunk? ZNeg { get; init; }
    public Chunk? XPos { get; init; }
    public Chunk? XNeg { get; init; }
    public Chunk? YPos { get; init; }
    public Chunk? YNeg { get; init; }
    
    public bool All => XNeg != null && XPos != null && YNeg != null && YPos != null && ZNeg != null && ZPos != null;
}