using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using SharpCraft.MathUtilities;

namespace SharpCraft.World.Chunks;

class Region
{
    //Number of chunks from the center chunk to the edge of the chunk area
    readonly int apothem;

    readonly ConcurrentDictionary<Vec3<int>, Chunk> chunks = [];
    readonly ConcurrentBag<Vec3<sbyte>> proximityIndexes = [];
    readonly object linkLock = new();

    public Region(int apothem)
    {
        this.apothem = apothem + 1; // Adding one for unloaded boundary chunks

        proximityIndexes = [.. BuildProximityIndexes()];
    }

    public Chunk this[Vec3<int> index]
    {
        get => chunks[index];
        set => chunks.TryAdd(index, value);
    }

    public IEnumerable<Chunk> GetActiveChunks()
    {
        foreach (var chunk in chunks.Values)
        {
            yield return chunk;
        }
    }

    public List<Vec3<int>> CollectIndexesForGeneration(Vec3<int> center)
    {
        List<Vec3<int>> ungenerated = [];
        foreach (var proximityIndex in proximityIndexes)
        {
            Vec3<int> index = center + proximityIndex.Into<int>();
            if (!chunks.ContainsKey(index))
            {
                ungenerated.Add(index);
            }
        }

        return ungenerated;
    }

    public List<Vec3<int>> CollectIndexesForRemoval(Vec3<int> center)
    {
        int activeRadius = apothem - 1;

        IEnumerable<Vec3<int>> activeChunks =
            from i in proximityIndexes
            where Math.Abs(i.X) <= activeRadius &&
                  Math.Abs(i.Y) <= activeRadius &&
                  Math.Abs(i.Z) <= activeRadius
            select i.Into<int>() + center;

            var toRemove = chunks.Keys.Except(activeChunks).ToList();
            return toRemove;
    }

    public void RemoveChunk(Vec3<int> index)
    {
        chunks.Remove(index, out var chunk);

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

        chunk.Dispose();
    }

    public void LinkChunk(Chunk chunk)
    {
        lock (linkLock)
        {
            // XNeg neighbor
            if (chunks.TryGetValue(chunk.Index + new Vec3<int>(-1, 0, 0), out var xNegChunk))
            {
                chunk.XNeg = xNegChunk;
                xNegChunk.XPos = chunk;
            }
            // XPos neighbor
            if (chunks.TryGetValue(chunk.Index + new Vec3<int>(1, 0, 0), out var xPosChunk))
            {
                chunk.XPos = xPosChunk;
                xPosChunk.XNeg = chunk;
            }
            // YNeg
            if (chunks.TryGetValue(chunk.Index + new Vec3<int>(0, -1, 0), out var yNegChunk))
            {
                chunk.YNeg = yNegChunk;
                yNegChunk.YPos = chunk;
            }
            // YPos
            if (chunks.TryGetValue(chunk.Index + new Vec3<int>(0, 1, 0), out var yPosChunk))
            {
                chunk.YPos = yPosChunk;
                yPosChunk.YNeg = chunk;
            }
            // ZNeg
            if (chunks.TryGetValue(chunk.Index + new Vec3<int>(0, 0, -1), out var zNegChunk))
            {
                chunk.ZNeg = zNegChunk;
                zNegChunk.ZPos = chunk;
            }
            // ZPos
            if (chunks.TryGetValue(chunk.Index + new Vec3<int>(0, 0, 1), out var zPosChunk))
            {
                chunk.ZPos = zPosChunk;
                zPosChunk.ZNeg = chunk;
            }
        }
    }

    List<Vec3<sbyte>> BuildProximityIndexes()
    {
        List<Vec3<sbyte>> indexes = [];
        for (int x = -apothem; x <= apothem; x++)
        {
            for (int y = -apothem; y <= apothem; y++)
            {
                for (int z = -apothem; z <= apothem; z++)
                {
                    Vec3<sbyte> index = new((sbyte)x, (sbyte)y, (sbyte)z);
                    indexes.Add(index);
                }
            }
        }

        indexes = [.. indexes.OrderBy(index => index.ManhattanDistance)];

        return indexes;
    }
}
