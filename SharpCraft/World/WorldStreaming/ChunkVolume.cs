using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SharpCraft.World.WorldStreaming;

internal sealed class ChunkVolume
{
    //Number of chunks from the center chunk to the edge of the chunk area
    private readonly int apothem;

    private readonly ConcurrentDictionary<Vec3<int>, Chunk> chunks = [];
    private readonly ConcurrentDictionary<Vec2<int>, int> columnCounts = [];
    private readonly Vec3<sbyte>[] proximityIndexes;

    public ChunkVolume(int apothem)
    {
        this.apothem = apothem;
        proximityIndexes = BuildProximityIndexes();
    }

    public Chunk? this[Vec3<int> index] => chunks.GetValueOrDefault(index);

    public bool TryGetValue(Vec3<int> index, [MaybeNullWhen(false)] out Chunk chunk)
    {
        return chunks.TryGetValue(index, out chunk);
    }

    public bool TryAdd(Chunk chunk)
    {
        if (!chunks.TryAdd(chunk.Index, chunk)) return false;
        
        var col = new Vec2<int>(chunk.Index.X, chunk.Index.Z);
        columnCounts.AddOrUpdate(col, 1, (_, count) => count + 1);
        return true;
    }

    public NeighborSet CollectNeighbors(Chunk chunk)
    {
        TryGetValue(chunk.Index + new Vec3<int>(0, 0, 1), out var zPos);
        TryGetValue(chunk.Index + new Vec3<int>(0, 0, -1), out var zNeg);
        TryGetValue(chunk.Index + new Vec3<int>(1, 0, 0), out var xPos);
        TryGetValue(chunk.Index + new Vec3<int>(-1, 0, 0), out var xNeg);
        TryGetValue(chunk.Index + new Vec3<int>(0, 1, 0), out var yPos);
        TryGetValue(chunk.Index + new Vec3<int>(0, -1, 0), out var yNeg);

        var neighbors = new NeighborSet
        {
            ZPos = zPos,
            ZNeg = zNeg,
            XPos = xPos,
            XNeg = xNeg,
            YPos = yPos,
            YNeg = yNeg
        };

        return neighbors;
    }

    public IEnumerable<Chunk> GetActiveChunks()
    {
        foreach (var chunk in chunks.Values)
        {
            yield return chunk;
        }
    }

    public bool ContainsColumn(int x, int z)
    {
        return columnCounts.TryGetValue(new Vec2<int>(x, z), out var count) && count > 0;
    }

    public List<Vec3<int>> CollectIndexesForGeneration(Vec3<int> center)
    {
        List<Vec3<int>> scheduledForGeneration = [];
        foreach (var proximityIndex in proximityIndexes)
        {
            Vec3<int> index = center + proximityIndex.Into<int>();
            if (!chunks.ContainsKey(index))
            {
                scheduledForGeneration.Add(index);
            }
        }

        return scheduledForGeneration;
    }

    public List<Vec3<int>> CollectIndexesForRemoval(Vec3<int> center)
    {
        IEnumerable<Vec3<int>> activeChunks =
            from i in proximityIndexes
            select i.Into<int>() + center;

        var toRemove = chunks.Keys.Except(activeChunks).ToList();
        return toRemove;
    }

    public void RemoveChunk(Vec3<int> index)
    {
        if (!chunks.Remove(index, out _)) return;

        var col = new Vec2<int>(index.X, index.Z);
        while (columnCounts.TryGetValue(col, out var count))
        {
            if (count <= 1)
            {
                // last one in the column
                if (columnCounts.TryRemove(new KeyValuePair<Vec2<int>, int>(col, count))) break;
                // if someone changed it, re-read and retry
            }
            else
            {
                if (columnCounts.TryUpdate(col, count - 1, count)) break;
            }
        }
    }

    private Vec3<sbyte>[] BuildProximityIndexes()
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

        return indexes.ToArray();
    }
}