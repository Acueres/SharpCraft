using SharpCraft.SharpMath;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Lighting;

internal class ChunkLight(Chunk chunk)
{
    private readonly LightValue[,,] map = new LightValue[Chunk.Size, Chunk.Size, Chunk.Size];
    private readonly Queue<LightNode> wavefront = new();

    private FacesState boundaryChanged;

    public LightValue Get(int x, int y, int z) => map[x, y, z];

    public void Set(int x, int y, int z, in LightValue value) => map[x, y, z] = value;

    public void SeedSkylight()
    {
        for (int x = 0; x < Chunk.Size; x++)
        for (int z = 0; z < Chunk.Size; z++)
        {
            if (!chunk[x, Chunk.Last, z].IsEmpty) continue;
            Admit(LightValue.Sunlight, x, Chunk.Last, z);
        }
    }

    public void SeedBlockLight()
    {
        foreach (Vec3<byte> src in chunk.GetLightSources())
        {
            byte srcVal = chunk.GetLightSourceValue(src.Into<int>());
            LightValue current = map[src.X, src.Y, src.Z];
            Admit(new LightValue(current.SkyValue, srcVal), src.X, src.Y, src.Z);
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

        Admit(incoming, localX, localY, localZ);
    }
    
    public FacesState Flood()
    {
        while (wavefront.TryDequeue(out LightNode node))
        {
            Propagate(node.X, node.Y, node.Z);
        }

        FacesState changed = boundaryChanged;
        boundaryChanged = default;
        return changed;
    }
    
    private void Admit(in LightValue value, int x, int y, int z)
    {
        if (!map[x, y, z].Compare(value, out LightValue merged)) return;

        map[x, y, z] = merged;
        wavefront.Enqueue(new LightNode(merged, x, y, z));

        if (x == 0) boundaryChanged.XNeg = true;
        else if (x == Chunk.Last) boundaryChanged.XPos = true;

        if (y == 0) boundaryChanged.YNeg = true;
        else if (y == Chunk.Last) boundaryChanged.YPos = true;

        if (z == 0) boundaryChanged.ZNeg = true;
        else if (z == Chunk.Last) boundaryChanged.ZPos = true;
    }

    private void Propagate(int x, int y, int z)
    {
        LightValue light = map[x, y, z];
        if (light == LightValue.Null) return;

        // Lateral: both sky and block lose 1
        LightValue lateral = light;
        if (lateral.SkyValue > 0) lateral = lateral.SubtractSkyValue(1);
        if (lateral.BlockValue > 0) lateral = lateral.SubtractBlockValue(1);

        // Downward (YNeg only): full skylight passes unattenuated, block loses 1
        LightValue down = light;
        if (down.SkyValue is < LightValue.MaxValue and > 0)
            down = down.SubtractSkyValue(1);
        if (down.BlockValue > 0)
            down = down.SubtractBlockValue(1);

        PropagateFace(x, y + 1, z, y == Chunk.Last, lateral);
        PropagateFace(x, y - 1, z, y == 0, down);
        PropagateFace(x + 1, y, z, x == Chunk.Last, lateral);
        PropagateFace(x - 1, y, z, x == 0, lateral);
        PropagateFace(x, y, z + 1, z == Chunk.Last, lateral);
        PropagateFace(x, y, z - 1, z == 0, lateral);
    }

    private void PropagateFace(int lx, int ly, int lz, bool isBoundary, LightValue next)
    {
        if (isBoundary) return;

        if (chunk.IsBlockTransparent(lx, ly, lz))
            Admit(next, lx, ly, lz);
    }

    public FacesData<LightValue> GetFacesLight(FacesState visibleFaces, in NeighborSet neighbors, int x, int y, int z)
    {
        FacesData<LightValue> lightValues = new();

        if (visibleFaces.ZPos)
            lightValues.ZPos = z == Chunk.Last ? NeighborLight(neighbors.ZPos!, x, y, 0) : map[x, y, z + 1];

        if (visibleFaces.ZNeg)
            lightValues.ZNeg = z == 0 ? NeighborLight(neighbors.ZNeg!, x, y, Chunk.Last) : map[x, y, z - 1];

        if (visibleFaces.YPos)
            lightValues.YPos = y == Chunk.Last ? NeighborLight(neighbors.YPos!, x, 0, z) : map[x, y + 1, z];

        if (visibleFaces.YNeg)
            lightValues.YNeg = y == 0 ? NeighborLight(neighbors.YNeg!, x, Chunk.Last, z) : map[x, y - 1, z];

        if (visibleFaces.XPos)
            lightValues.XPos = x == Chunk.Last ? NeighborLight(neighbors.XPos!, 0, y, z) : map[x + 1, y, z];

        if (visibleFaces.XNeg)
            lightValues.XNeg = x == 0 ? NeighborLight(neighbors.XNeg!, Chunk.Last, y, z) : map[x - 1, y, z];

        return lightValues;
    }

    private static LightValue NeighborLight(Chunk neighbor, int x, int y, int z)
        => neighbor.Light?.Get(x, y, z) ?? LightValue.Null;
}