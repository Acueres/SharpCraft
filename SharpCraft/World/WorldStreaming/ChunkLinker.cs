using SharpCraft.World.Chunks;
using SharpCraft.SharpMath;

namespace SharpCraft.World.WorldStreaming;

internal class ChunkLinker
{
    private readonly Lock linkingLock = new();
    
    public void LinkChunk(Chunk chunk, in NeighborSet neighbors)
    {
        lock (linkingLock)
        {
            chunk.ZPos = neighbors.ZPos;
            neighbors.ZPos?.ZNeg = chunk;

            chunk.ZNeg = neighbors.ZNeg;
            neighbors.ZNeg?.ZPos = chunk;

            chunk.XPos = neighbors.XPos;
            neighbors.XPos?.XNeg = chunk;

            chunk.XNeg = neighbors.XNeg;
            neighbors.XNeg?.XPos = chunk;

            chunk.YPos = neighbors.YPos;
            neighbors.YPos?.YNeg = chunk;

            chunk.YNeg = neighbors.YNeg;
            neighbors.YNeg?.YPos = chunk;
        }
    }

    public void UnlinkChunk(Chunk chunk)
    {
        lock (linkingLock)
        {
            if (chunk.XNeg != null)
            {
                chunk.XNeg.XPos = null;
                chunk.XNeg = null;
            }
            if (chunk.XPos != null)
            {
                chunk.XPos.XNeg = null;
                chunk.XPos = null;
            }
            if (chunk.YNeg != null)
            {
                chunk.YNeg.YPos = null;
                chunk.YNeg = null;
            }
            if (chunk.YPos != null)
            {
                chunk.YPos.YNeg = null;
                chunk.YPos = null;
            }
            if (chunk.ZNeg != null)
            {
                chunk.ZNeg.ZPos = null;
                chunk.ZNeg = null;
            }
            if (chunk.ZPos != null)
            {
                chunk.ZPos.ZNeg = null;
                chunk.ZPos = null;
            }
        }
    }
}