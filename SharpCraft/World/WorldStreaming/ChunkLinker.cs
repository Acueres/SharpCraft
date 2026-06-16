using SharpCraft.World.Chunks;
using SharpCraft.SharpMath;

namespace SharpCraft.World.WorldStreaming;

internal class ChunkLinker(ChunkVolume volume)
{
    private readonly Lock linkingLock = new();
    
    public void LinkChunk(Chunk chunk)
    {
        lock (linkingLock)
        {
            // ZPos
            if (volume.TryGetValue(chunk.Index + new Vec3<int>(0, 0, 1), out var zPosChunk))
            {
                chunk.ZPos = zPosChunk;
                zPosChunk.ZNeg = chunk;
            }
            // ZNeg
            if (volume.TryGetValue(chunk.Index + new Vec3<int>(0, 0, -1), out var zNegChunk))
            {
                chunk.ZNeg = zNegChunk;
                zNegChunk.ZPos = chunk;
            }
            // XPos
            if (volume.TryGetValue(chunk.Index + new Vec3<int>(1, 0, 0), out var xPosChunk))
            {
                chunk.XPos = xPosChunk;
                xPosChunk.XNeg = chunk;
            }
            // XNeg
            if (volume.TryGetValue(chunk.Index + new Vec3<int>(-1, 0, 0), out var xNegChunk))
            {
                chunk.XNeg = xNegChunk;
                xNegChunk.XPos = chunk;
            }
            // YPos
            if (volume.TryGetValue(chunk.Index + new Vec3<int>(0, 1, 0), out var yPosChunk))
            {
                chunk.YPos = yPosChunk;
                yPosChunk.YNeg = chunk;
            }
            // YNeg
            if (volume.TryGetValue(chunk.Index + new Vec3<int>(0, -1, 0), out var yNegChunk))
            {
                chunk.YNeg = yNegChunk;
                yNegChunk.YPos = chunk;
            }
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