using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;

using System.Collections.Concurrent;

namespace SharpCraft.World.WorldStreaming;

internal sealed class ChunkVolume
{
    //Number of chunks from the center chunk to the edge of the chunk area
    private readonly uint apothem;

    private readonly ConcurrentDictionary<Vec3<int>, Chunk> chunks = [];
    private readonly ConcurrentDictionary<Vec2<int>, int> columnCounts = [];
    private readonly Vec3<sbyte>[] proximityIndexes;
    private readonly Vec3<sbyte>[] boundaryIndexes;

    private Vec3<int> center;
    private Vec3<int> previousCenter;

    public ChunkVolume(uint apothem)
    {
        this.apothem = apothem;
        proximityIndexes = BuildProximityIndexes();
        boundaryIndexes = BuildBoundaryIndexes();
    }
    
    public void InitCenter(Vec3<int> pos)
    {
        previousCenter = pos;
        center = pos;
    }

    public void SetCenter(Vec3<int> pos)
    {
        previousCenter = center;
        center = pos;
    }

    public void Add(Chunk chunk)
    {
        if (!chunks.TryAdd(chunk.Index, chunk)) return;
        
        var col = new Vec2<int>(chunk.Index.X, chunk.Index.Z);
        columnCounts.AddOrUpdate(col, 1, (_, count) => count + 1);
    }

    public NeighborSet CollectNeighbors(Chunk chunk)
    {
        chunks.TryGetValue(chunk.Index + new Vec3<int>(0, 0, 1), out var zPos);
        chunks.TryGetValue(chunk.Index + new Vec3<int>(0, 0, -1), out var zNeg);
        chunks.TryGetValue(chunk.Index + new Vec3<int>(1, 0, 0), out var xPos);
        chunks.TryGetValue(chunk.Index + new Vec3<int>(-1, 0, 0), out var xNeg);
        chunks.TryGetValue(chunk.Index + new Vec3<int>(0, 1, 0), out var yPos);
        chunks.TryGetValue(chunk.Index + new Vec3<int>(0, -1, 0), out var yNeg);

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
        return chunks.Values;
    }

    public bool ContainsColumn(int x, int z)
    {
        return columnCounts.TryGetValue(new Vec2<int>(x, z), out var count) && count > 0;
    }

    public List<Vec3<int>> CollectIndexesForGeneration()
    {
        var dCenter = center - previousCenter;

        if (Math.Max(Math.Abs(dCenter.X), Math.Max(Math.Abs(dCenter.Y), Math.Abs(dCenter.Z))) == 1)
        {
            return CollectBoundaryIndexesForGeneration();
        }

        return CollectFullIndexesForGeneration();
    }

    private List<Vec3<int>> CollectFullIndexesForGeneration()
    {
        List<Vec3<int>> result = [];
        foreach (var proximityIndex in proximityIndexes)
        {
            Vec3<int> index = center + proximityIndex.Into<int>();
            if (!chunks.ContainsKey(index))
            {
                result.Add(index);
            }
        }

        return result;
    }

    private List<Vec3<int>> CollectBoundaryIndexesForGeneration()
    {
        List<Vec3<int>> result = [];
        foreach (var boundaryIndex in boundaryIndexes)
        {
            Vec3<int> index = center + boundaryIndex.Into<int>();
            if (!IsWithinPreviousActiveVolume(index))
            {
                result.Add(index);
            }
        }

        return result;
    }

    public List<Vec3<int>> CollectIndexesForRemoval()
    {
        var dCenter = center - previousCenter;
        
        if (Math.Max(Math.Abs(dCenter.X), Math.Max(Math.Abs(dCenter.Y), Math.Abs(dCenter.Z))) == 1)
        {
            return CollectBoundaryIndexesForRemoval();
        }
        
        return CollectFullIndexesForRemoval();
    }
    
    private List<Vec3<int>> CollectBoundaryIndexesForRemoval()
    {
        List<Vec3<int>> result = [];
        foreach (var boundaryIndex in boundaryIndexes)
        {
            Vec3<int> index = previousCenter + boundaryIndex.Into<int>();
            if (!IsWithinActiveVolume(index))
            {
                result.Add(index);
            }
        }

        return result;
    }
    
    private List<Vec3<int>> CollectFullIndexesForRemoval()
    {
        List<Vec3<int>> result = [];
        foreach (var proximityIndex in proximityIndexes)
        {
            Vec3<int> index = previousCenter + proximityIndex.Into<int>();
            if (!IsWithinActiveVolume(index))
            {
                result.Add(index);
            }
        }

        return result;
    }

    public bool IsWithinActiveVolume(in Vec3<int> pos)
    {
        int dx = Math.Abs(pos.X - center.X);
        int dy = Math.Abs(pos.Y - center.Y);
        int dz = Math.Abs(pos.Z - center.Z);
        return Math.Max(dx, Math.Max(dy, dz)) <= apothem;
    }
    
    private bool IsWithinPreviousActiveVolume(in Vec3<int> pos)
    {
        int dx = Math.Abs(pos.X - previousCenter.X);
        int dy = Math.Abs(pos.Y - previousCenter.Y);
        int dz = Math.Abs(pos.Z - previousCenter.Z);
        return Math.Max(dx, Math.Max(dy, dz)) <= apothem;
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
        for (int x = -(int)apothem; x <= apothem; x++)
        {
            for (int y = -(int)apothem; y <= apothem; y++)
            {
                for (int z = -(int)apothem; z <= apothem; z++)
                {
                    Vec3<sbyte> index = new((sbyte)x, (sbyte)y, (sbyte)z);
                    indexes.Add(index);
                }
            }
        }

        indexes = [.. indexes.OrderBy(index => index.ManhattanDistance)];

        return indexes.ToArray();
    }
    
    private Vec3<sbyte>[] BuildBoundaryIndexes()
    {
        List<Vec3<sbyte>> indexes = [];
        for (int x = -(int)apothem; x <= apothem; x++)
        {
            for (int y = -(int)apothem; y <= apothem; y++)
            {
                for (int z = -(int)apothem; z <= apothem; z++)
                {
                    if (Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))) < apothem)
                    {
                        continue;
                    }
                    
                    Vec3<sbyte> index = new((sbyte)x, (sbyte)y, (sbyte)z);
                    indexes.Add(index);
                }
            }
        }

        indexes = [.. indexes.OrderBy(index => index.ManhattanDistance)];

        return indexes.ToArray();
    }
}