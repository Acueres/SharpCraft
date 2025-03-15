using System.Collections.Generic;
using SharpCraft.MathUtilities;

namespace SharpCraft.World.Chunks;

public class AdjacencyGraph
{
    readonly Dictionary<Vector3I, ChunkAdjacency> adjacencyMap = [];

    public ChunkAdjacency GetAdjacency(Vector3I index)
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

        Vector3I xNeg = chunk.Index + new Vector3I(-1, 0, 0);
        Vector3I xPos = chunk.Index + new Vector3I(1, 0, 0);
        Vector3I yPos = chunk.Index + new Vector3I(0, 1, 0);
        Vector3I yNeg = chunk.Index + new Vector3I(0, -1, 0);
        Vector3I zNeg = chunk.Index + new Vector3I(0, 0, -1);
        Vector3I zPos = chunk.Index + new Vector3I(0, 0, 1);

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

    public void Dereference(Vector3I index)
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
