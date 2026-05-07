using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using SharpCraft.Persistence;
using SharpCraft.World.Chunks;
using SharpCraft.World.ChunkStreaming;
using SharpCraft.World.Generation;

namespace SharpCraft.World;

class SpawnResolver(Player player, Parameters parameters, RegionStreaming worldGenerator, ChunkGenerator chunkGenerator)
{
    const int SpawnSearchRadius = 32;
    const int MaxAllowedNeighborDrop = 1;

    public Vector3 Resolve()
    {
        if (parameters.Position != Vector3.Zero)
        {
            player.Position = parameters.Position;
            player.Index = Chunk.WorldToChunkCoords(player.Position);
            worldGenerator.BulkGenerate(player.Position);
            return player.Position;
        }

        Vector3 spawnPosition = FindSpawnPosition();
        worldGenerator.BulkGenerate(spawnPosition);
        return spawnPosition;
    }

    private Vector3 FindSpawnPosition()
    {
        for (int radius = 0; radius <= SpawnSearchRadius; radius++)
        {
            foreach (var (x, z) in EnumerateRing(0, 0, radius))
            {
                int terrainY = chunkGenerator.GetTerrainHeight(x, z);
                int waterY = chunkGenerator.GetWaterHeight(x, z);

                if (waterY > terrainY) continue;
                if (!IsEdgeSafe(x, terrainY, z)) continue;

                return new Vector3(x + 0.5f, terrainY + 1f, z + 0.5f);
            }
        }

        int fallbackY = chunkGenerator.GetTerrainHeight(0, 0);
        return new Vector3(0.5f, fallbackY + 1f, 0.5f);
    }

    private bool IsEdgeSafe(int x, int centerY, int z)
    {
        ReadOnlySpan<(int dx, int dz)> neighbors = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        foreach (var (dx, dz) in neighbors)
        {
            if (centerY - chunkGenerator.GetTerrainHeight(x + dx, z + dz) > MaxAllowedNeighborDrop)
                return false;
        }
        return true;
    }

    private static IEnumerable<(int x, int z)> EnumerateRing(int cx, int cz, int radius)
    {
        if (radius == 0) { yield return (cx, cz); yield break; }

        for (int x = cx - radius; x <= cx + radius; x++)
        {
            yield return (x, cz - radius);
            yield return (x, cz + radius);
        }
        for (int z = cz - radius + 1; z < cz + radius; z++)
        {
            yield return (cx - radius, z);
            yield return (cx + radius, z);
        }
    }
}
