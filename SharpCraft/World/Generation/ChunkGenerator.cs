using System;
using System.Collections.Concurrent;

using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using SharpCraft.MathUtilities;

namespace SharpCraft.World.Generation;

class ChunkGenerator
{
    readonly int seed;
    readonly DatabaseService db;
    readonly BlockMetadataProvider blockMetadata;

    readonly TerrainGenerator terrainGenerator;

    readonly ConcurrentDictionary<Vec2<int>, int[,]> terrainLevelCache = [];
    readonly ConcurrentDictionary<Vec2<int>, int[,]> waterLevelCache = [];
    readonly ConcurrentDictionary<Vec2<int>, BiomeType[,]> biomesCache = [];
    readonly ConcurrentDictionary<Vec2<int>, int> elevationCache = [];

    public ChunkGenerator(Parameters parameters, DatabaseService databaseService, BlockMetadataProvider blockMetadata)
    {
        this.blockMetadata = blockMetadata;
        db = databaseService;

        seed = parameters.Seed;

        terrainGenerator = new(seed, blockMetadata);
    }

    public Chunk GenerateChunk(Vec3<int> index)
    {
        Chunk chunk = new(index, blockMetadata);
        Block[,,] buffer = null;
        int chunkSeed = HashCode.Combine(index.X, index.Y, index.Z, seed);
        Vec2<int> cacheIndex = new(index.X, index.Z);
        Random rnd = new(chunkSeed);

        TerrainData terrainData;
        if (terrainLevelCache.TryGetValue(cacheIndex, out int[,] terrainValue)
            && waterLevelCache.TryGetValue(cacheIndex, out int[,] waterLevelValue)
            && biomesCache.TryGetValue(cacheIndex, out var biomesValue)
            && elevationCache.TryGetValue(cacheIndex, out var elevationValue))
        {
            terrainData = new TerrainData(terrainValue, waterLevelValue, biomesValue, elevationValue);
        }
        else
        {
            terrainData = terrainGenerator.GenerateTerrainData(chunk.Position, cacheIndex);

            terrainLevelCache.TryAdd(cacheIndex, terrainData.TerrainLevel);
            waterLevelCache.TryAdd(cacheIndex, terrainData.WaterLevel);
            biomesCache.TryAdd(cacheIndex, terrainData.BiomesData);
            elevationCache.TryAdd(cacheIndex, terrainData.MaxElevation);
        }

        int maxElevation = terrainData.MaxElevation;
        var terrainLevel = terrainData.TerrainLevel;
        var waterLevel = terrainData.WaterLevel;
        var biomes = terrainData.BiomesData;

        if (chunk.Index.Y * Chunk.Size > maxElevation)
        {
            buffer = db.ApplyDelta(chunk, buffer);
            chunk.BuildPalette(buffer);
            AdjustMaximumElevation(chunk, cacheIndex);
            return chunk;
        }

        buffer = Chunk.GetBlockArray();

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                for (int y = 0; y < Chunk.Size; y++)
                {
                    int currentY = (int)chunk.Position.Y + y;

                    ushort texture = terrainGenerator.Fill(terrainLevel[x, z], currentY, waterLevel[x, z], biomes[x, z], rnd);

                    if (texture != Block.EmptyValue)
                    {
                        buffer[x, y, z] = new(texture);
                    }
                }
            }
        }

        db.ApplyDelta(chunk, buffer);

        chunk.BuildPalette(buffer);

        return chunk;
    }

    public bool IsSunlight(Chunk chunk)
    {
        int maxElevation = elevationCache[new Vec2<int>(chunk.Index.X, chunk.Index.Z)];
        int y = Chunk.WorldToChunkIndex(maxElevation);
        return chunk.Index.Y == y;
    }

    public void RemoveCache(Vec3<int> index)
    {
        Vec2<int> cacheIndex = new(index.X, index.Z);
        terrainLevelCache.TryRemove(cacheIndex, out _);
        waterLevelCache.TryRemove(cacheIndex, out _);
        biomesCache.TryRemove(cacheIndex, out _);
        elevationCache.TryRemove(cacheIndex, out _);
    }

    void AdjustMaximumElevation(Chunk chunk, Vec2<int> cacheIndex)
    {
        int? newMaxElevation = chunk.GetMaximumTerrainElevation();
        if (newMaxElevation.HasValue)
        {
            elevationCache[cacheIndex] = (int)newMaxElevation;
        }
    }
}
