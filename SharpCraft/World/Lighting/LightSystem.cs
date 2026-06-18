using SharpCraft.SharpMath;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SharpCraft.World.Lighting;

internal class LightSystem
{
    readonly ConcurrentQueue<LightNode> lightQueue = [];
    readonly ConcurrentQueue<(LightNode, LightValue)> lightRemovalQueue = [];

    public static void InitializeSkylight(Chunk chunk)
    {
        chunk.EnsureLight();

        for (int x = 0; x < Chunk.Size; x++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                if (!chunk[x, Chunk.Last, z].IsEmpty) continue;
                chunk.LightQueue.Enqueue((LightValue.Sunlight, (byte)x, Chunk.Last, (byte)z));
            }
    }

    public static void InitializeLight(Chunk chunk)
    {
        chunk.EnsureLight();

        // Seed block light sources directly into the queue
        foreach (Vec3<byte> src in chunk.GetLightSources())
        {
            byte srcVal = chunk.GetLightSourceValue(src.Into<int>());
            LightValue current = chunk.GetLight(src.X, src.Y, src.Z);
            chunk.LightQueue.Enqueue((new LightValue(current.SkyValue, srcVal), src.X, src.Y, src.Z));
        }

        GetNeighborsLight(chunk);
    }

    public HashSet<Chunk> RunBFS()
    {
        HashSet<Chunk> touched = [];

        while (lightQueue.TryDequeue(out LightNode node))
        {
            touched.Add(node.Chunk);

            if (node.IsEmpty) continue;

            BFSPropagate(node.Chunk, node.X, node.Y, node.Z);
        }

        return touched;
    }

    public static (HashSet<Chunk> MeshTouched, HashSet<Chunk> LightSpilled) RunBFS(Chunk chunk, in NeighborSet neighbors)
    {
        HashSet<Chunk> lightSpilledChunks = [];
        HashSet<Chunk> meshTouchedChunks = [];
        Queue<LightNode> localQueue = [];

        while (chunk.LightQueue.TryDequeue(out var pending))
        {
            LightValue existing = chunk.GetLight(pending.X, pending.Y, pending.Z);
            if (existing.Compare(pending.Value, out LightValue merged))
            {
                chunk.SetLight(pending.X, pending.Y, pending.Z, merged);
                localQueue.Enqueue(new LightNode(chunk, pending.X, pending.Y, pending.Z));
            }
        }

        while (localQueue.TryDequeue(out var node))
        {
            if (node.IsEmpty) continue;
            var (meshTouched, lightSpilled) = BFSPropagateChunkLocal(node.Chunk, node.X, node.Y, node.Z, localQueue, neighbors);
            
            if (meshTouched.ZPos) meshTouchedChunks.Add(neighbors.ZPos!);
            if (meshTouched.ZNeg) meshTouchedChunks.Add(neighbors.ZNeg!);
            if (meshTouched.XPos) meshTouchedChunks.Add(neighbors.XPos!);
            if (meshTouched.XNeg) meshTouchedChunks.Add(neighbors.XNeg!);
            if (meshTouched.YPos) meshTouchedChunks.Add(neighbors.YPos!);
            if (meshTouched.YNeg) meshTouchedChunks.Add(neighbors.YNeg!);
            
            if (lightSpilled.ZPos) lightSpilledChunks.Add(neighbors.ZPos!);
            if (lightSpilled.ZNeg) lightSpilledChunks.Add(neighbors.ZNeg!);
            if (lightSpilled.XPos) lightSpilledChunks.Add(neighbors.XPos!);
            if (lightSpilled.XNeg) lightSpilledChunks.Add(neighbors.XNeg!);
            if (lightSpilled.YPos) lightSpilledChunks.Add(neighbors.YPos!);
            if (lightSpilled.YNeg) lightSpilledChunks.Add(neighbors.YNeg!);
        }

        return (meshTouchedChunks, lightSpilledChunks);
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

    public HashSet<Chunk> RecalculateLightOnBlockUpdate(Chunk chunk, Vec3<byte> index)
    {
        lightRemovalQueue.Enqueue((new LightNode(chunk, index.X, index.Y, index.Z), LightValue.Null));
        var removedVisited = RunRemovalBFS();

        foreach (var ch in removedVisited)
            SetSourceLight(ch);

        var visited = RunBFS();

        HashSet<Chunk> combined = [.. removedVisited];
        combined.UnionWith(visited);
        return combined;
    }

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

    private static void GetNeighborsLight(Chunk chunk)
    {
        for (int x = 0; x < Chunk.Size; x++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                // Light entering this chunk from the chunk above.
                SeedFromNeighbor(
                    chunk,
                    localX: x, localY: Chunk.Last, localZ: z,
                    neighbor: chunk.YPos,
                    neighborX: x, neighborY: 0, neighborZ: z,
                    downward: true);

                // Light entering from below
                SeedFromNeighbor(
                    chunk,
                    localX: x, localY: 0, localZ: z,
                    neighbor: chunk.YNeg,
                    neighborX: x, neighborY: Chunk.Last, neighborZ: z,
                    downward: false);
            }

        for (int y = 0; y < Chunk.Size; y++)
            for (int z = 0; z < Chunk.Size; z++)
            {
                SeedFromNeighbor(chunk, Chunk.Last, y, z, chunk.XPos, 0, y, z, downward: false);
                SeedFromNeighbor(chunk, 0, y, z, chunk.XNeg, Chunk.Last, y, z, downward: false);
            }

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                SeedFromNeighbor(chunk, x, y, Chunk.Last, chunk.ZPos, x, y, 0, downward: false);
                SeedFromNeighbor(chunk, x, y, 0, chunk.ZNeg, x, y, Chunk.Last, downward: false);
            }
    }

    private static void SeedFromNeighbor(
    Chunk chunk,
    int localX, int localY, int localZ,
    Chunk neighbor,
    int neighborX, int neighborY, int neighborZ,
    bool downward)
    {
        if (neighbor is null)
            return;

        if (!chunk.IsBlockTransparent(localX, localY, localZ))
            return;

        LightValue source = neighbor.GetLight(neighborX, neighborY, neighborZ);
        if (source == LightValue.Null)
            return;

        LightValue incoming = source;

        if (incoming.SkyValue > 0)
        {
            if (!downward || incoming.SkyValue < LightValue.MaxValue)
                incoming = incoming.SubtractSkyValue(1);
        }

        if (incoming.BlockValue > 0)
            incoming = incoming.SubtractBlockValue(1);

        LightValue existing = chunk.GetLight(localX, localY, localZ);
        if (existing.Compare(incoming, out LightValue merged))
        {
            chunk.LightQueue.Enqueue((
                merged,
                (byte)localX,
                (byte)localY,
                (byte)localZ));
        }
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

    private static (FacesState MeshTouched, FacesState LightSpilled) BFSPropagateChunkLocal(
    Chunk chunk,
    int x, int y, int z,
    Queue<LightNode> localQueue,
    in NeighborSet neighbors)
    {
        FacesState meshTouched = default, lightSpilled = default;
        
        LightValue light = chunk.GetLight(x, y, z);
        if (light == LightValue.Null) return (meshTouched, lightSpilled);

        // Lateral attenuation: both sky and block lose 1
        LightValue lateral = light;
        if (lateral.SkyValue > 0) lateral = lateral.SubtractSkyValue(1);
        if (lateral.BlockValue > 0) lateral = lateral.SubtractBlockValue(1);

        // Downward: full skylight passes through unattenuated, block still loses 1
        LightValue down = light;
        if (down.SkyValue < LightValue.MaxValue && down.SkyValue > 0)
            down = down.SubtractSkyValue(1);
        if (down.BlockValue > 0)
            down = down.SubtractBlockValue(1);

        (meshTouched.YPos, lightSpilled.YPos) = PropagateFace(chunk, x, y + 1, z, neighbors.YPos!, x, 0, z, y == Chunk.Last, lateral, localQueue);
        (meshTouched.YNeg, lightSpilled.YNeg) = PropagateFace(chunk, x, y - 1, z, neighbors.YNeg!, x, Chunk.Last, z, y == 0, down, localQueue);
        (meshTouched.XPos, lightSpilled.XPos) = PropagateFace(chunk, x + 1, y, z, neighbors.XPos!, 0, y, z, x == Chunk.Last, lateral, localQueue);
        (meshTouched.XNeg, lightSpilled.XNeg) = PropagateFace(chunk, x - 1, y, z, neighbors.XNeg!, Chunk.Last, y, z, x == 0, lateral, localQueue);
        (meshTouched.ZPos, lightSpilled.ZPos) = PropagateFace(chunk, x, y, z + 1, neighbors.ZPos!, x, y, 0, z == Chunk.Last, lateral, localQueue);
        (meshTouched.ZNeg, lightSpilled.ZNeg) = PropagateFace(chunk, x, y, z - 1, neighbors.ZNeg!, x, y, Chunk.Last, z == 0, lateral, localQueue);
        
        return (meshTouched, lightSpilled);
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
        if (current == target) return;

        node.SetLight(target);

        var (neighborNodes, neighborValues) = GetNeighborLightValues(node.X, node.Y, node.Z, node.Chunk);

        for (int i = 0; i < 6; i++)
        {
            FaceDirection face = (FaceDirection)i;
            LightNode nNode = neighborNodes.GetValue(face);
            LightValue nVal = neighborValues.GetValue(face);

            if (!nNode.Chunk.IsBlockTransparent(nNode.X, nNode.Y, nNode.Z))
            {
                lightRemovalQueue.Enqueue((new LightNode(nNode.Chunk), LightValue.Null));
                continue;
            }

            // Block light removal - evaluate against original nVal
            if (nVal.BlockValue != 0)
            {
                lightRemovalQueue.Enqueue((nNode, new LightValue(nVal.SkyValue, 0)));
            }

            // Sky light removal - evaluate against original nVal, independent of block check
            if (nVal.SkyValue != 0 &&
                (nVal.SkyValue < current.SkyValue || (face == FaceDirection.YNeg && nVal.SkyValue <= current.SkyValue)))
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

    private static (bool MeshTouched, bool LightSpilled) PropagateFace(
        Chunk chunk,
        int lx, int ly, int lz,
        [MaybeNull] Chunk neighbor,
        int nx, int ny, int nz,
        bool isBoundary,
        LightValue next,
        Queue<LightNode> localQueue)
    {
        bool meshTouched = false;
        bool lightSpilled = false;

        if (neighbor is null)
        {
            return (meshTouched, lightSpilled);
        }
        
        if (isBoundary)
        {
            if (!neighbor.IsBlockTransparent(nx, ny, nz))
            {
                meshTouched = true;
            }
            else if (neighbor.GetLight(nx, ny, nz).Compare(next, out LightValue value))
            {
                neighbor.LightQueue.Enqueue((value, (byte)nx, (byte)ny, (byte)nz));
                lightSpilled = true;
            }
        }
        else if (chunk.IsBlockTransparent(lx, ly, lz)
                 && chunk.GetLight(lx, ly, lz).Compare(next, out LightValue value))
        {
            chunk.SetLight(lx, ly, lz, value);
            localQueue.Enqueue(new LightNode(chunk, lx, ly, lz));
        }

        return (meshTouched, lightSpilled);
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
