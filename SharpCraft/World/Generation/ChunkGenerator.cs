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

    readonly TopographyGenerator topographyGenerator;
    readonly BiomeMapGenerator biomeMapGenerator;
    readonly GeologyGenerator geologyGenerator;

    readonly ConcurrentDictionary<Vec2<int>, int[,]> heightLevelCache = [];
    readonly ConcurrentDictionary<Vec2<int>, int[,]> waterLevelCache = [];
    readonly ConcurrentDictionary<Vec2<int>, ReliefType[,]> terrainCache = [];
    readonly ConcurrentDictionary<Vec2<int>, int> maxElevationCache = [];

    public ChunkGenerator(Parameters parameters, DatabaseService databaseService, BlockMetadataProvider blockMetadata)
    {
        this.blockMetadata = blockMetadata;
        db = databaseService;

        seed = parameters.Seed;

        topographyGenerator = new(seed);
        biomeMapGenerator = new(seed);
        geologyGenerator = new(blockMetadata);
    }

    public Chunk GenerateChunk(Vec3<int> index)
    {
        Chunk chunk = new(index, blockMetadata);
        Block[,,] buffer = null;
        int chunkSeed = HashCode.Combine(index.X, index.Y, index.Z, seed);
        Vec2<int> cacheIndex = new(index.X, index.Z);
        Random rnd = new(chunkSeed);

        TopographyData topographyData;
        if (heightLevelCache.TryGetValue(cacheIndex, out int[,] heightValue)
            && waterLevelCache.TryGetValue(cacheIndex, out int[,] waterLevelValue)
            && terrainCache.TryGetValue(cacheIndex, out var terrainValue)
            && maxElevationCache.TryGetValue(cacheIndex, out var maxElevationValue))
        {
            topographyData = new TopographyData(heightValue, waterLevelValue, terrainValue, maxElevationValue);
        }
        else
        {
            topographyData = topographyGenerator.GetTopographyData(chunk.Position);

            heightLevelCache.TryAdd(cacheIndex, topographyData.HeightLevel);
            waterLevelCache.TryAdd(cacheIndex, topographyData.WaterLevel);
            terrainCache.TryAdd(cacheIndex, topographyData.ReliefData);
            maxElevationCache.TryAdd(cacheIndex, topographyData.MaxElevation);
        }

        int maxElevation = topographyData.MaxElevation;
        var heightLevel = topographyData.HeightLevel;
        var waterLevel = topographyData.WaterLevel;
        var terrain = topographyData.ReliefData;

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

                    ushort texture = geologyGenerator.GetBlockForLayer(heightLevel[x, z], currentY, waterLevel[x, z], terrain[x, z], rnd);

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
        int maxElevation = maxElevationCache[new Vec2<int>(chunk.Index.X, chunk.Index.Z)];
        int y = Chunk.WorldToChunkIndex(maxElevation);
        return chunk.Index.Y == y;
    }

    public ReliefType GetReliefType(int cx, int cz, int bx, int bz)
    {
        return terrainCache[new Vec2<int>(cx, cz)][bx, bz];
    }

    public void RemoveCache(Vec3<int> index)
    {
        Vec2<int> cacheIndex = new(index.X, index.Z);
        heightLevelCache.TryRemove(cacheIndex, out _);
        waterLevelCache.TryRemove(cacheIndex, out _);
        terrainCache.TryRemove(cacheIndex, out _);
        maxElevationCache.TryRemove(cacheIndex, out _);
    }

    void AdjustMaximumElevation(Chunk chunk, Vec2<int> cacheIndex)
    {
        int? newMaxElevation = chunk.GetMaximumTerrainElevation();
        if (newMaxElevation.HasValue)
        {
            maxElevationCache[cacheIndex] = (int)newMaxElevation;
        }
    }
}
