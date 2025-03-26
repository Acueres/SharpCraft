using System.Collections.Generic;
using SharpCraft.MathUtilities;

namespace SharpCraft.World.Chunks;

public class AdjacencyGraph
{
    readonly Dictionary<Vec3<int>, ChunkAdjacency> adjacencyMap = [];

    public ChunkAdjacency GetAdjacency(Vec3<int> index)
    {
        if (adjacencyMap.TryGetValue(index, out var res))
        {
            return res;
        }

        return null;
    }

    public void CalculateChunkAdjacency(Chunk chunk)
    {
        if (adjacencyMap.ContainsKey(chunk.Index)) return;

        ChunkAdjacency adjacency = new()
        {
            Root = chunk
        };

        Vec3<int> xNeg = chunk.Index + new Vec3<int>(-1, 0, 0);
        Vec3<int> xPos = chunk.Index + new Vec3<int>(1, 0, 0);
        Vec3<int> yPos = chunk.Index + new Vec3<int>(0, 1, 0);
        Vec3<int> yNeg = chunk.Index + new Vec3<int>(0, -1, 0);
        Vec3<int> zNeg = chunk.Index + new Vec3<int>(0, 0, -1);
        Vec3<int> zPos = chunk.Index + new Vec3<int>(0, 0, 1);

        adjacency.XNeg = adjacencyMap.TryGetValue(xNeg, out ChunkAdjacency value) ? value : null;
        if (adjacency.XNeg != null)
        {
            adjacencyMap[xNeg].XPos = adjacency;
        }

        adjacency.XPos = adjacencyMap.TryGetValue(xPos, out value) ? value : null;
        if (adjacency.XPos != null)
        {
            adjacencyMap[xPos].XNeg = adjacency;
        }

        adjacency.YNeg = adjacencyMap.TryGetValue(yNeg, out value) ? value : null;
        if (adjacency.YNeg != null)
        {
            adjacencyMap[yNeg].YPos = adjacency;
        }

        adjacency.YPos = adjacencyMap.TryGetValue(yPos, out value) ? value : null;
        if (adjacency.YPos != null)
        {
            adjacencyMap[yPos].YNeg = adjacency;
        }

        adjacency.ZNeg = adjacencyMap.TryGetValue(zNeg, out value) ? value : null;
        if (adjacency.ZNeg != null)
        {
            adjacencyMap[zNeg].ZPos = adjacency;
        }

        adjacency.ZPos = adjacencyMap.TryGetValue(zPos, out value) ? value : null;
        if (adjacency.ZPos != null)
        {
            adjacencyMap[zPos].ZNeg = adjacency;
        }

        adjacencyMap.Add(chunk.Index, adjacency);
    }

    public void Dereference(Vec3<int> index)
    {
        ChunkAdjacency adjacency = GetAdjacency(index);

        if (adjacency.ZNeg != null)
        {
            adjacency.ZNeg.ZPos = null;
        }

        if (adjacency.ZPos != null)
        {
            adjacency.ZPos.ZNeg = null;
        }

        if (adjacency.XNeg != null)
        {
            adjacency.XNeg.XPos = null;
        }

        if (adjacency.XPos != null)
        {
            adjacency.XPos.XNeg = null;
        }

        if (adjacency.YNeg != null)
        {
            adjacency.YNeg.YPos = null;
        }

        if (adjacency.YPos != null)
        {
            adjacency.YPos.YNeg = null;
        }

        adjacencyMap.Remove(adjacency.Root.Index);
    }
}
