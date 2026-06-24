using System.Collections.Concurrent;
using SharpCraft.SharpMath;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Lighting;

internal class ChunkLight(Chunk chunk)
{
    private readonly LightValue[,,] map = new LightValue[Chunk.Size, Chunk.Size, Chunk.Size];
    private readonly ConcurrentQueue<LightNode> queue = [];
    private readonly HashSet<Vec3<byte>> lightSources = [];
    
    public LightValue Get(int x, int y, int z)
    {
        return map[x, y, z];
    }
    
    public void Set(int x, int y, int z, in LightValue value)
    {
        map[x, y, z] = value;
    }
    
    public void Enqueue(in LightNode lightNode)
    {
        queue.Enqueue(lightNode);
    }
    
    public void SeedSkylight()
    {
        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                if (!chunk[x, Chunk.Last, z].IsEmpty) continue;
                
                LightNode node = new LightNode(LightValue.Sunlight, x, Chunk.Last, z);
                queue.Enqueue(node);
            }
        }
    }
    
    public void SeedBlockLight()
    {
        // Seed block light sources directly into the queue
        foreach (Vec3<byte> src in chunk.GetLightSources())
        {
            byte srcVal = chunk.GetLightSourceValue(src.Into<int>());
            LightValue current = map[src.X, src.Y, src.Z];
            LightValue value = new LightValue(current.SkyValue, srcVal);
            LightNode node = new LightNode(value, src.X, src.Y, src.Z);
            queue.Enqueue(node);
        }
    }
    
    public void SeedNeighborsLight()
    {
        for (int x = 0; x < Chunk.Size; x++)
        for (int z = 0; z < Chunk.Size; z++)
        {
            // Light entering this chunk from the chunk above.
            SeedFromNeighbor(
                localX: x, localY: Chunk.Last, localZ: z,
                neighbor: chunk.YPos,
                neighborX: x, neighborY: 0, neighborZ: z,
                downward: true);

            // Light entering from below
            SeedFromNeighbor(
                localX: x, localY: 0, localZ: z,
                neighbor: chunk.YNeg,
                neighborX: x, neighborY: Chunk.Last, neighborZ: z,
                downward: false);
        }

        for (int y = 0; y < Chunk.Size; y++)
        for (int z = 0; z < Chunk.Size; z++)
        {
            SeedFromNeighbor(Chunk.Last, y, z, chunk.XPos, 0, y, z, downward: false);
            SeedFromNeighbor(0, y, z, chunk.XNeg, Chunk.Last, y, z, downward: false);
        }

        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            SeedFromNeighbor(x, y, Chunk.Last, chunk.ZPos, x, y, 0, downward: false);
            SeedFromNeighbor(x, y, 0, chunk.ZNeg, x, y, Chunk.Last, downward: false);
        }
    }
    
    private void SeedFromNeighbor(
        int localX, int localY, int localZ,
        Chunk? neighbor,
        int neighborX, int neighborY, int neighborZ,
        bool downward)
    {
        if (neighbor?.Light is null)
            return;

        if (!chunk.IsBlockTransparent(localX, localY, localZ))
            return;

        LightValue source = neighbor.Light.Get(neighborX, neighborY, neighborZ);
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

        LightValue existing = map[localX, localY, localZ];
        if (existing.Compare(incoming, out LightValue merged))
        {
            LightNode node = new LightNode(merged, localX, localY, localZ);
            queue.Enqueue(node);
        }
    }

    public (FacesState MeshTouched, FacesData<List<LightNode>> SpilledLight) Flood(in NeighborSet neighbors)
    {
        FacesState meshTouched = default;
        FacesData<List<LightNode>> spilledLight = new()
        {
            ZPos = [],
            ZNeg = [],
            XPos = [],
            XNeg = [],
            YPos = [],
            YNeg = []
        };

        int count = queue.Count;
        for (int i = 0; i < count; i++)
        {
            if (!queue.TryDequeue(out var node)) continue;

            LightValue existing = map[node.X, node.Y, node.Z];

            if (existing.Compare(node.Value, out LightValue merged))
            {
                map[node.X, node.Y, node.Z] = merged;
                queue.Enqueue(node);
            }
        }

        while (queue.TryDequeue(out var node))
        {
            var (meshTouchedPerNode, spilledLightPerNode) =
                Propagate(node.X, node.Y, node.Z, neighbors);

            if (meshTouchedPerNode.ZPos) meshTouched.ZPos = true;
            if (meshTouchedPerNode.ZNeg) meshTouched.ZNeg = true;
            if (meshTouchedPerNode.XPos) meshTouched.XPos = true;
            if (meshTouchedPerNode.XNeg) meshTouched.XNeg = true;
            if (meshTouchedPerNode.YPos) meshTouched.YPos = true;
            if (meshTouchedPerNode.YNeg) meshTouched.YNeg = true;

            if (spilledLightPerNode.ZPos is not null) spilledLight.ZPos.Add(spilledLightPerNode.ZPos.Value);
            if (spilledLightPerNode.ZNeg is not null) spilledLight.ZNeg.Add(spilledLightPerNode.ZNeg.Value);
            if (spilledLightPerNode.XPos is not null) spilledLight.XPos.Add(spilledLightPerNode.XPos.Value);
            if (spilledLightPerNode.XNeg is not null) spilledLight.XNeg.Add(spilledLightPerNode.XNeg.Value);
            if (spilledLightPerNode.YPos is not null) spilledLight.YPos.Add(spilledLightPerNode.YPos.Value);
            if (spilledLightPerNode.YNeg is not null) spilledLight.YNeg.Add(spilledLightPerNode.YNeg.Value);
        }

        return (meshTouched, spilledLight);
    }

    private (FacesState MeshTouched, FacesData<LightNode?> SpilledLight) Propagate(
        int x, int y, int z,
        in NeighborSet neighbors)
    {
        FacesState meshTouched = default;
        FacesData<LightNode?> spilledLight = new();

        LightValue light = map[x, y, z];
        if (light == LightValue.Null) return (meshTouched, spilledLight);

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

        (meshTouched.YPos, spilledLight.YPos) = PropagateFace(x, y + 1, z, neighbors.YPos, x, 0, z, y == Chunk.Last, lateral);
        (meshTouched.YNeg, spilledLight.YNeg) = PropagateFace(x, y - 1, z, neighbors.YNeg, x, Chunk.Last, z, y == 0, down);
        (meshTouched.XPos, spilledLight.XPos) = PropagateFace(x + 1, y, z, neighbors.XPos, 0, y, z, x == Chunk.Last, lateral);
        (meshTouched.XNeg, spilledLight.XNeg) = PropagateFace(x - 1, y, z, neighbors.XNeg, Chunk.Last, y, z, x == 0, lateral);
        (meshTouched.ZPos, spilledLight.ZPos) = PropagateFace(x, y, z + 1, neighbors.ZPos, x, y, 0, z == Chunk.Last, lateral);
        (meshTouched.ZNeg, spilledLight.ZNeg) = PropagateFace(x, y, z - 1, neighbors.ZNeg, x, y, Chunk.Last, z == 0, lateral);
        
        return (meshTouched, spilledLight);
    }
    
    private (bool MeshTouched, LightNode? spilledLight) PropagateFace(
        int lx, int ly, int lz,
        Chunk? neighbor,
        int nx, int ny, int nz,
        bool isBoundary,
        LightValue next)
    {
        bool meshTouched = false;
        LightNode? spilledLight = null;

        if (neighbor is null)
        {
            return (meshTouched, spilledLight);
        }
        
        if (isBoundary)
        {
            if (neighbor.IsBlockTransparent(nx, ny, nz))
            {
                spilledLight = new LightNode(next, nx, ny, nz);
            }
            else
            {
                meshTouched = true;
            }
        }
        else if (chunk.IsBlockTransparent(lx, ly, lz)
                 && map[lx, ly, lz].Compare(next, out LightValue merged))
        {
            map[lx, ly, lz] = merged;
            queue.Enqueue(new LightNode(merged, lx, ly, lz));
        }

        return (meshTouched, spilledLight);
    }
    
    public FacesData<LightValue> GetFacesLight(FacesState visibleFaces, in NeighborSet neighbors, int x, int y, int z)
    {
        FacesData<LightValue> lightValues = new();

        if (visibleFaces.ZPos)
        {
            if (z == Chunk.Last)
            {
                lightValues.ZPos = neighbors.ZPos!.Light is null
                    ? LightValue.Null
                    : neighbors.ZPos!.Light!.Get(x, y, 0);
            }
            else
            {
                lightValues.ZPos = map[x, y, z + 1];
            }
        }

        if (visibleFaces.ZNeg)
        {
            if (z == 0)
            {
                lightValues.ZNeg = neighbors.ZNeg!.Light is null ? LightValue.Null :
                    neighbors.ZNeg!.Light!.Get(x, y, Chunk.Last);
            }
            else
            {
                lightValues.ZNeg = map[x, y, z - 1];
            }
        }

        if (visibleFaces.YPos)
        {
            if (y == Chunk.Last)
            {
                lightValues.YPos = neighbors.YPos!.Light is null ? LightValue.Null : neighbors.YPos!.Light.Get(x, 0, z);
            }
            else
            {
                lightValues.YPos = map[x, y + 1, z];
            }
        }

        if (visibleFaces.YNeg)
        {
            if (y == 0)
            {
                lightValues.YNeg = neighbors.YNeg!.Light is null
                    ? LightValue.Null
                    : neighbors.YNeg!.Light.Get(x, Chunk.Last, z);
            }
            else
            {
                lightValues.YNeg = map[x, y - 1, z];
            }
        }


        if (visibleFaces.XPos)
        {
            if (x == Chunk.Last)
            {
                lightValues.XPos = neighbors.XPos!.Light is null ? LightValue.Null : neighbors.XPos!.Light.Get(0, y, z);
            }
            else
            {
                lightValues.XPos = map[x + 1, y, z];
            }
        }

        if (visibleFaces.XNeg)
        {
            if (x == 0)
            {
                lightValues.XNeg = neighbors.XNeg!.Light is null
                    ? LightValue.Null
                    : neighbors.XNeg!.Light.Get(Chunk.Last, y, z);
            }
            else
            {
                lightValues.XNeg = map[x - 1, y, z];
            }
        }

        return lightValues;
    }
}