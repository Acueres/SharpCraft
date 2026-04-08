using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using SharpCraft.MathUtilities;
using SharpCraft.Persistence;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.World.Generation;

namespace SharpCraft.World;

class SpawnResolver(Player player, Parameters parameters, WorldGenerator worldGenerator, ChunkGenerator chunkGenerator, Region region)
{
    const int SpawnSearchRadius = 32;
    const int MaxAllowedNeighborDrop = 1;
    const int MaxLiftSearch = 16;

    public Vector3 Resolve()
    {
        if (parameters.Position != Vector3.Zero)
        {
            player.Position = parameters.Position;
            player.Index = Chunk.WorldToChunkCoords(player.Position);
            worldGenerator.BulkGenerate(player.Position);
            return player.Position;
        }

        Vector3 preferredPosition = new(0.5f, 0f, 0.5f);
        Vector3 spawnPosition = ResolveSpawnPosition(preferredPosition);

        worldGenerator.BulkGenerate(spawnPosition);

        spawnPosition = LiftSpawnToValidSpace(spawnPosition);
        return spawnPosition;
    }

    private Vector3 ResolveSpawnPosition(Vector3 preferredPosition)
    {
        int originX = (int)MathF.Floor(preferredPosition.X);
        int originZ = (int)MathF.Floor(preferredPosition.Z);

        for (int radius = 0; radius <= SpawnSearchRadius; radius++)
        {
            Vector3? bestCandidate = null;
            int bestScore = int.MaxValue;

            foreach (var (x, z) in EnumerateRing(originX, originZ, radius))
            {
                if (!TryGetSpawnColumnCandidate(x, z, out Vector3 candidate, out int score))
                    continue;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate.HasValue)
                return bestCandidate.Value;
        }

        int fallbackY = chunkGenerator.GetTerrainHeight(originX, originZ);
        return new Vector3(originX + 0.5f, fallbackY + 1f, originZ + 0.5f);
    }

    bool TryGetSpawnColumnCandidate(int worldX, int worldZ, out Vector3 candidate, out int score)
    {
        int centerY = chunkGenerator.GetTerrainHeight(worldX, worldZ);
        int waterY = chunkGenerator.GetWaterHeight(worldX, worldZ);

        candidate = default;
        score = int.MaxValue;

        // Skip water-covered terrain
        if (waterY > centerY)
            return false;

        // Reject columns that sit on obvious cliff edges
        int minNeighborY = centerY;
        int totalSlope = 0;

        ReadOnlySpan<(int dx, int dz)> neighbors =
        [
            (-1, 0),
            ( 1, 0),
            ( 0,-1),
            ( 0, 1),
            (-1,-1),
            (-1, 1),
            ( 1,-1),
            ( 1, 1)
        ];

        foreach (var (dx, dz) in neighbors)
        {
            int neighborY = chunkGenerator.GetTerrainHeight(worldX + dx, worldZ + dz);

            if (neighborY < minNeighborY)
                minNeighborY = neighborY;

            totalSlope += Math.Abs(neighborY - centerY);
        }

        if (centerY - minNeighborY > MaxAllowedNeighborDrop)
            return false;

        candidate = new Vector3(worldX + 0.5f, centerY + 1f, worldZ + 0.5f);
        score = totalSlope;
        return true;
    }

    Vector3 LiftSpawnToValidSpace(Vector3 candidate)
    {
        Vector3 localMin = player.Bound.Min - player.Position;
        Vector3 localMax = player.Bound.Max - player.Position;

        int worldX = (int)MathF.Floor(candidate.X);
        int worldZ = (int)MathF.Floor(candidate.Z);
        int topSolidY = (int)MathF.Floor(candidate.Y) - 1;

        // If the predicted terrain height was slightly low, climb to the true top solid block
        while (IsSolidBlockAt(worldX, topSolidY + 1, worldZ))
        {
            topSolidY++;
        }

        for (int feetY = topSolidY + 1; feetY <= topSolidY + MaxLiftSearch; feetY++)
        {
            Vector3 spawnPos = new(worldX + 0.5f, feetY, worldZ + 0.5f);

            if (IntersectsSolid(spawnPos, localMin, localMax))
                continue;

            if (!HasStableFooting(spawnPos, localMin, localMax))
                continue;

            return spawnPos;
        }

        return candidate;
    }

    bool IntersectsSolid(Vector3 position, Vector3 localMin, Vector3 localMax)
    {
        BoundingBox bounds = new(position + localMin, position + localMax);

        int minX = (int)Math.Floor(bounds.Min.X);
        int minY = (int)Math.Floor(bounds.Min.Y);
        int minZ = (int)Math.Floor(bounds.Min.Z);

        int maxX = (int)Math.Ceiling(bounds.Max.X) - 1;
        int maxY = (int)Math.Ceiling(bounds.Max.Y) - 1;
        int maxZ = (int)Math.Ceiling(bounds.Max.Z) - 1;

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (IsSolidBlockAt(x, y, z))
                        return true;
                }

        return false;
    }

    bool HasStableFooting(Vector3 position, Vector3 localMin, Vector3 localMax)
    {
        float probeY = position.Y + localMin.Y - 0.05f;

        ReadOnlySpan<Vector3> probes =
        [
            new(position.X + localMin.X + 0.05f, probeY, position.Z + localMin.Z + 0.05f),
            new(position.X + localMin.X + 0.05f, probeY, position.Z + localMax.Z - 0.05f),
            new(position.X + localMax.X - 0.05f, probeY, position.Z + localMin.Z + 0.05f),
            new(position.X + localMax.X - 0.05f, probeY, position.Z + localMax.Z - 0.05f),
            new(position.X,                        probeY, position.Z)
        ];

        foreach (var probe in probes)
        {
            int x = (int)MathF.Floor(probe.X);
            int y = (int)MathF.Floor(probe.Y);
            int z = (int)MathF.Floor(probe.Z);

            if (!IsSolidBlockAt(x, y, z))
                return false;
        }

        return true;
    }

    bool IsSolidBlockAt(int worldX, int worldY, int worldZ)
    {
        Vector3 sample = new(worldX + 0.5f, worldY + 0.5f, worldZ + 0.5f);

        Vec3<int> chunkIndex = Chunk.WorldToChunkCoords(sample);
        Chunk chunk = region[chunkIndex];
        if (chunk is null) return false;

        Vec3<byte> blockIndex = Chunk.WorldToBlockCoords(sample);
        Block block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];

        return !block.IsEmpty;
    }

    static IEnumerable<(int x, int z)> EnumerateRing(int centerX, int centerZ, int radius)
    {
        if (radius == 0)
        {
            yield return (centerX, centerZ);
            yield break;
        }

        int minX = centerX - radius;
        int maxX = centerX + radius;
        int minZ = centerZ - radius;
        int maxZ = centerZ + radius;

        for (int x = minX; x <= maxX; x++)
        {
            yield return (x, minZ);
            yield return (x, maxZ);
        }

        for (int z = minZ + 1; z < maxZ; z++)
        {
            yield return (minX, z);
            yield return (maxX, z);
        }
    }
}
