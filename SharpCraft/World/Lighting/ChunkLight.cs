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

    public LightValue Get(int x, int y, int z) => map[x, y, z];

    public void Set(int x, int y, int z, in LightValue value) => map[x, y, z] = value;

    public void Enqueue(in LightNode lightNode) => queue.Enqueue(lightNode);

    // -- Seeding --------------------------------------------------------------
    // Seeds are ENQUEUE-ONLY: they never write the map. The value lands in the
    // map when Flood's admit phase dequeues the seed and finds it raises the
    // (still-lower) cell. Writing the map here would make the admit phase's
    // Compare reject the seed as "already at this value", and the seed would
    // never propagate — this is why SeedSkylight must not write-through.
    // Contrast Admit(), below, which DOES write the map.

    public void SeedSkylight()
    {
        for (int x = 0; x < Chunk.Size; x++)
        for (int z = 0; z < Chunk.Size; z++)
        {
            if (!chunk[x, Chunk.Last, z].IsEmpty) continue;
            queue.Enqueue(new LightNode(LightValue.Sunlight, x, Chunk.Last, z));
        }
    }

    public void SeedBlockLight()
    {
        foreach (Vec3<byte> src in chunk.GetLightSources())
        {
            byte srcVal = chunk.GetLightSourceValue(src.Into<int>());
            LightValue current = map[src.X, src.Y, src.Z];
            queue.Enqueue(new LightNode(new LightValue(current.SkyValue, srcVal), src.X, src.Y, src.Z));
        }
    }

    public void SeedNeighborsLight(in NeighborSet neighbors)
    {
        for (int x = 0; x < Chunk.Size; x++)
        for (int z = 0; z < Chunk.Size; z++)
        {
            SeedFromNeighbor(x, Chunk.Last, z, neighbors.YPos, x, 0, z, downward: true);
            SeedFromNeighbor(x, 0, z, neighbors.YNeg, x, Chunk.Last, z, downward: false);
        }

        for (int y = 0; y < Chunk.Size; y++)
        for (int z = 0; z < Chunk.Size; z++)
        {
            SeedFromNeighbor(Chunk.Last, y, z, neighbors.XPos, 0, y, z, downward: false);
            SeedFromNeighbor(0, y, z, neighbors.XNeg, Chunk.Last, y, z, downward: false);
        }

        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            SeedFromNeighbor(x, y, Chunk.Last, neighbors.ZPos, x, y, 0, downward: false);
            SeedFromNeighbor(x, y, 0, neighbors.ZNeg, x, y, Chunk.Last, downward: false);
        }
    }

    private void SeedFromNeighbor(
        int localX, int localY, int localZ,
        Chunk? neighbor,
        int neighborX, int neighborY, int neighborZ,
        bool downward)
    {
        if (neighbor?.Light is null) return;
        if (!chunk.IsBlockTransparent(localX, localY, localZ)) return;

        LightValue source = neighbor.Light.Get(neighborX, neighborY, neighborZ);
        if (source == LightValue.Null) return;

        LightValue incoming = source;
        if (incoming.SkyValue > 0)
        {
            if (!downward || incoming.SkyValue < LightValue.MaxValue)
                incoming = incoming.SubtractSkyValue(1);
        }
        if (incoming.BlockValue > 0)
            incoming = incoming.SubtractBlockValue(1);

        // Enqueue-only seed (see Seeding note): compare to decide if it's worth
        // queuing, but do NOT write the map.
        LightValue existing = map[localX, localY, localZ];
        if (existing.Compare(incoming, out LightValue merged))
            queue.Enqueue(new LightNode(merged, localX, localY, localZ));
    }

    // -- Flood ----------------------------------------------------------------

    public (FacesState MeshTouched, FacesData<List<LightNode>> SpilledLight) Flood(in NeighborSet neighbors)
    {
        FacesState meshTouched = default;
        FacesData<List<LightNode>> spilledLight = new()
        {
            ZPos = [], ZNeg = [], XPos = [], XNeg = [], YPos = [], YNeg = []
        };

        // Admit phase: drain the seed/deposit nodes currently queued, writing
        // any that raise their cell back into the map and re-queuing them for
        // propagation. Bounded by the current count so propagation enqueues
        // aren't processed here.
        int seeded = queue.Count;
        for (int i = 0; i < seeded; i++)
        {
            if (!queue.TryDequeue(out var node)) break;
            Admit(node.Value, node.X, node.Y, node.Z);
        }

        // Propagation phase: drain everything Admit re-queued, plus everything
        // propagation itself enqueues, until the wavefront is exhausted.
        while (queue.TryDequeue(out var node))
        {
            var (mt, sp) = Propagate(node.X, node.Y, node.Z, neighbors);
            Accumulate(ref meshTouched, spilledLight, mt, sp);
        }

        return (meshTouched, spilledLight);
    }

    /// <summary>
    /// Map-writing entry: if <paramref name="value"/> raises the cell, write it
    /// and queue the cell for propagation. Shared by the admit phase and the
    /// interior branch of PropagateFace.
    /// </summary>
    private void Admit(in LightValue value, int x, int y, int z)
    {
        if (map[x, y, z].Compare(value, out LightValue merged))
        {
            map[x, y, z] = merged;
            queue.Enqueue(new LightNode(merged, x, y, z));
        }
    }

    private (FacesState MeshTouched, FacesData<LightNode?> SpilledLight) Propagate(
        int x, int y, int z,
        in NeighborSet neighbors)
    {
        FacesState meshTouched = default;
        FacesData<LightNode?> spilledLight = new();

        LightValue light = map[x, y, z];
        if (light == LightValue.Null) return (meshTouched, spilledLight);

        // Lateral: both sky and block lose 1.
        LightValue lateral = light;
        if (lateral.SkyValue > 0) lateral = lateral.SubtractSkyValue(1);
        if (lateral.BlockValue > 0) lateral = lateral.SubtractBlockValue(1);

        // Downward (YNeg only): full skylight passes unattenuated, block loses 1.
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

    private (bool MeshTouched, LightNode? SpilledLight) PropagateFace(
        int lx, int ly, int lz,
        Chunk? neighbor,
        int nx, int ny, int nz,
        bool isBoundary,
        LightValue next)
    {
        if (isBoundary)
        {
            if (neighbor is null) return (false, null);
            
            return neighbor.IsBlockTransparent(nx, ny, nz)
                ? (false, new LightNode(next, nx, ny, nz))
                : (true, null);
        }

        if (chunk.IsBlockTransparent(lx, ly, lz))
            Admit(next, lx, ly, lz);

        return (false, null);
    }

    private static void Accumulate(
        ref FacesState meshTouched,
        FacesData<List<LightNode>> spill,
        in FacesState mt,
        in FacesData<LightNode?> sp)
    {
        if (mt.ZPos) meshTouched.ZPos = true;
        if (mt.ZNeg) meshTouched.ZNeg = true;
        if (mt.XPos) meshTouched.XPos = true;
        if (mt.XNeg) meshTouched.XNeg = true;
        if (mt.YPos) meshTouched.YPos = true;
        if (mt.YNeg) meshTouched.YNeg = true;

        if (sp.ZPos is { } zp) spill.ZPos.Add(zp);
        if (sp.ZNeg is { } zn) spill.ZNeg.Add(zn);
        if (sp.XPos is { } xp) spill.XPos.Add(xp);
        if (sp.XNeg is { } xn) spill.XNeg.Add(xn);
        if (sp.YPos is { } yp) spill.YPos.Add(yp);
        if (sp.YNeg is { } yn) spill.YNeg.Add(yn);
    }

    // -- Mesh-time face sampling ----------------------------------------------

    public FacesData<LightValue> GetFacesLight(FacesState visibleFaces, in NeighborSet neighbors, int x, int y, int z)
    {
        FacesData<LightValue> lightValues = new();

        if (visibleFaces.ZPos)
            lightValues.ZPos = z == Chunk.Last ? NeighborLight(neighbors.ZPos, x, y, 0) : map[x, y, z + 1];

        if (visibleFaces.ZNeg)
            lightValues.ZNeg = z == 0 ? NeighborLight(neighbors.ZNeg, x, y, Chunk.Last) : map[x, y, z - 1];

        if (visibleFaces.YPos)
            lightValues.YPos = y == Chunk.Last ? NeighborLight(neighbors.YPos, x, 0, z) : map[x, y + 1, z];

        if (visibleFaces.YNeg)
            lightValues.YNeg = y == 0 ? NeighborLight(neighbors.YNeg, x, Chunk.Last, z) : map[x, y - 1, z];

        if (visibleFaces.XPos)
            lightValues.XPos = x == Chunk.Last ? NeighborLight(neighbors.XPos, 0, y, z) : map[x + 1, y, z];

        if (visibleFaces.XNeg)
            lightValues.XNeg = x == 0 ? NeighborLight(neighbors.XNeg, Chunk.Last, y, z) : map[x - 1, y, z];

        return lightValues;
    }
    
    private static LightValue NeighborLight(Chunk? neighbor, int x, int y, int z)
        => neighbor?.Light?.Get(x, y, z) ?? LightValue.Null;
}