using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Chunks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SharpCraft.World.Lighting;

public class LightSystem
{
    readonly ConcurrentQueue<LightNode> lightQueue = [];
    readonly ConcurrentQueue<(LightNode, LightValue)> lightRemovalQueue = [];

    public void InitializeSkylight(Chunk chunk)
    {
        //Propagate light downward from a sky chunk
        Chunk emitterChunk;

        if (chunk.IsEmpty)
        {
            emitterChunk = chunk.YNeg;
        }
        else
        {
            emitterChunk = chunk;
        }

        emitterChunk.InitLight();

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                if (!emitterChunk[x, Chunk.Last, z].IsEmpty) continue;

                emitterChunk.SetLight(x, Chunk.Last, z, LightValue.Sunlight);
                lightQueue.Enqueue(new LightNode(emitterChunk, x, Chunk.Last, z));
            }
        }
    }

    public void InitializeLight(Chunk chunk)
    {
        chunk.InitLight();

        if (chunk.YPos is not null)
        {
            for (int x = 0; x < Chunk.Size; x++)
            {
                for (int z = 0; z < Chunk.Size; z++)
                {
                    if (!chunk.YPos[x, 0, z].IsEmpty) continue;

                    lightQueue.Enqueue(new LightNode(chunk.YPos, x, 0, z));
                }
            }
        }

        SetSourceLight(chunk);
    }

    public HashSet<Chunk> RunBFS()
    {
        HashSet<Chunk> visitedChunks = [];
        while (!lightQueue.IsEmpty)
        {
            lightQueue.TryDequeue(out LightNode node);

            visitedChunks.Add(node.Chunk);

            if (node.IsEmpty) continue;

            BFSPropagate(node.Chunk, node.X, node.Y, node.Z);
        }

        return visitedChunks;
    }

    public HashSet<Chunk> RunRemovalBFS()
    {
        HashSet<Chunk> visitedChunks = [];

        while (!lightRemovalQueue.IsEmpty)
        {
            lightRemovalQueue.TryDequeue(out var value);
            (LightNode node, LightValue lightValue) = value;

            visitedChunks.Add(node.Chunk);

            if (node.IsEmpty) continue;

            BFSRemove(node, lightValue);
        }

        return visitedChunks;
    }

    /// <summary>
    /// Recalculates light after a block is placed or a light source is removed.
    /// This involves removing light invalidated by the update, then fully re-propagating light.
    /// This is a comprehensive update involving both light removal and propagation phases.
    /// </summary>
    /// <param name="chunk">The chunk containing the updated block</param>
    /// <param name="index">The index of the updated block within the chunk</param>
    /// <returns>A HashSet of chunks affected by the light recalculation.</returns>
    public HashSet<Chunk> RecalculateLightOnBlockUpdate(Chunk chunk, Vec3<byte> index)
    {
        lightRemovalQueue.Enqueue((new LightNode(chunk, index.X, index.Y, index.Z), LightValue.Null));
        var removedVisited = RunRemovalBFS();

        foreach (var ch in removedVisited)
        {
            SetSourceLight(ch);
        }

        var visited = RunBFS();

        HashSet<Chunk> combined = [.. removedVisited];
        combined.UnionWith(visited);

        return combined;
    }

    /// <summary>
    /// Recalculates light when a non-source block is removed.
    /// Propagates existing light from neighbors into the newly emptied space.
    /// This is a light-spreading operation.
    /// </summary>
    /// <param name="chunk">The chunk containing the block that was removed</param>
    /// <param name="index">The index of the block that was removed within the chunk</param>
    /// <returns>A HashSet of chunks affected by the light recalculation.</returns>
    public HashSet<Chunk> RecalculateLightOnBlockRemoval(Chunk chunk, Vec3<byte> index)
    {
        var (neighborNodes, _) =
            GetNeighborLightValues(index.X, index.Y, index.Z, chunk);

        foreach (var node in neighborNodes.GetValues())
        {
            lightQueue.Enqueue(node);
        }

        return RunBFS();
    }

    void SetSourceLight(Chunk chunk)
    {
        foreach (Vec3<byte> lightSourceIndex in chunk.GetLightSources())
        {
            int x = lightSourceIndex.X;
            int y = lightSourceIndex.Y;
            int z = lightSourceIndex.Z;

            LightValue light = chunk.GetLight(x, y, z);
            LightValue sourceLight = new(light.SkyValue, chunk.GetLightSourceValue(lightSourceIndex.Into<int>()));

            chunk.SetLight(x, y, z, sourceLight);
            lightQueue.Enqueue(new LightNode(chunk, x, y, z));
        }
    }

    static (FacesData<LightNode> nodes, FacesData<LightValue> lightValues) GetNeighborLightValues(int x, int y, int z, Chunk chunk)
    {
        FacesData<LightNode> nodes = new();
        FacesData<LightValue> lightValues = new();

        if (y == Chunk.Last)
        {
            nodes.YPos = new LightNode(chunk.YPos, x, 0, z);
            LightValue light = chunk.YPos.GetLight(x, 0, z);
            lightValues.YPos = light;
        }
        else
        {
            nodes.YPos = new LightNode(chunk, x, y + 1, z);
            LightValue light = chunk.GetLight(x, y + 1, z);
            lightValues.YPos = light;
        }

        if (y == 0)
        {
            nodes.YNeg = new LightNode(chunk.YNeg, x, Chunk.Last, z);
            LightValue light = chunk.YNeg.GetLight(x, Chunk.Last, z);
            lightValues.YNeg = light;
        }
        else
        {
            nodes.YNeg = new LightNode(chunk, x, y - 1, z);
            LightValue light = chunk.GetLight(x, y - 1, z);
            lightValues.YNeg = light;
        }


        if (x == Chunk.Last)
        {
            nodes.XPos = new LightNode(chunk.XPos, 0, y, z);
            LightValue light = chunk.XPos.GetLight(0, y, z);
            lightValues.XPos = light;
        }
        else
        {
            nodes.XPos = new LightNode(chunk, x + 1, y, z);
            LightValue light = chunk.GetLight(x + 1, y, z);
            lightValues.XPos = light;
        }

        if (x == 0)
        {
            nodes.XNeg = new LightNode(chunk.XNeg, Chunk.Last, y, z);
            LightValue light = chunk.XNeg.GetLight(Chunk.Last, y, z);
            lightValues.XNeg = light;
        }
        else
        {
            nodes.XNeg = new LightNode(chunk, x - 1, y, z);
            LightValue light = chunk.GetLight(x - 1, y, z);
            lightValues.XNeg = light;
        }


        if (z == Chunk.Last)
        {
            nodes.ZPos = new LightNode(chunk.ZPos, x, y, 0);
            LightValue light = chunk.ZPos.GetLight(x, y, 0);
            lightValues.ZPos = light;
        }
        else
        {
            nodes.ZPos = new LightNode(chunk, x, y, z + 1);
            LightValue light = chunk.GetLight(x, y, z + 1);
            lightValues.ZPos = light;
        }

        if (z == 0)
        {
            nodes.ZNeg = new LightNode(chunk.ZNeg, x, y, Chunk.Last);
            LightValue light = chunk.ZNeg.GetLight(x, y, Chunk.Last);
            lightValues.ZNeg = light;
        }
        else
        {
            nodes.ZNeg = new LightNode(chunk, x, y, z - 1);
            LightValue light = chunk.GetLight(x, y, z - 1);
            lightValues.ZNeg = light;
        }

        return (nodes, lightValues);
    }

    void BFSPropagate(Chunk chunk, sbyte x, sbyte y, sbyte z)
    {
        LightValue lightValue = chunk.GetLight(x, y, z);

        if (lightValue == LightValue.Null)
        {
            return;
        }

        LightValue nextLightValue = lightValue;
        if (lightValue.SkyValue > 0)
        {
            nextLightValue = nextLightValue.SubtractSkyValue(1);
        }

        if (lightValue.BlockValue > 0)
        {
            nextLightValue = nextLightValue.SubtractBlockValue(1);
        }

        LightValue nextDownwardLightValue = lightValue;
        if (lightValue.SkyValue < LightValue.MaxValue && lightValue.SkyValue > 0)
        {
            nextDownwardLightValue = nextDownwardLightValue.SubtractSkyValue(1);
        }

        if (lightValue.BlockValue > 0)
        {
            nextDownwardLightValue = nextDownwardLightValue.SubtractBlockValue(1);
        }

        LightValue value;

        if (y == Chunk.Last)
        {
            if (chunk.YPos != null)
            {
                if (chunk.YPos.IsBlockTransparent(x, 0, z) &&
                chunk.YPos.GetLight(x, 0, z).Compare(nextLightValue, out value))
                {
                    chunk.YPos.SetLight(x, 0, z, value);
                    lightQueue.Enqueue(new LightNode(chunk.YPos, x, 0, z));
                }
                else
                {
                    lightQueue.Enqueue(new LightNode(chunk.YPos));
                }
            }
        }
        else if (chunk.IsBlockTransparent(x, y + 1, z) &&
            chunk.GetLight(x, y + 1, z).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x, y + 1, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y + 1, z));
        }

        if (y == 0)
        {
            if (chunk.YNeg != null)
            {
                if (chunk.YNeg.IsBlockTransparent(x, Chunk.Last, z) &&
                    chunk.YNeg.GetLight(x, Chunk.Last, z).Compare(nextDownwardLightValue, out value))
                {
                    chunk.YNeg.SetLight(x, Chunk.Last, z, value);
                    lightQueue.Enqueue(new LightNode(chunk.YNeg, x, Chunk.Last, z));
                }
                else
                {
                    lightQueue.Enqueue(new LightNode(chunk.YNeg));
                }
            }
        }
        else if (chunk.IsBlockTransparent(x, y - 1, z) &&
            chunk.GetLight(x, y - 1, z).Compare(nextDownwardLightValue, out value))
        {
            chunk.SetLight(x, y - 1, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y - 1, z));
        }


        if (x == Chunk.Last)
        {
            if (chunk.XPos != null)
            {
                if (chunk.XPos.IsBlockTransparent(0, y, z) &&
                    chunk.XPos.GetLight(0, y, z).Compare(nextLightValue, out value))
                {
                    chunk.XPos.SetLight(0, y, z, value);
                    lightQueue.Enqueue(new LightNode(chunk.XPos, 0, y, z));
                }
                else
                {
                    lightQueue.Enqueue(new LightNode(chunk.XPos));
                }
            }
        }
        else if (chunk.IsBlockTransparent(x + 1, y, z) &&
            chunk.GetLight(x + 1, y, z).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x + 1, y, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x + 1, y, z));
        }


        if (x == 0)
        {
            if (chunk.XNeg != null)
            {
                if (chunk.XNeg.IsBlockTransparent(Chunk.Last, y, z) &&
                    chunk.XNeg.GetLight(Chunk.Last, y, z).Compare(nextLightValue, out value))
                {
                    chunk.XNeg.SetLight(Chunk.Last, y, z, value);
                    lightQueue.Enqueue(new LightNode(chunk.XNeg, Chunk.Last, y, z));
                }
                else
                {
                    lightQueue.Enqueue(new LightNode(chunk.XNeg));
                }
            }
        }
        else if (chunk.IsBlockTransparent(x - 1, y, z) &&
            chunk.GetLight(x - 1, y, z).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x - 1, y, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x - 1, y, z));
        }


        if (z == Chunk.Last)
        {
            if (chunk.ZPos != null)
            {
                if (chunk.ZPos.IsBlockTransparent(x, y, 0) &&
                    chunk.ZPos.GetLight(x, y, 0).Compare(nextLightValue, out value))
                {
                    chunk.ZPos.SetLight(x, y, 0, value);
                    lightQueue.Enqueue(new LightNode(chunk.ZPos, x, y, 0));
                }
                else
                {
                    lightQueue.Enqueue(new LightNode(chunk.ZPos));
                }
            }
        }
        else if (chunk.IsBlockTransparent(x, y, z + 1) &&
            chunk.GetLight(x, y, z + 1).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x, y, z + 1, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y, z + 1));
        }


        if (z == 0)
        {
            if (chunk.ZNeg != null)
            {
                if (chunk.ZNeg.IsBlockTransparent(x, y, Chunk.Last) &&
                    chunk.ZNeg.GetLight(x, y, Chunk.Last).Compare(nextLightValue, out value))
                {
                    chunk.ZNeg.SetLight(x, y, Chunk.Last, value);
                    lightQueue.Enqueue(new LightNode(chunk.ZNeg, x, y, Chunk.Last));
                }
                else
                {
                    lightQueue.Enqueue(new LightNode(chunk.ZNeg));
                }
            }
        }
        else if (chunk.IsBlockTransparent(x, y, z - 1) &&
            chunk.GetLight(x, y, z - 1).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x, y, z - 1, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y, z - 1));
        }
    }

    void BFSRemove(LightNode node, LightValue target)
    {
        LightValue current = node.GetLight();

        // Avoid infinite loops
        if (current == target) return;

        node.SetLight(target);

        var (neighborNodes, neighborValues) =
            GetNeighborLightValues(node.X, node.Y, node.Z, node.Chunk);

        for (int i = 0; i < 6; i++)
        {
            Faces face = (Faces)i;
            LightNode nNode = neighborNodes.GetValue(face);
            LightValue nVal = neighborValues.GetValue(face);

            if (!nNode.Chunk.IsBlockTransparent(nNode.X, nNode.Y, nNode.Z))
            {
                lightRemovalQueue.Enqueue((new LightNode(nNode.Chunk), LightValue.Null));
                continue;
            }

            // Block light removal
            if (nVal.BlockValue != 0)
            {
                nVal = new LightValue(nVal.SkyValue, 0);
                lightRemovalQueue.Enqueue((nNode, nVal));
            }

            // Sky light attenuation 
            if (nVal.SkyValue != 0 &&
                (nVal.SkyValue < current.SkyValue || (face == Faces.YNeg && nVal.SkyValue <= current.SkyValue)))
            {
                lightRemovalQueue.Enqueue((nNode, new LightValue(0, nVal.BlockValue)));
            }
            else if (nVal.SkyValue > 1)
            {
                lightQueue.Enqueue(nNode);
            }
            else
            {
                lightRemovalQueue.Enqueue((new LightNode(nNode.Chunk), LightValue.Null));
            }
        }
    }

    public static FacesData<LightValue> GetFacesLight(FacesState visibleFaces, int x, int y, int z, Chunk chunk)
    {
        FacesData<LightValue> lightValues = new();

        if (visibleFaces.ZPos)
        {
            if (z == Chunk.Last)
            {
                lightValues.ZPos = chunk.ZPos.GetLight(x, y, 0);
            }
            else
            {
                lightValues.ZPos = chunk.GetLight(x, y, z + 1);
            }
        }

        if (visibleFaces.ZNeg)
        {
            if (z == 0)
            {
                lightValues.ZNeg = chunk.ZNeg.GetLight(x, y, Chunk.Last);
            }
            else
            {
                lightValues.ZNeg = chunk.GetLight(x, y, z - 1);
            }
        }

        if (visibleFaces.YPos)
        {
            if (y == Chunk.Last)
            {
                lightValues.YPos = chunk.YPos.GetLight(x, 0, z);
            }
            else
            {
                lightValues.YPos = chunk.GetLight(x, y + 1, z);
            }
        }

        if (visibleFaces.YNeg)
        {
            if (y == 0)
            {
                lightValues.YNeg = chunk.YNeg.GetLight(x, Chunk.Last, z);
            }
            else
            {
                lightValues.YNeg = chunk.GetLight(x, y - 1, z);
            }
        }


        if (visibleFaces.XPos)
        {
            if (x == Chunk.Last)
            {
                lightValues.XPos = chunk.XPos.GetLight(0, y, z);
            }
            else
            {
                lightValues.XPos = chunk.GetLight(x + 1, y, z);
            }
        }

        if (visibleFaces.XNeg)
        {
            if (x == 0)
            {
                lightValues.XNeg = chunk.XNeg.GetLight(Chunk.Last, y, z);
            }
            else
            {
                lightValues.XNeg = chunk.GetLight(x - 1, y, z);
            }
        }

        return lightValues;
    }
}
