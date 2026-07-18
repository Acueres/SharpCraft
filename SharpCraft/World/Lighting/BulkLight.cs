using SharpCraft.SharpMath;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Lighting;

/// <summary>
/// Single-threaded light flood for startup bulk generation.
/// </summary>
internal sealed class BulkLight(Dictionary<Vec3<int>, NeighborSet> neighbors)
{
    private readonly struct Node(Chunk chunk, int x, int y, int z)
    {
        public Chunk Chunk { get; } = chunk;
        public sbyte X { get; } = (sbyte)x;
        public sbyte Y { get; } = (sbyte)y;
        public sbyte Z { get; } = (sbyte)z;

        public LightValue GetLight() => Chunk.Light!.Get(X, Y, Z);
    }

    private readonly Queue<Node> queue = [];
    
    public void SeedSkylight(Chunk chunk)
    {
        chunk.EnsureLight();

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                if (!chunk[x, Chunk.Last, z].IsEmpty) continue;
                
                chunk.Light!.Set(x, Chunk.Last, z, LightValue.Sunlight);
                var node = new Node(chunk, x, Chunk.Last, z);
                queue.Enqueue(node);
            }
        }
    }
    
    public void SeedBlockLights(Chunk chunk)
    {
        chunk.EnsureLight();

        foreach (var src in chunk.GetLightSources())
        {
            byte srcVal = chunk.GetLightSourceValue(src.Into<int>());
            LightValue current = chunk.Light!.Get(src.X, src.Y, src.Z);

            LightValue seeded = new(current.SkyValue, srcVal);
            if (current.Compare(seeded, out LightValue merged))
            {
                chunk.Light.Set(src.X, src.Y, src.Z, merged);
                queue.Enqueue(new Node(chunk, src.X, src.Y, src.Z));
            }
        }
    }
    
    public void Flood()
    {
        while (queue.TryDequeue(out Node node))
        {
            LightValue light = node.GetLight();
            if (light == LightValue.Null) continue;

            // Lateral: both sky and block lose 1
            LightValue lateral = light;
            if (lateral.SkyValue > 0) lateral = lateral.SubtractSkyValue(1);
            if (lateral.BlockValue > 0) lateral = lateral.SubtractBlockValue(1);

            // Downward: full skylight passes unattenuated, block loses 1
            LightValue down = light;
            if (down.SkyValue is < LightValue.MaxValue and > 0)
                down = down.SubtractSkyValue(1);
            if (down.BlockValue > 0)
                down = down.SubtractBlockValue(1);

            int x = node.X, y = node.Y, z = node.Z;
            Chunk chunk = node.Chunk;

            var n = neighbors[chunk.Index];

            // Y+ (lateral), Y- (downward), then the four laterals
            Visit(chunk, x, y + 1, z, n.YPos, x, 0, z, y == Chunk.Last, lateral);
            Visit(chunk, x, y - 1, z, n.YNeg, x, Chunk.Last, z, y == 0, down);
            Visit(chunk, x + 1, y, z, n.XPos, 0, y, z, x == Chunk.Last, lateral);
            Visit(chunk, x - 1, y, z, n.XNeg, Chunk.Last, y, z, x == 0, lateral);
            Visit(chunk, x, y, z + 1, n.ZPos, x, y, 0, z == Chunk.Last, lateral);
            Visit(chunk, x, y, z - 1, n.ZNeg, x, y, Chunk.Last, z == 0, lateral);
        }
    }

    /// <summary>
    /// Propagates <paramref name="next"/> into one neighboring cell, which is
    /// either inside <paramref name="chunk"/> (interior) or in an adjacent chunk
    /// (boundary). On a boundary, <paramref name="boundaryChunk"/> is the adjacent
    /// chunk and (bx,by,bz) are coordinates within it; otherwise (lx,ly,lz) are
    /// the interior coordinates within <paramref name="chunk"/>.
    /// </summary>
    private void Visit(
        Chunk chunk,
        int lx, int ly, int lz,
        Chunk? boundaryChunk,
        int bx, int by, int bz,
        bool isBoundary,
        LightValue next)
    {
        Chunk target;
        int tx, ty, tz;

        if (isBoundary)
        {
            if (boundaryChunk is null) return;
            target = boundaryChunk;
            tx = bx; ty = by; tz = bz;
        }
        else
        {
            target = chunk;
            tx = lx; ty = ly; tz = lz;
        }

        if (!target.IsBlockTransparent(tx, ty, tz) || target.Light is null) return;
        
        if (target.Light.Get(tx, ty, tz).Compare(next, out LightValue merged))
        {
            target.Light.Set(tx, ty, tz, merged);
            queue.Enqueue(new Node(target, tx, ty, tz));
        }
    }
}